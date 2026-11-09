using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using ResolutionSwitcher;

internal static class VibranceSmoke
{
    private sealed class Driver : IVibranceDriver
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int> { { "one", 12 }, { "two", -8 } };
        public int Writes; public string FailRead = "", FailWrite = "";
        public Action<string, int> BeforeWrite;
        public VibranceLevel Read(string device) { if (device == FailRead) throw new InvalidOperationException("read failure"); return new VibranceLevel { Current = Values[device], Minimum = -100, Maximum = 100, Default = 0 }; }
        public void Write(string device, int level) { if (device == FailWrite) throw new InvalidOperationException("write failure"); if (BeforeWrite != null) BeforeWrite(device, level); Values[device] = level; Writes++; }
    }
    private sealed class Trial : ICustomResolutionTrial
    {
        public readonly List<string> Calls = new List<string>(); public string Fail = "";
        private void Step(string step) { Calls.Add(step); if (step == Fail) throw new InvalidOperationException(step); }
        public void Begin() { Step("begin"); } public void Save() { Step("save"); } public void Revert() { Step("revert"); } public void RestoreLayout() { Step("layout"); }
    }
    private sealed class RateTrial : IRefreshRateTrial
    {
        public int Applies, Restores; public bool Fail;
        public void ApplyAndVerify() { Applies++; if (Fail) throw new InvalidOperationException("Test apply failure"); }
        public void Restore() { Restores++; }
    }
    private static void Assert(bool condition, string error) { if (!condition) throw new Exception(error); }
    private static void Fails(Action action) { bool failed = false; try { action(); } catch (InvalidOperationException) { failed = true; } Assert(failed, "Expected controlled failure"); }
    [STAThread] private static void Main(string[] args)
    {
        if (args.Contains("--probe")) { Probe(); return; }
        string folder = Path.Combine(Path.GetTempPath(), "LucentVideoSmoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            DesktopVibranceStore store = new DesktopVibranceStore(folder); Driver driver = new Driver();
            DesktopVibranceSession session = new DesktopVibranceSession(driver, store);
            Assert(driver.Writes == 0 && !session.Enabled, "Constructing session changed colors");
            session.Enable(new[] { "one", "two" }, 70);
            Assert(driver.Values["one"] == 40 && driver.Values["two"] == 40 && store.LoadRecovery().Count == 2, "Desktop-wide enable failed");
            session.Restore(); Assert(driver.Values["one"] == 12 && driver.Values["two"] == -8 && store.LoadRecovery().Count == 0, "Exact per-display restore failed");
            session.Enable(new[] { "one", "two" }, 70); driver.Values["two"] = 27;
            Assert(session.Restore() && driver.Values["one"] == 12 && driver.Values["two"] == 27, "External color change overwritten");
            driver.FailRead = "two"; int writes = driver.Writes;
            Fails(() => session.Enable(new[] { "one", "two" }, 70)); Assert(driver.Writes == writes, "Partial write before capabilities checked"); driver.FailRead = "";
            driver.FailWrite = "two";
            Fails(() => session.Enable(new[] { "one", "two" }, 70)); Assert(driver.Values["one"] == 12 && driver.Values["two"] == 27 && !session.Enabled, "Failed enable did not roll back"); driver.FailWrite = "";
            session.Enable(new[] { "one", "two" }, 80); driver.FailWrite = "two";
            Fails(() => session.Restore()); Assert(session.HasPendingRestore && store.LoadRecovery().Count == 1 && driver.Values["one"] == 12, "Restore failure lost pending display"); driver.FailWrite = "";
            session.Restore(); Assert(driver.Values["two"] == 27 && store.LoadRecovery().Count == 0, "Restore retry failed");
            session.Enable(new[] { "one", "two" }, 65);
            var record = store.LoadRecovery(); var restarted = new DesktopVibranceSession(driver, store);
            restarted.Restore(); Assert(store.LoadRecovery().Count == 2, "Inactive session cleared crash recovery");
            restarted.Recover(record); Assert(driver.Values["one"] == 12 && driver.Values["two"] == 27, "Explicit crash recovery failed");
            File.WriteAllLines(Path.Combine(folder, "vibrance-recovery.ini"), new[] { "LucentVibrance1", "one", "12", "40" }); driver.Values["one"] = 40;
            restarted.Recover(store.LoadRecovery()); Assert(driver.Values["one"] == 12 && store.LoadRecovery().Count == 0, "Previous preview journal recovery failed");
            store.Percent = 88; store.SaveProfile(); DesktopVibranceStore loaded = new DesktopVibranceStore(folder); loaded.LoadProfile(); Assert(loaded.Percent == 88, "Preference roundtrip failed");
            Console.WriteLine("PASS: desktop vibrance opt-in, all-monitor capability checks, exact per-monitor restore, external colors, rollback, restore retries, crash recovery, legacy recovery, preference persistence.");
            CheckTrials(); CheckRefreshOnly(); CheckResolutionOnly(); CheckLiveVibrance(folder); CheckTheme(); CheckPreferencesAndBindings(folder); CheckIdentity(); CheckCrosshair(); CheckVibranceLayout(store, folder);
            if (args.Contains("--ui")) CheckUi(args.Contains("--screenshots"),new DesktopVibranceStore(Path.Combine(folder,"ui-vibrance")));
        }
        finally { Directory.Delete(folder, true); }
    }
    private static void CheckTrials()
    {
        Assert(Marshal.SizeOf(typeof(NvTimingExtra)) == 64 && Marshal.SizeOf(typeof(NvTiming)) == 96 && Marshal.SizeOf(typeof(NvCustomMode)) == 144 && Marshal.SizeOf(typeof(NvTimingInput)) == 32, "NVAPI struct size mismatch");
        Assert(Marshal.OffsetOf(typeof(NvTimingInput), "Type").ToInt32() == 28, "NV_TIMING_FLAG union storage mismatch");
        Assert(Marshal.SizeOf(typeof(NvConfigDetails)) == 128 && Marshal.SizeOf(typeof(NvConfigPath)) == (IntPtr.Size == 8 ? 48 : 28) && Marshal.SizeOf(typeof(NvConfigTarget)) == (IntPtr.Size == 8 ? 24 : 12), "NVIDIA scaling ABI mismatch");
        byte[] scaling = Enumerable.Range(0, 128).Select(index => (byte)index).ToArray(), previous = (byte[])scaling.Clone();
        NvidiaScalingConfig.SetFullscreenField(scaling);
        Assert(BitConverter.ToInt32(scaling, 8) == 2, "Full-screen GPU scaling value incorrect");
        Assert(Enumerable.Range(0,128).All(index => (index >= 8 && index < 12) || scaling[index] == previous[index]), "Scaling edit changed another setting");
        Assert(Marshal.OffsetOf(typeof(NvCustomMode), "Timing").ToInt32() == 44 && Marshal.OffsetOf(typeof(NvTiming), "PixelClock").ToInt32() == 28, "NVAPI field offset mismatch");
        Trial canceled = new Trial(); CustomResolutionOutcome no = CustomResolutionTransaction.Run(canceled, () => false);
        Assert(!no.Saved && string.Join(",", canceled.Calls) == "begin,revert,layout", "Unconfirmed trial saved or failed to restore");
        Trial confirmed = new Trial(); var yes = CustomResolutionTransaction.Run(confirmed, () => true);
        Assert(yes.Saved && string.Join(",", confirmed.Calls) == "begin,save,revert,layout", "Confirmed trial flow failed");
        foreach (string step in new[] { "begin", "save", "revert" })
        {
            Trial failed = new Trial { Fail = step }; var outcome = CustomResolutionTransaction.Run(failed, () => true);
            Assert(outcome.Error.Length > 0 && failed.Calls.Last() == "layout", "Failed trial skipped layout restoration");
        }
        long elapsed = 0; bool saved = CustomResolutionWorker.WaitForDecision(() => elapsed, () => false, () => false, () => { elapsed += 50; return false; });
        Assert(!saved && elapsed == 15000, "Unconfirmed trial did not expire");
        Assert(!CustomResolutionWorker.WaitForDecision(() => 0, () => true, () => false, () => true), "Cancel lost to Save");
        Assert(!CustomResolutionWorker.WaitForDecision(() => 0, () => false, () => true, () => true), "Parent exit did not cancel");
        Assert(CustomResolutionWorker.WaitForDecision(() => 0, () => false, () => false, () => true), "Timely confirmation rejected");
        elapsed = 14999; Assert(!CustomResolutionWorker.WaitForDecision(() => elapsed, () => false, () => false, () => { elapsed = 15001; return true; }), "Late Save accepted");
        Fails(() => new CustomResolutionRequest { Device = "one", Width = 0, Height = 1080, Refresh = 60 }.Validate());
        var working = new NvTiming { HVisible = 1920, VVisible = 1080, HTotal = 2080, VTotal = 1110, PixelClock = 71573, Extra = new NvTimingExtra { RefreshMilliHz = 310000 } };
        var oddWidth = new CustomResolutionRequest { Device = "one", Width = 1020, Height = 1080, Refresh = 310 };
        oddWidth.ValidateGpuScale(1920, 1080, working);
        Fails(() => new CustomResolutionRequest { Device = "one", Width = 2560, Height = 1440, Refresh = 310 }.ValidateGpuScale(1920, 1080, working));
        Fails(() => new CustomResolutionRequest { Device = "one", Width = 1020, Height = 1080, Refresh = 300 }.ValidateGpuScale(1920, 1080, working));
        Fails(() => oddWidth.ValidateGpuScale(1920, 1080, new NvTiming()));
        NvTiming changedSignal = working; changedSignal.PixelClock++;
        Assert(!NvidiaWorkingSignal.Same(working, changedSignal), "Changed pixel clock was accepted as the same monitor signal");
        changedSignal = working; changedSignal.HFrontPorch++;
        Assert(!NvidiaWorkingSignal.Same(working, changedSignal), "Changed blanking was accepted as the same signal");
        Assert(CustomResolutionWorker.Run(new[] { "--custom-resolution-trial" }) == 2, "Malformed worker arguments accepted");
        Console.WriteLine("PASS: native ABI sizes/offsets, explicit Save only, timeout, cancel precedence, parent exit, late confirmation rejection, failed begin/save/revert still restores layout, invalid input rejected. No live resolution trial.");
    }
    private static void CheckIdentity()
    {
        var epic = BackgroundAppBoost.Catalog.Single(item => item.Id == "epic-helper");
        Assert(!BackgroundAppBoost.CanManage(epic, new ProcessIdentity { Name = "EpicWebHelper", Path = @"C:\Epic\EpicWebHelper.exe", Product = "Epic Games", Company = "Epic Games", Ancestors = "VALORANT-Win64-Shipping.exe" }), "VALORANT helper appeared as Epic");
        Assert(!BackgroundAppBoost.CanManage(epic, new ProcessIdentity { Name = "EpicWebHelper" }), "Unidentified helper accepted");
        Assert(!BackgroundAppBoost.CanManage(epic, new ProcessIdentity { Name = "RiotClientUx", Path = @"C:\Riot Games\RiotClientUx.exe" }), "Riot misidentified");
        Assert(BackgroundAppBoost.CanManage(epic, new ProcessIdentity { Name = "EpicWebHelper", Path = @"C:\Epic Games\Launcher\EpicWebHelper.exe", Company = "Epic Games" }), "Verified standalone Epic helper rejected");
        var chatgpt = BackgroundAppBoost.Catalog.Single(item => item.Id == "chatgpt");
        Assert(!chatgpt.Recommended && BackgroundAppBoost.CanManage(chatgpt, new ProcessIdentity { Name = "ChatGPT", Path = @"C:\Program Files\WindowsApps\OpenAI.ChatGPT_1\ChatGPT.exe", Product = "ChatGPT", Company = "OpenAI" }), "Standalone ChatGPT unavailable");
        Assert(!BackgroundAppBoost.CanManage(chatgpt, new ProcessIdentity { Name = "ChatGPT", Path = @"C:\Program Files\WindowsApps\OpenAI.Codex_1\ChatGPT.exe", Product = "Codex", Company = "OpenAI" }), "Codex's ChatGPT.exe accepted");
        Assert(!BackgroundAppBoost.CanManage(chatgpt, new ProcessIdentity { Name = "ChatGPT" }), "Unverified ChatGPT accepted");
        var codex = BackgroundAppBoost.Catalog.Single(item => item.Id == "codex");
        Assert(!codex.Recommended && BackgroundAppBoost.CanManage(codex, new ProcessIdentity { Name = "ChatGPT", Path = @"C:\Program Files\WindowsApps\OpenAI.Codex_1\ChatGPT.exe", Product = "Codex", Company = "OpenAI" }), "Codex was still protected instead of explicitly selectable");
        Assert(!BackgroundAppBoost.CanManage(codex, new ProcessIdentity { Name = "ChatGPT", Path = @"C:\Program Files\WindowsApps\OpenAI.ChatGPT_1\ChatGPT.exe", Product = "ChatGPT", Company = "OpenAI" }), "Standalone ChatGPT misidentified as Codex");
        Assert(!BackgroundAppBoost.CanManage(codex, new ProcessIdentity { Name = "codex-code-mode-host", Path = @"C:\Riot Games\codex-code-mode-host.exe", Company = "Riot" }), "Codex exception bypassed Riot protections");
        string[] protectedNames = { "System", "MsMpEng", "RiotClientServices", "VALORANT-Win64-Shipping", "vgc", "nvcontainer", "Lucent" };
        Assert(protectedNames.All(BackgroundAppBoost.IsProtectedProcessName), "Protection regression");
        Assert(BackgroundAppBoost.Catalog.Length >= 45, "Expanded optional catalog missing");
        Assert(new BoostCandidate { MemoryBytes = 512 }.MemoryText == "512 B" && new BoostCandidate { MemoryBytes = 4096 }.MemoryText == "4 KB", "Tiny process memory display failed");
        Console.WriteLine("PASS: VALORANT-owned Epic helpers excluded, unknown helpers excluded, standalone ChatGPT/Codex separately selectable, Codex never recommended automatically, system/Riot/driver/Lucent protections retained.");
    }
    private static void CheckResolutionOnly()
    {
        var native = new DEVMODE { dmPelsWidth = 1920, dmPelsHeight = 1080, dmBitsPerPel = 32, dmDisplayFrequency = 300 };
        var current = new DisplayMode("one", native);
        native.dmPelsWidth = 1440; native.dmDisplayFrequency = 310; var overclock = new DisplayMode("one", native);
        native.dmDisplayFrequency = 300; var sameRate = new DisplayMode("one", native);
        Assert(ReferenceEquals(DisplayService.FindResolutionOnlyMode(new[] { overclock, sameRate }, 1440, 1080, current), sameRate), "Resolution switch chose stored/maximum Hz rather than active Hz");
        Assert(DisplayService.FindResolutionOnlyMode(new[] { overclock }, 1440, 1080, current) == null, "Resolution switch silently changed Hz");
        native.dmBitsPerPel = 24; var otherDepth = new DisplayMode("one", native);
        Assert(DisplayService.FindResolutionOnlyMode(new[] { otherDepth }, 1440, 1080, current) == null, "Resolution-only switch changed color depth");
        Console.WriteLine("PASS: pair chooses the current Hz/color depth, rejects unsupported same-Hz targets, and never falls back to an old overclock rate.");
    }
    private static void CheckLiveVibrance(string folder)
    {
        var store = new DesktopVibranceStore(Path.Combine(folder, "live")); var driver = new Driver(); var session = new DesktopVibranceSession(driver, store);
        session.Enable(new[] { "one", "two" }, 70); session.Adjust(80); session.Adjust(100);
        Assert(session.Enabled && driver.Values["one"] == 100 && driver.Values["two"] == 100, "Live slider adjustment failed");
        session.Restore(); Assert(driver.Values["one"] == 12 && driver.Values["two"] == -8, "Live update lost the original desktop baseline");
        session.Enable(new[] { "one", "two" }, 70); session.Adjust(80);
        var journal = store.LoadRecovery(); driver.Values["one"] = 40; // Simulate a crash between monitor writes.
        var recovered = new DesktopVibranceSession(driver, store); recovered.Recover(journal);
        Assert(driver.Values["one"] == 12 && driver.Values["two"] == -8, "Interrupted live update could not restore old/new monitor levels");
        session = new DesktopVibranceSession(driver, store); session.Enable(new[] { "one", "two" }, 70); session.Adjust(80); driver.Values["one"] = 40;
        Fails(() => session.Adjust(90)); Assert(driver.Values["one"] == 40 && driver.Values["two"] == -8 && !session.Enabled, "External color change to a prior level was overwritten");
        driver.Values["one"] = 12; session.Enable(new[] { "one", "two" }, 70); driver.FailWrite = "two";
        Fails(() => session.Adjust(80)); Assert(driver.Values["one"] == 12 && session.HasPendingRestore, "Failed live update lost partial restoration state");
        driver.FailWrite = ""; session.Restore(); Assert(driver.Values["two"] == -8, "Failed live update could not be recovered");
        session.Enable(new[] { "one", "two" }, 70); int writes = driver.Writes; session.Adjust(100, () => false); Assert(driver.Writes == writes && driver.Values["one"] == 40, "Stale live update ran after cancellation");
        using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
        {
            driver.BeforeWrite = (device, level) => { if (device == "one" && level == 60) { entered.Set(); if (!release.Wait(2000)) throw new InvalidOperationException("Test timed out"); } };
            Task change = Task.Factory.StartNew(() => session.Adjust(80)); Assert(entered.Wait(2000), "Live task did not reach driver");
            Task stop = Task.Factory.StartNew(() => session.Restore()); release.Set();
            Assert(Task.WaitAll(new[] { change, stop }, 3000) && !session.Enabled && driver.Values["one"] == 12 && driver.Values["two"] == -8, "Off raced a live update and left colors applied");
        }
        driver.BeforeWrite = null;
        Console.WriteLine("PASS: live vibrance updates preserve original baselines, interrupted/failed updates recover, external changes are kept, stale work cancels and Off serializes with an in-flight update.");
    }
    private static void CheckTheme()
    {
        Assert(Synthwave.DarkMode, "Monochrome theme must be permanent");
        using (var image = new Bitmap(200,120)) using (var graphics = Graphics.FromImage(image))
        {
            Synthwave.Draw(graphics,new Rectangle(0,0,200,120),0);
            int low = 255, high = 0;
            for (int y=0;y<image.Height;y++) for (int x=0;x<image.Width;x++) { Color p=image.GetPixel(x,y); Assert(p.R==p.G && p.G==p.B,"Backdrop contains color"); low=Math.Min(low,p.R); high=Math.Max(high,p.R); }
            Assert(high-low>30,"Sun/grid artwork was lost");
        }
        Console.WriteLine("PASS: permanent monochrome artwork with sun/grid contrast retained.");
    }
    private static void CheckPreferencesAndBindings(string folder)
    {
        string path=Path.Combine(folder,"settings.ini");
        SwitcherSettings settings=new SwitcherSettings {SwitchHotkey=0,CrosshairHotkey=0,BackgroundEnabled=false,BackgroundMotion=false,CometsEnabled=false,ModeBWidth=1720};
        settings.SaveTo(path); settings=SwitcherSettings.LoadFrom(path);
        Assert(settings.SwitchHotkey==0 && settings.CrosshairHotkey==0,"Unbound shortcuts reverted after reload");
        Assert(!settings.BackgroundEnabled && !settings.BackgroundMotion && !settings.CometsEnabled && settings.ModeBWidth==1720,"Appearance preferences or display preference roundtrip failed");
        settings.SwitchHotkey=(int)(Keys.Control|Keys.Alt|Keys.F8); settings.BackgroundEnabled=true; settings.SaveTo(path);
        Assert(SwitcherSettings.LoadFrom(path).SwitchHotkey==settings.SwitchHotkey && SwitcherSettings.LoadFrom(path).BackgroundEnabled,"Rebinding/re-enabling failed persistence");
        using(HotkeyWindow first=new HotkeyWindow(()=>{})) using(HotkeyWindow second=new HotkeyWindow(()=>{}))
        {
            Keys available=Keys.None;
            foreach(Keys key in new[]{Keys.F24,Keys.F23,Keys.F22,Keys.F21}) { Keys candidate=Keys.Control|Keys.Alt|Keys.Shift|key; if(first.Register(candidate)) {available=candidate;break;} }
            Assert(available!=Keys.None,"No spare key available for native unregister check");
            Assert(!second.Register(available),"Conflict check accepted an occupied shortcut");
            Assert(first.Register(Keys.None) && second.Register(available),"Unbind did not release the OS shortcut");
            Assert(second.Register(Keys.None) && first.Register(available),"Shortcut could not rebind after Unbind");
        }
        Console.WriteLine("PASS: appearance and unbound binds persist; native shortcut conflicts rejected, Unbind releases registration, and rebind succeeds. No key presses sent.");
    }
    private static void CheckRefreshOnly()
    {
        var immediate=new RateTrial();var applied=RefreshRateTransaction.ApplyImmediately(immediate);
        Assert(applied.Kept && immediate.Applies==1 && immediate.Restores==0,"Immediate Hz apply did not commit without a confirmation");
        var failure=new RateTrial {Fail=true};applied=RefreshRateTransaction.ApplyImmediately(failure);
        Assert(!applied.Kept && failure.Restores==1 && applied.Error.Length>0,"Immediate Hz failure did not restore");
        var canceled = new RateTrial(); var outcome = RefreshRateTransaction.Run(canceled, () => false);
        Assert(!outcome.Kept && canceled.Applies == 1 && canceled.Restores == 1, "Hz-only cancellation did not restore");
        var kept = new RateTrial(); outcome = RefreshRateTransaction.Run(kept, () => true);
        Assert(outcome.Kept && kept.Restores == 0, "Explicit Keep did not retain the refresh rate");
        var failed = new RateTrial { Fail = true }; bool askedToKeep = false;
        outcome = RefreshRateTransaction.Run(failed, () => { askedToKeep = true; return true; });
        Assert(!outcome.Kept && !askedToKeep && failed.Restores == 1 && outcome.Error.Length > 0, "Failed Hz-only apply could be kept");
        var before = new[] { new RefreshDesktopState { Device = "one", Width = 1920, Height = 1080, Hertz = 310, Depth = 32 }, new RefreshDesktopState { Device = "two", Width = 1080, Height = 1920, Hertz = 60, X = 1920, Orientation = 1, Depth = 32 } };
        var after = new[] { new RefreshDesktopState { Device = "one", Width = 1920, Height = 1080, Hertz = 300, Depth = 32 }, new RefreshDesktopState { Device = "two", Width = 1080, Height = 1920, Hertz = 60, X = 1920, Orientation = 1, Depth = 32 } };
        Assert(RefreshDesktopState.OnlyRefreshChanged(before, after, "one", 300), "Valid refresh-only change rejected");
        after[0].Width = 1440; Assert(!RefreshDesktopState.OnlyRefreshChanged(before, after, "one", 300), "Hz-only change altered resolution"); after[0].Width = 1920;
        after[1].Hertz = 59; Assert(!RefreshDesktopState.OnlyRefreshChanged(before, after, "one", 300), "Hz-only change altered another monitor"); after[1].Hertz = 60;
        after[1].X = 1800; Assert(!RefreshDesktopState.OnlyRefreshChanged(before, after, "one", 300), "Hz-only change altered layout");
        Assert(RefreshRateWorker.Run(new[] { "--refresh-only-trial" }) == 2, "Invalid refresh worker input accepted");
        Console.WriteLine("PASS: immediate Hz commit/failure rollback and legacy Keep/cancel/error flow; resolution, color depth, rotation, positions and other monitors guarded. No live refresh change.");
    }
    private static void CheckCrosshair()
    {
        using (Bitmap image = new Bitmap(201, 201))
        {
            using (Graphics graphics = Graphics.FromImage(image)) { graphics.Clear(Color.Magenta); CrosshairArt.Draw(graphics, new Rectangle(0, 0, 201, 201), "positted", Color.White, .6f, false); }
            int count = 0;
            for (int y = 0; y < 201; y++) for (int x = 0; x < 201; x++)
            {
                bool expected = ((x == 99 || x == 100) && ((y >= 96 && y <= 98) || (y >= 101 && y <= 103))) || ((y == 99 || y == 100) && ((x >= 96 && x <= 98) || (x >= 101 && x <= 103)));
                bool white = image.GetPixel(x, y).ToArgb() == Color.White.ToArgb();
                Assert(white == expected, "positted reference pixel mismatch"); if (white) count++;
            }
            Assert(count == 24, "Unexpected crosshair area");
        }
        using (SwapControl swap = new SwapControl())
        {
            int clicks = 0; swap.Click += (s, e) => clicks++;
            typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(swap, new object[] { new KeyEventArgs(Keys.Enter) });
            Assert(clicks == 1 && swap.TabStop && swap.AccessibleRole == AccessibleRole.PushButton, "Swap keyboard/accessibility failed");
        }
        Console.WriteLine("PASS: positted exactly matches 8x8 reference geometry at 0.6x (24 pixels, 2x2 gap); swap keyboard event and accessibility.");
    }
    private static void Probe()
    {
        ProcessTree.Refresh();
        foreach (Process process in Process.GetProcessesByName("EpicWebHelper")) using (process) Console.WriteLine(ProcessIdentity.Read(process).Evidence);
        var engine = new BackgroundAppBoost(); var candidates = engine.DetectCandidates();
        foreach (BoostCandidate candidate in candidates) Console.WriteLine("SCAN ONLY: " + candidate.Definition.DisplayName + " [" + candidate.Definition.Id + "]");
        using (NvidiaVibranceDriver driver = new NvidiaVibranceDriver())
            foreach (Screen screen in Screen.AllScreens)
            {
                var level = driver.Read(screen.DeviceName); Console.WriteLine("READ ONLY DVC: " + screen.DeviceName + " current=" + level.Current);
                using (var scaling = new NvidiaScalingConfig(screen.DeviceName)) Console.WriteLine("READ ONLY NVIDIA SCALING: " + screen.DeviceName + " current=" + scaling.CurrentScaling);
                var current = DisplayService.GetCurrentMode(screen.DeviceName);
                int workingHz = (int)Math.Round(NvidiaWorkingSignal.Hertz(NvidiaWorkingSignal.Read(screen.DeviceName)));
                try { using (var trial = new NvidiaCustomResolutionTrial(new CustomResolutionRequest { Device = screen.DeviceName, Width = current.Width, Height = current.Height, Refresh = workingHz })) Console.WriteLine("READ ONLY TIMING: " + trial.TimingSummary); }
                catch (InvalidOperationException error) { Console.WriteLine("READ ONLY TIMING NOT AVAILABLE: " + screen.DeviceName + " " + current.Width + "x" + current.Height + " @ " + current.Frequency + ": " + error.Message); }
                if (screen.Primary) try { using (var trial = new NvidiaCustomResolutionTrial(new CustomResolutionRequest { Device = screen.DeviceName, Width = 1720, Height = 1080, Refresh = workingHz })) Console.WriteLine("READ ONLY CUSTOM CANDIDATE: " + trial.TimingSummary); } catch (InvalidOperationException error) { Console.WriteLine("READ ONLY CUSTOM CANDIDATE NOT AVAILABLE: " + error.Message); }
                if (screen.Primary) try { using (var trial = new NvidiaCustomResolutionTrial(new CustomResolutionRequest { Device = screen.DeviceName, Width = 1020, Height = 1080, Refresh = workingHz })) Console.WriteLine("READ ONLY 1020-WIDTH CANDIDATE: " + trial.TimingSummary); } catch (InvalidOperationException error) { Console.WriteLine("READ ONLY 1020-WIDTH CANDIDATE NOT AVAILABLE: " + error.Message); }
            }
        DisplayLayoutSnapshot.Capture(); Console.WriteLine("READ ONLY: full display layout captured. No trial, save, revert or layout apply was invoked.");
    }
    private static void CheckUi(bool screenshots,DesktopVibranceStore uiStore)
    {
        Application.EnableVisualStyles();
        using(StartupOverlay intro=new StartupOverlay {Size=new Size(720,466)})
        {
            Bitmap destination=new Bitmap(720,466);using(Graphics graphics=Graphics.FromImage(destination))graphics.Clear(Color.FromArgb(42,42,42));
            intro.PrepareReveal(destination);intro.Start();
            Assert(!((Stopwatch)typeof(StartupOverlay).GetField("bootClock",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(intro)).IsRunning,"Intro clock started before its first paint");
            using(Bitmap finalFrame=new Bitmap(720,466))
            {
                using(Graphics graphics=Graphics.FromImage(finalFrame))intro.Render(graphics,StartupOverlay.Duration);
                foreach(Point point in new[]{new Point(10,10),new Point(350,220),new Point(690,440)})Assert(finalFrame.GetPixel(point.X,point.Y).ToArgb()==Color.FromArgb(42,42,42).ToArgb(),"Intro did not finish on the fully prepared app frame");
            }
        }
        using (MainForm form = new MainForm(uiStore))
        {
            form.Show();
            if(screenshots)
            {
                var intro=(StartupOverlay)typeof(MainForm).GetField("startupOverlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                foreach(float seconds in new[]{.6f,1.23f})
                using(Bitmap frame=new Bitmap(form.ClientSize.Width,form.ClientSize.Height))
                {
                    using(Graphics graphics=Graphics.FromImage(frame))intro.Render(graphics,seconds);
                    string folder=Path.Combine(Environment.CurrentDirectory,"..","previews");Directory.CreateDirectory(folder);
                    frame.Save(Path.Combine(folder,seconds<1?"Startup.png":"Startup-reveal.png"));
                }
            }
            // Freeze only this test host while capturing. Rendering every scene
            // frame can keep Application.DoEvents busy on a loaded desktop.
            ((System.Windows.Forms.Timer)typeof(MainForm).GetField("sceneTimer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form)).Stop();
            Pump(1750);
            Assert(typeof(MainForm).GetField("startupOverlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form)==null,"Intro did not complete its handoff");
            var tabs = form.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<MiamiTab>()).ToArray();
            Assert(tabs.Length == 3 && tabs.Any(tab => tab.Text == "Video") && !tabs.Any(tab => tab.Text == "Vibrance"), "Video navigation missing");
            var formKeyDown=typeof(MainForm).GetMethod("OnFormKeyDown",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var tab in tabs)
            {
                typeof(Control).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(tab,new object[]{EventArgs.Empty});
                var escape=new KeyEventArgs(Keys.Escape);formKeyDown.Invoke(form,new object[]{form,escape});
                Assert(!form.IsDisposed && form.Visible && escape.SuppressKeyPress,"Escape closed or hid the main app from "+tab.Text);
            }
            typeof(Control).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(tabs.Single(tab=>tab.Text=="Video"),new object[]{EventArgs.Empty});
            var page = form.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<VibrancePageControl>()).Single();
            Assert(page.Visible && page.Bottom <= 400, "Vibrance not in Video page");
            Assert(!form.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<Label>()).Any(label => label.Visible && label.Text == "REFRESH ONLY"), "Refresh only heading remains");
            var pairA = (DarkDropDown)typeof(MainForm).GetField("modeARefreshPicker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
            var pairB = (DarkDropDown)typeof(MainForm).GetField("modeBRefreshPicker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
            Assert(!pairA.Visible && !pairB.Visible, "Pair still exposes per-resolution Hz pickers");
            var percentLabel = (Label)typeof(VibrancePageControl).GetField("value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page);
            Assert(percentLabel.Parent.Controls.OfType<AccentButton>().Count() == 1, "Redundant vibrance button returned");
            Assert(!percentLabel.Parent.Controls.Cast<Control>().Any(control => control != percentLabel && control.Visible && control.Bounds.IntersectsWith(percentLabel.Bounds)), "Vibrance percentage overlaps another control");
            var session = (DesktopVibranceSession)typeof(VibrancePageControl).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page);
            Assert(session == null || (!session.Enabled && !session.HasPendingRestore), "Opening Video applied colors");
            var liveSlider=(VibranceSlider)typeof(VibrancePageControl).GetField("slider",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(page);
            int savedPercent=liveSlider.Value;
            var precisionField=typeof(MainForm).GetField("precisionTimer",BindingFlags.NonPublic|BindingFlags.Instance);
            var animationField=typeof(MainForm).GetField("sceneTimer",BindingFlags.NonPublic|BindingFlags.Instance);
            typeof(VibranceSlider).GetMethod("HoldInteraction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(liveSlider,null);
            typeof(VibranceSlider).GetMethod("MoveThumb",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(liveSlider,new object[]{143});
            Assert((bool)precisionField.GetValue(form) && !((System.Windows.Forms.Timer)animationField.GetValue(form)).Enabled,"Slider animation must retain precise timing without repainting the whole scene");
            float pointerPercent=(float)((143d-10)/Math.Max(1,liveSlider.Width-20)*100);
            Stopwatch settling=Stopwatch.StartNew();while(Math.Abs(liveSlider.VisualPercent-pointerPercent)>=.1f && settling.ElapsedMilliseconds<600)Pump(20);
            Assert(Math.Abs(liveSlider.VisualPercent-pointerPercent)<.1f,"Visible slider did not settle at the fractional pointer position");
            liveSlider.Value=savedPercent;Pump(300);
            ((System.Windows.Forms.Timer)animationField.GetValue(form)).Stop();
            Assert(((HashSet<object>)typeof(MainForm).GetField("sceneInteractions",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form)).Count==0,"Slider retained an interaction after settling");
            var ratePicker = (DarkDropDown)typeof(MainForm).GetField("currentRefreshPicker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
            var actual = DisplayService.GetDisplays().First(display => display.IsPrimary);
            var actualMode = DisplayService.GetCurrentMode(actual.DeviceName);
            int[] allowedRates = DisplayService.GetModes(actual.DeviceName).Where(mode => mode.Width == actualMode.Width && mode.Height == actualMode.Height && mode.BitsPerPixel == actualMode.BitsPerPixel).Select(mode => mode.Frequency).ToArray();
            Assert(ratePicker.Items.OfType<RefreshChoice>().All(choice => allowedRates.Contains(choice.Hertz)), "Hz-only picker contains a rate from another resolution");
            var pendingRate = ratePicker.Items.OfType<RefreshChoice>().FirstOrDefault(choice => choice.Hertz != actualMode.Frequency);
            if (pendingRate != null) { ratePicker.SelectedItem = pendingRate; Pump(1900); Assert(ReferenceEquals(ratePicker.SelectedItem, pendingRate), "Polling overwrote the pending Hz choice"); }
            ratePicker.SelectedItem = ratePicker.Items.OfType<RefreshChoice>().FirstOrDefault(choice => choice.Hertz == actualMode.Frequency);
            Assert(typeof(MainForm).GetField("themeButton",BindingFlags.NonPublic|BindingFlags.Instance)==null,"Theme toggle remains");
            Assert(form.Controls.OfType<Panel>().SelectMany(panel=>panel.Controls.OfType<BrandWordmark>()).Count()==1,"Header wordmark missing");
            if (screenshots) SaveImage(form,"Video-Dark.png");
            DarkDropDown resolutionPicker=(DarkDropDown)typeof(MainForm).GetField("modeAPicker",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
            typeof(DarkDropDown).GetMethod("ShowOptions",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(resolutionPicker,null);Pump(50);
            var modernPopup=(ModePickerPopup)typeof(DarkDropDown).GetField("popupMenu",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(resolutionPicker);
            Assert(modernPopup.Visible,"Buffered mode picker did not open");
            Assert((bool)precisionField.GetValue(form) && !((System.Windows.Forms.Timer)animationField.GetValue(form)).Enabled,"Mode picker lost precise timing or resumed the background");
            if(screenshots)SaveImage(modernPopup,"Resolution-picker.png");if(!modernPopup.IsDisposed && modernPopup.Visible)modernPopup.Close();
            ContextMenuStrip menu=(ContextMenuStrip)typeof(MainForm).GetField("trayMenu",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
            Assert(menu is LucentTrayMenu && menu.Items.OfType<ToolStripMenuItem>().Any(item=>item.Text=="Settings"),"Styled tray/settings menu missing");
            menu.Show(form,new Point(428,50)); Pump(100); if(screenshots) SaveImage(menu,"Tray-menu.png"); menu.Close();
            menu.Items.OfType<ToolStripMenuItem>().Single(item=>item.Text=="Boost").PerformClick();
            var boostTab = tabs.Single(tab => tab.Text == "Boost"); typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boostTab, new object[] { EventArgs.Empty }); Pump(150);
            var boost = form.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<BoostPageControl>()).Single();
            var list = (FlowLayoutPanel)typeof(BoostPageControl).GetField("candidateList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boost);
            Assert(list.Controls.OfType<BoostAppRow>().All(row => !row.SelectedForBoost), "Boost selected automatically");
            if (screenshots) SaveImage(form, "Boost-Dark.png");
            var crosshairTab = tabs.Single(tab => tab.Text == "Crosshair"); typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(crosshairTab, new object[] { EventArgs.Empty }); Pump(80);
            if (screenshots) SaveImage(form, "Crosshair-Dark.png");
            var popupClock=Stopwatch.StartNew();typeof(MainForm).GetMethod("OpenSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);form.Update();
            Console.WriteLine("UI TIMING: prepared Settings open + paint "+popupClock.ElapsedMilliseconds+" ms; font="+Synthwave.FontFamily);Pump(100);
            SettingsPageControl settingsView=(SettingsPageControl)typeof(MainForm).GetField("settingsControl",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
            Assert(settingsView.Visible && form.OwnedForms.Length==0,"Settings must stay inside the main window");
            Assert(settingsView.Parent.Parent.Bounds==form.ClientRectangle,"Popup backdrop must cover the whole client area");
            if(screenshots) SaveImage(settingsView.Parent.Parent,"Settings-popup.png");
            settingsView.BeginBinding(false); Assert(settingsView.IsCapturing,"Inline capture did not start");
            if(screenshots) SaveImage(settingsView.Parent.Parent,"Settings-capture.png");
            var cancelCapture=new KeyEventArgs(Keys.Escape);formKeyDown.Invoke(form,new object[]{form,cancelCapture});
            Assert(!settingsView.IsCapturing && settingsView.Visible && cancelCapture.SuppressKeyPress,"Escape must cancel binding without closing Settings");
            var dismissSettings=new KeyEventArgs(Keys.Escape);formKeyDown.Invoke(form,new object[]{form,dismissSettings});
            Assert(!settingsView.Visible && crosshairTab.Selected && !form.IsDisposed && form.Visible && dismissSettings.SuppressKeyPress,"Escape must close only Settings and restore the previous page");
            Assert(!settingsView.IsDisposed,"Prepared Settings surface was discarded");
            typeof(MainForm).GetMethod("OpenSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
            typeof(MainForm).GetMethod("OpenSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
            Assert(!settingsView.Visible && crosshairTab.Selected,"Second gear click did not restore the previous page");
            typeof(MainForm).GetMethod("OpenSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
            SwitcherSettings preferences=(SwitcherSettings)typeof(MainForm).GetField("settings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
            bool previousBackground=preferences.BackgroundEnabled, previousMotion=preferences.BackgroundMotion;
            var appearance=typeof(MainForm).GetMethod("ApplyAppearance",BindingFlags.NonPublic|BindingFlags.Instance);
            var scene=(System.Windows.Forms.Timer)typeof(MainForm).GetField("sceneTimer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
            try
            {
                preferences.BackgroundEnabled=false; appearance.Invoke(form,null); Pump(30);
                Assert(!scene.Enabled,"Scene timer still wakes with background disabled");
                if(screenshots) SaveImage(form,"Background-off.png");
                preferences.BackgroundEnabled=true; preferences.BackgroundMotion=false; appearance.Invoke(form,null);
                Assert(!scene.Enabled,"Scene timer still wakes with motion disabled");
            }
            finally {preferences.BackgroundEnabled=previousBackground; preferences.BackgroundMotion=previousMotion; appearance.Invoke(form,null);}
            form.Close();
        }
        using (CustomResolutionDialog dialog = new CustomResolutionDialog()) { dialog.Show(); Pump(100); Assert(dialog.BackColor.GetBrightness() > .5f && dialog.ForeColor.GetBrightness() < .5f, "Custom resolution dialog should retain light Windows styling"); if (screenshots) SaveImage(dialog, "Custom-resolution.png"); dialog.Close(); }
        using(InlineBinding field=new InlineBinding())
        {
            Keys saved=Keys.F10;field.Hotkey=saved;field.TryBind=key=>{saved=key;return true;};
            field.BeginCapture();typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(field,new object[]{new KeyEventArgs(Keys.Control|Keys.Alt|Keys.F8)});
            Assert(saved==(Keys.Control|Keys.Alt|Keys.F8) && !field.Capturing,"Inline bind failed");
            field.ClearBinding();Assert(saved==Keys.None && field.Hotkey==Keys.None,"Inline Unbind failed");
            field.TryBind=key=>false;field.BeginCapture();typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(field,new object[]{new KeyEventArgs(Keys.F9)});
            Assert(field.Hotkey==Keys.None,"Unavailable bind replaced the previous key");field.CancelCapture();
        }
        object chosen=null;bool dismissed=false;
        using(ModePickerList picker=new ModePickerList(new object[]{"one","two","three"},"one",null,null,item=>chosen=item,()=>dismissed=true))
        {
            picker.Size=new Size(240,122);
            typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{new KeyEventArgs(Keys.Down)});
            typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{new KeyEventArgs(Keys.Enter)});
            Assert(Convert.ToString(chosen)=="two" && dismissed,"Mode picker keyboard selection failed");
        }
        Console.WriteLine("PASS: prepared intro frame, first-paint clock and completed handoff; Escape keeps all three main tabs open, cancels binding capture first, then dismisses only Settings; other UI regression checks and clean explicit close.");
    }
    private static void CheckVibranceLayout(DesktopVibranceStore store, string folder)
    {
        string preferencePath = Path.Combine(folder, "desktop-vibrance.ini");
        string previousPreference = File.ReadAllText(preferencePath);
        using (var page = new VibrancePageControl(store))
        {
            var label = (Label)typeof(VibrancePageControl).GetField("value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page);
            var slider = (VibranceSlider)typeof(VibrancePageControl).GetField("slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page);
            Assert(label.Parent.Controls.OfType<AccentButton>().Count() == 1, "Extra vibrance button remains");
            Assert(!label.Parent.Controls.Cast<Control>().Any(control => control != label && control.Bounds.IntersectsWith(label.Bounds)), "Percentage overlaps another control");
            foreach (int percent in new[] { 0, 1, 70, 99, 100 })
            {
                slider.Value = percent;
                Assert(label.Text == percent + "%" && TextRenderer.MeasureText(label.Text, label.Font).Width + 8 <= label.Width, "Percentage does not fit its display");
            }
            Assert(File.ReadAllText(preferencePath) == previousPreference, "Rapid slider changes wrote preferences synchronously");
            typeof(VibranceSlider).GetMethod("MoveThumb", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(slider, new object[] { 143 });
            Assert(Math.Abs(slider.VisualPercent - Math.Round(slider.VisualPercent)) > .01, "Thumb is still snapped to integer percentages");
            using (Bitmap image = new Bitmap(slider.Width, slider.Height))
            {
                slider.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                int cyanPixels = 0;
                for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++) { Color pixel = image.GetPixel(x, y); if (pixel.G > 200 && pixel.B > 200 && pixel.R < 100) cyanPixels++; }
                Assert(cyanPixels == 0, "Cyan outline is still drawn by the slider");
            }
            slider.Value = 100;
        }
        Assert(File.ReadAllText(preferencePath).Trim() == "100", "Final slider preference was not flushed on close");
        Console.WriteLine("PASS: continuous thumb position, no cyan outline, no synchronous preference writes during rapid changes, final preference flushed, readable 0–100% and one toggle.");
    }
    private static void SaveImage(Control form, string name)
    {
        string folder = Path.Combine(Environment.CurrentDirectory, "..", "previews"); Directory.CreateDirectory(folder);
        using (Bitmap image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height)); image.Save(Path.Combine(folder, name)); }
    }
    private static void Pump(int ms) { Stopwatch timer = Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
}
