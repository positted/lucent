using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    // Native ABI follows NVIDIA/nvapi's official nvapi.h and nvapi_interface.h.
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NvTimingExtra
    {
        public uint Flags; public ushort Refresh; public uint RefreshMilliHz, Aspect;
        public ushort Repetition; public uint Status;
        public uint Name0, Name1, Name2, Name3, Name4, Name5, Name6, Name7, Name8, Name9;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NvTiming
    {
        public ushort HVisible, HBorder, HFrontPorch, HSyncWidth, HTotal; public byte HSyncPolarity;
        public ushort VVisible, VBorder, VFrontPorch, VSyncWidth, VTotal; public byte VSyncPolarity;
        public ushort Interlaced; public uint PixelClock; public NvTimingExtra Extra;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NvTimingInput
    {
        // NV_TIMING_FLAG contains three DWORD storage units: the bitfields
        // before its anonymous union, the union, then the scaling bitfield.
        public uint Version, Width, Height; public float Refresh; public uint Flags, FormatFlags, Scaling, Type;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NvCustomMode
    {
        public uint Version, Width, Height, Depth, Format;
        public float PartitionX, PartitionY, PartitionWidth, PartitionHeight, XRatio, YRatio;
        public NvTiming Timing; public uint HardwareOnly;
    }
    internal sealed class CustomResolutionRequest
    {
        public string Device;
        public int Width, Height, Refresh;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Device) || Width < 320 || Width > 16384 || Height < 200 || Height > 16384 || Refresh < 23 || Refresh > 1000)
                throw new InvalidOperationException("Use width 320–16384, height 200–16384 and refresh 23–1000 Hz. The driver must accept the mode.");
        }
        internal void ValidateGpuScale(int maximumWidth, int maximumHeight, NvTiming signal)
        {
            Validate();
            if (!NvidiaWorkingSignal.Valid(signal)) throw new InvalidOperationException("A known-working monitor signal could not be read. No display test will run.");
            if (Width > maximumWidth || Height > maximumHeight) throw new InvalidOperationException("GPU-scaled sizes must fit within " + maximumWidth + " × " + maximumHeight + ". This creator does not overclock or enlarge the monitor signal.");
            if (Math.Abs(Refresh - NvidiaWorkingSignal.Hertz(signal)) > .6) throw new InvalidOperationException("Use the current signal refresh rate, " + Math.Round(NvidiaWorkingSignal.Hertz(signal)) + " Hz. This creator keeps the monitor timing unchanged.");
        }
    }
    internal static class NvidiaWorkingSignal
    {
        public static bool Valid(NvTiming signal) { return signal.HVisible > 0 && signal.VVisible > 0 && signal.HTotal >= signal.HVisible && signal.VTotal >= signal.VVisible && signal.PixelClock > 0 && signal.Interlaced == 0; }
        public static double Hertz(NvTiming signal) { return signal.Extra.RefreshMilliHz > 0 ? signal.Extra.RefreshMilliHz / 1000d : signal.PixelClock * 10000d / ((double)signal.HTotal * signal.VTotal); }
        public static NvTiming Read(string device)
        {
            using (NvidiaScalingConfig config = new NvidiaScalingConfig(device))
            {
                if (config.SharesSource) throw new InvalidOperationException("This display shares a cloned desktop. A custom test could affect multiple monitors, so it is unavailable in clone mode.");
                NvTiming signal = config.CurrentSignalTiming;
                if (!Valid(signal)) throw new InvalidOperationException("The driver did not return the current working signal. No new signal timings will be generated.");
                return signal;
            }
        }
        internal static bool Same(NvTiming a, NvTiming b)
        {
            return a.PixelClock == b.PixelClock && a.HVisible == b.HVisible && a.HBorder == b.HBorder && a.HFrontPorch == b.HFrontPorch && a.HSyncWidth == b.HSyncWidth && a.HTotal == b.HTotal && a.HSyncPolarity == b.HSyncPolarity
                && a.VVisible == b.VVisible && a.VBorder == b.VBorder && a.VFrontPorch == b.VFrontPorch && a.VSyncWidth == b.VSyncWidth && a.VTotal == b.VTotal && a.VSyncPolarity == b.VSyncPolarity && a.Interlaced == b.Interlaced && a.Extra.Flags == b.Extra.Flags && a.Extra.Repetition == b.Extra.Repetition;
        }
    }
    internal sealed class CustomNvApi : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Query(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Lifecycle();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr library, string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr library);
        private IntPtr library; private Query query; private Lifecycle unload; private bool initialized;
        public CustomNvApi()
        {
            try
            {
                library = LoadLibraryEx(Path.Combine(Environment.SystemDirectory, IntPtr.Size == 8 ? "nvapi64.dll" : "nvapi.dll"), IntPtr.Zero, 0x800);
                if (library == IntPtr.Zero) throw new InvalidOperationException("Custom resolution creation requires an NVIDIA-connected display.");
                IntPtr pointer = GetProcAddress(library, "nvapi_QueryInterface");
                if (pointer == IntPtr.Zero) throw new InvalidOperationException("NVIDIA's display API is unavailable.");
                query = (Query)Marshal.GetDelegateForFunctionPointer(pointer, typeof(Query));
                Lifecycle init = Bind<Lifecycle>(0x0150E828); unload = Bind<Lifecycle>(0xD22BDD7E);
                Check(init()); initialized = true;
            }
            catch { Dispose(); throw; }
        }
        public T Bind<T>(uint id) where T : class
        {
            IntPtr pointer = query(id);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("The driver does not support custom-resolution creation.");
            return Marshal.GetDelegateForFunctionPointer(pointer, typeof(T)) as T;
        }
        public static void Check(int status) { if (status != 0) throw new InvalidOperationException("NVIDIA rejected the request (" + status + "). Your monitor or driver may not support this mode."); }
        public void Dispose() { if (initialized) { unload(); initialized = false; } if (library != IntPtr.Zero) { FreeLibrary(library); library = IntPtr.Zero; } }
    }
    internal sealed class DisplayLayoutSnapshot
    {
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathBuffer, ref uint modes, IntPtr modeBuffer, IntPtr topology);
        [DllImport("user32.dll")] private static extern int SetDisplayConfig(uint paths, IntPtr pathBuffer, uint modes, IntPtr modeBuffer, uint flags);
        private byte[] paths, modes; private uint pathCount, modeCount;
        public static DisplayLayoutSnapshot Capture()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint pc, mc; int result = GetDisplayConfigBufferSizes(0x12, out pc, out mc);
                if (result != 0 || pc == 0 || pc > 64 || mc > 256) throw new InvalidOperationException("Could not capture the current display layout (" + result + ").");
                IntPtr pb = Marshal.AllocHGlobal(checked((int)pc * 72)), mb = Marshal.AllocHGlobal(checked((int)mc * 64));
                try
                {
                    result = QueryDisplayConfig(0x12, ref pc, pb, ref mc, mb, IntPtr.Zero);
                    if (result == 122) continue;
                    if (result != 0) throw new InvalidOperationException("Could not read the current display layout (" + result + ").");
                    DisplayLayoutSnapshot snapshot = new DisplayLayoutSnapshot { pathCount = pc, modeCount = mc, paths = new byte[pc * 72], modes = new byte[mc * 64] };
                    Marshal.Copy(pb, snapshot.paths, 0, snapshot.paths.Length); Marshal.Copy(mb, snapshot.modes, 0, snapshot.modes.Length); return snapshot;
                }
                finally { Marshal.FreeHGlobal(pb); Marshal.FreeHGlobal(mb); }
            }
            throw new InvalidOperationException("The display layout is changing. Retry after it settles.");
        }
        public void Restore()
        {
            IntPtr pb = Marshal.AllocHGlobal(paths.Length), mb = Marshal.AllocHGlobal(modes.Length);
            try
            {
                Marshal.Copy(paths, 0, pb, paths.Length); Marshal.Copy(modes, 0, mb, modes.Length);
                int result = SetDisplayConfig(pathCount, pb, modeCount, mb, 0x8060); // Validate supplied, virtual-aware configuration.
                if (result == 0) result = SetDisplayConfig(pathCount, pb, modeCount, mb, 0x80A0); // Apply, without saving a new Windows layout.
                if (result != 0) throw new InvalidOperationException("Windows could not restore the original monitor layout (" + result + "). Check Windows Display settings.");
            }
            finally { Marshal.FreeHGlobal(pb); Marshal.FreeHGlobal(mb); }
        }
    }
    internal interface ICustomResolutionTrial
    {
        void Begin(); void Save(); void Revert(); void RestoreLayout();
    }
    internal sealed class CustomResolutionOutcome
    {
        public bool Saved;
        public string Error = "";
    }
    internal static class CustomResolutionTransaction
    {
        public static CustomResolutionOutcome Run(ICustomResolutionTrial trial, Func<bool> confirm)
        {
            CustomResolutionOutcome outcome = new CustomResolutionOutcome();
            try { trial.Begin(); if (confirm()) { trial.Save(); outcome.Saved = true; } }
            catch (Exception error) { outcome.Error = error.Message; }
            finally
            {
                try { trial.Revert(); } catch (Exception error) { outcome.Error += " Revert: " + error.Message; }
                try { trial.RestoreLayout(); } catch (Exception error) { outcome.Error += " Layout: " + error.Message; }
            }
            return outcome;
        }
    }
    internal sealed class NvidiaCustomResolutionTrial : ICustomResolutionTrial, IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DisplayId([MarshalAs(UnmanagedType.LPStr)] string device, out uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TryMode([In] uint[] ids, uint count, [In] NvCustomMode[] modes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SaveMode([In] uint[] ids, uint count, uint outputOnly, uint monitorOnly);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RevertMode([In] uint[] ids, uint count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EnumMode(uint id, uint index, ref NvCustomMode mode);
        private readonly CustomNvApi api;
        private readonly uint[] ids;
        private readonly NvCustomMode mode;
        private readonly TryMode tryMode; private readonly SaveMode saveMode; private readonly RevertMode revertMode;
        private DisplayLayoutSnapshot layout; private bool attempted, saved;
        private readonly CustomResolutionRequest request;
        public NvidiaCustomResolutionTrial(CustomResolutionRequest request)
        {
            request.Validate(); this.request = request; api = new CustomNvApi();
            try
            {
                uint id; CustomNvApi.Check(api.Bind<DisplayId>(0xAE457190)(request.Device, out id)); ids = new[] { id };
                tryMode = api.Bind<TryMode>(0x1F7DB630); saveMode = api.Bind<SaveMode>(0x49882876); revertMode = api.Bind<RevertMode>(0xCBBD40F0);
                NvCustomMode capability = new NvCustomMode { Version = (uint)Marshal.SizeOf(typeof(NvCustomMode)) | 0x10000u };
                int enumeration = api.Bind<EnumMode>(0xA2072D59)(id, 0, ref capability);
                if (enumeration != -7) CustomNvApi.Check(enumeration); // -7 = no custom modes saved yet.
                DisplayMode current = DisplayService.GetCurrentMode(request.Device);
                if (current == null) throw new InvalidOperationException("The active desktop mode is unavailable.");
                NvTiming timing = NvidiaWorkingSignal.Read(request.Device);
                bool portrait = current.NativeMode.dmDisplayOrientation == 1 || current.NativeMode.dmDisplayOrientation == 3;
                request.ValidateGpuScale(portrait ? timing.VVisible : timing.HVisible, portrait ? timing.HVisible : timing.VVisible, timing);
                // Change only the GPU's source dimensions. Copy the complete
                // current raster signal, including pixel clock and blanking;
                // AUTO/CVT calculations are deliberately not used here.
                mode = new NvCustomMode { Version = (uint)Marshal.SizeOf(typeof(NvCustomMode)) | 0x10000u, Width = (uint)request.Width, Height = (uint)request.Height, Depth = 32, Format = 0, PartitionWidth = 1, PartitionHeight = 1, XRatio = 1, YRatio = 1, Timing = timing };
            }
            catch { api.Dispose(); throw; }
        }
        public string TimingSummary { get { return "desktop " + mode.Width + " × " + mode.Height + " · signal " + mode.Timing.HVisible + " × " + mode.Timing.VVisible + " · " + mode.Timing.Extra.RefreshMilliHz / 1000d + " Hz"; } }
        public void Begin()
        {
            if (!NvidiaWorkingSignal.Same(mode.Timing, NvidiaWorkingSignal.Read(request.Device))) throw new InvalidOperationException("The monitor signal changed while preparing the test. Reopen the creator and retry.");
            layout = DisplayLayoutSnapshot.Capture(); attempted = true;
            CustomNvApi.Check(tryMode(ids, 1, new[] { mode }));
            DisplayMode actual = DisplayService.GetCurrentMode(request.Device);
            if (actual == null || actual.Width != request.Width || actual.Height != request.Height || Math.Abs(actual.Frequency - request.Refresh) > 1)
                throw new InvalidOperationException("The active display mode did not match the requested test. Nothing will be saved.");
            if (!NvidiaWorkingSignal.Same(mode.Timing, NvidiaWorkingSignal.Read(request.Device))) throw new InvalidOperationException("The driver changed the monitor signal instead of only scaling the desktop. The test will revert and cannot be saved.");
        }
        public void Save() { CustomNvApi.Check(saveMode(ids, 1, 1, 1)); saved = true; }
        public void Revert() { if (attempted) CustomNvApi.Check(revertMode(ids, 1)); }
        public void RestoreLayout()
        {
            if (attempted && layout != null) layout.Restore();
            // Only an explicit, successful Save requests a persistent scaling
            // preference. Cancel/timeout never change it.
            if (saved)
            {
                using (NvidiaScalingConfig scaling = new NvidiaScalingConfig(request.Device)) scaling.ApplyFullscreen();
                using (NvidiaScalingConfig verify = new NvidiaScalingConfig(request.Device))
                    if (verify.CurrentScaling != 2) throw new InvalidOperationException("The custom mode was saved, but full-screen GPU scaling was not kept by the driver.");
            }
        }
        public void Dispose() { api.Dispose(); }
    }
    internal static class CustomResolutionWorker
    {
        internal static bool WaitForDecision(Func<long> elapsed, Func<bool> canceled, Func<bool> parentExited, Func<bool> waitForSave)
        {
            while (elapsed() < 15000)
            {
                if (canceled() || parentExited()) return false;
                if (waitForSave()) return !canceled() && !parentExited() && elapsed() < 15000;
            }
            return false;
        }
        public static string StatePath(string token) { return Path.Combine(DesktopVibranceStore.DefaultFolder, "custom-trials", token + ".state"); }
        public static string EventName(string token, string action) { return Lucent.LegacyNames.CustomTrialEvent + token + "." + action; }
        private static void Status(string token, string state, string message)
        {
            string path = StatePath(token); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, new[] { state, message.Replace('\r', ' ').Replace('\n', ' ') });
        }
        public static int Run(string[] args)
        {
            Guid guid; int width, height, refresh, parentId;
            if (args.Length != 8 || !Guid.TryParseExact(args[1], "N", out guid) || !int.TryParse(args[3], out width) || !int.TryParse(args[4], out height) || !int.TryParse(args[5], out refresh) || !int.TryParse(args[6], out parentId) || args[7] != "v1") return 2;
            string token = guid.ToString("N");
            try
            {
                using (Process parent = Process.GetProcessById(parentId))
                using (EventWaitHandle save = EventWaitHandle.OpenExisting(EventName(token, "save")))
                using (EventWaitHandle cancel = EventWaitHandle.OpenExisting(EventName(token, "cancel")))
                using (Mutex mutex = new Mutex(false, Lucent.LegacyNames.CustomTrialMutex))
                {
                    if (parent.HasExited || !string.Equals(parent.MainModule.FileName, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return 2;
                    bool owns = false;
                    try
                    {
                        try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
                        if (!owns) throw new InvalidOperationException("Another custom resolution test is already running.");
                        string device = Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
                        if (!DisplayService.GetDisplays().Any(display => display.DeviceName == device)) throw new InvalidOperationException("The selected display is no longer connected.");
                        CustomResolutionRequest request = new CustomResolutionRequest { Device = device, Width = width, Height = height, Refresh = refresh };
                        Status(token, "preparing", "Calculating driver timings…");
                        using (NvidiaCustomResolutionTrial trial = new NvidiaCustomResolutionTrial(request))
                        {
                            CustomResolutionOutcome outcome = CustomResolutionTransaction.Run(trial, () => {
                                Stopwatch deadline = Stopwatch.StartNew(); Status(token, "testing", trial.TimingSummary);
                                return WaitForDecision(() => deadline.ElapsedMilliseconds, () => cancel.WaitOne(0), () => parent.HasExited, () => save.WaitOne(50));
                            });
                            Status(token, outcome.Error.Length > 0 ? "error" : outcome.Saved ? "saved" : "reverted", outcome.Error.Length > 0 ? (outcome.Saved ? "Mode saved. " : "") + outcome.Error : outcome.Saved ? "Mode saved with full-screen GPU scaling. Previous resolution and layout restored." : "Test reverted. No custom resolution was saved.");
                            return outcome.Error.Length > 0 ? 1 : 0;
                        }
                    }
                    finally { if (owns) mutex.ReleaseMutex(); }
                }
            }
            catch (Exception error) { try { Status(token, "error", error.Message); } catch { } return 1; }
        }
    }
    internal sealed class CustomResolutionDialog : Form
    {
        private readonly ComboBox displays;
        private readonly NumericUpDown width, height, refresh;
        private readonly Label status;
        private readonly Label signalInfo;
        private readonly Button test, save, cancel;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
        private EventWaitHandle saveEvent, cancelEvent;
        private Process worker; private string token; private bool testing, closeWhenDone;
        private readonly Stopwatch testingClock = new Stopwatch();
        private bool signalAvailable;
        private string signalDescription = "Reading current monitor signal…";
        public CustomResolutionDialog()
        {
            Text = "Lucent · Custom resolution"; Font = new Font(Synthwave.FontFamily, 9); ClientSize = new Size(484, 365);
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false; AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(new Label { Text = "Create a GPU-scaled desktop resolution", Bounds = new Rectangle(20, 16, 444, 24), Font = new Font(Font, FontStyle.Bold) });
            displays = new ComboBox { Bounds = new Rectangle(20, 52, 444, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            displays.Items.AddRange(DisplayService.GetDisplays().Cast<object>().ToArray()); if (displays.Items.Count > 0) displays.SelectedIndex = 0; Controls.Add(displays);
            width = Number("Width", 20, 320, 16384, 1920); height = Number("Height", 174, 200, 16384, 1080); refresh = Number("Current Hz (fixed)", 328, 23, 1000, 60); refresh.ReadOnly = true;
            displays.SelectedIndexChanged += (s, e) => LoadCurrent(); LoadCurrent();
            signalInfo = new Label { Text = signalDescription + "\nOnly the desktop size changes. The current monitor signal is preserved.\nClose games first. Save adds the mode and selects full-screen GPU scaling.", Bounds = new Rectangle(20, 156, 444, 61) }; Controls.Add(signalInfo);
            LinkLabel nvidiaSetup = new LinkLabel { Text = "NVIDIA setup: enable modes not exposed by the display…", Bounds = new Rectangle(20, 220, 444, 21) };
            nvidiaSetup.LinkClicked += (s, e) => OpenNvidiaSetup(); Controls.Add(nvidiaSetup);
            status = new Label { Text = "Enter a mode, then test it. No change is made until you click Test.", Bounds = new Rectangle(20, 245, 444, 53) }; Controls.Add(status);
            test = new Button { Text = "Test 15 seconds", Bounds = new Rectangle(20, 311, 150, 32), Enabled = signalAvailable };
            save = new Button { Text = "Save resolution", Bounds = new Rectangle(180, 311, 150, 32), Enabled = false };
            cancel = new Button { Text = "Close", Bounds = new Rectangle(340, 311, 124, 32) };
            Controls.AddRange(new Control[] { test, save, cancel });
            test.Click += StartTrial; save.Click += (s, e) => { if (testing && worker != null && !worker.HasExited) { save.Enabled = false; saveEvent.Set(); status.Text = "Saving, then restoring the previous layout…"; } };
            cancel.Click += (s, e) => { if (worker != null && !worker.HasExited) { cancelEvent.Set(); save.Enabled = false; status.Text = "Reverting…"; } else Close(); };
            timer.Tick += (s, e) => Poll();
            FormClosing += (s, e) => { if (worker != null && !worker.HasExited) { e.Cancel = true; closeWhenDone = true; cancelEvent.Set(); save.Enabled = false; status.Text = "Restoring the original display layout before closing…"; } };
            // Keep this technical dialog in its original light Windows appearance.
        }
        private NumericUpDown Number(string label, int left, int min, int max, int initial)
        {
            Controls.Add(new Label { Text = label, Bounds = new Rectangle(left, 99, 136, 20) });
            NumericUpDown input = new NumericUpDown { Minimum = min, Maximum = max, Value = initial, Bounds = new Rectangle(left, 121, 136, 27) }; Controls.Add(input); return input;
        }
        private void OpenNvidiaSetup()
        {
            try
            {
                string classic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "Control Panel Client", "nvcplui.exe");
                if (File.Exists(classic)) Process.Start(new ProcessStartInfo(classic) { UseShellExecute = true });
                else Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel") { UseShellExecute = true });
                MessageBox.Show(this, "In NVIDIA Control Panel, select your intended monitor under Change resolution, click Customize, then check Enable resolutions not exposed by the display and click OK. Return here to test your custom mode.\n\nLucent saves modes through the driver directly. This one-time checkbox step is only needed if your driver requires it; Lucent cannot verify or toggle that checkbox through the public driver API.", "NVIDIA setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error) { status.Text = "Could not open NVIDIA Control Panel: " + error.Message; }
        }
        private void LoadCurrent()
        {
            DisplayInfo display = displays.SelectedItem as DisplayInfo; if (display == null || width == null) return;
            DisplayMode mode = DisplayService.GetCurrentMode(display.DeviceName); if (mode == null) return;
            width.Value = Math.Max(width.Minimum, Math.Min(width.Maximum, mode.Width)); height.Value = Math.Max(height.Minimum, Math.Min(height.Maximum, mode.Height));
            signalAvailable = false;
            try
            {
                NvTiming timing = NvidiaWorkingSignal.Read(display.DeviceName);
                int hz = (int)Math.Round(NvidiaWorkingSignal.Hertz(timing));
                if (hz < 23 || hz > 1000) throw new InvalidOperationException("The current monitor refresh rate is unsupported.");
                refresh.Minimum = 23; refresh.Maximum = 1000; refresh.Value = hz; refresh.Minimum = refresh.Maximum = hz;
                signalDescription = "Monitor stays at " + timing.HVisible + " × " + timing.VVisible + " · " + hz + " Hz.";
                signalAvailable = true;
            }
            catch (Exception error) { signalDescription = error.Message; }
            if (signalInfo != null) signalInfo.Text = signalDescription + "\nOnly the desktop size changes. The current monitor signal is preserved.\nClose games first. Save adds the mode and selects full-screen GPU scaling.";
            if (test != null) test.Enabled = signalAvailable;
        }
        private void StartTrial(object sender, EventArgs e)
        {
            DisplayInfo display = displays.SelectedItem as DisplayInfo; if (display == null) return;
            DisplayMode active = DisplayService.GetCurrentMode(display.DeviceName);
            if (active != null && active.Width == width.Value && active.Height == height.Value && active.Frequency == refresh.Value) { status.Text = "That is already the active desktop size. Enter a different width or height to test."; return; }
            try
            {
                CleanupWorker(); token = Guid.NewGuid().ToString("N");
                saveEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CustomResolutionWorker.EventName(token, "save"));
                cancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CustomResolutionWorker.EventName(token, "cancel"));
                string arguments = "--custom-resolution-trial " + token + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes(display.DeviceName)) + " " + (int)width.Value + " " + (int)height.Value + " " + (int)refresh.Value + " " + Process.GetCurrentProcess().Id + " v1";
                worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                if (worker == null) throw new InvalidOperationException("Could not start the display trial.");
                testing = false; closeWhenDone = false; status.Text = "Preparing the test. Wait for the driver…";
                displays.Enabled = width.Enabled = height.Enabled = refresh.Enabled = test.Enabled = false;
                cancel.Text = "Revert"; timer.Start();
            }
            catch (Exception error) { status.Text = error.Message; CleanupWorker(); }
        }
        private void Poll()
        {
            if (worker == null) return;
            try
            {
                string[] state = File.Exists(CustomResolutionWorker.StatePath(token)) ? File.ReadAllLines(CustomResolutionWorker.StatePath(token)) : new string[0];
                if (state.Length >= 2)
                {
                    if (state[0] == "testing")
                    {
                        if (!testing) { testing = true; testingClock.Restart(); save.Enabled = true; }
                        if (save.Enabled) status.Text = state[1] + Environment.NewLine + "Picture correct? Save within " + Math.Max(0, 15 - (int)testingClock.Elapsed.TotalSeconds) + " seconds. Otherwise it reverts.";
                    }
                    else if (state[0] != "preparing") { status.Text = state[1]; save.Enabled = false; }
                }
                if (!worker.HasExited) return;
                if (state.Length < 2) status.Text = "The display helper exited unexpectedly. Check Windows Display settings before another test.";
                timer.Stop(); displays.Enabled = width.Enabled = height.Enabled = refresh.Enabled = true; test.Enabled = signalAvailable; save.Enabled = false; cancel.Text = "Close";
                CleanupWorker(); if (closeWhenDone) Close();
            }
            catch (IOException) { } // Worker may be updating its status file.
        }
        private void CleanupWorker()
        {
            if (worker != null) { worker.Dispose(); worker = null; }
            if (saveEvent != null) { saveEvent.Dispose(); saveEvent = null; }
            if (cancelEvent != null) { cancelEvent.Dispose(); cancelEvent = null; }
            if (token != null) { try { File.Delete(CustomResolutionWorker.StatePath(token)); } catch { } token = null; }
        }
        protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); if (cancelEvent != null) cancelEvent.Set(); CleanupWorker(); } base.Dispose(disposing); }
    }
}
