using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class RefreshDesktopState
    {
        public string Device;
        public int Width, Height, Hertz, X, Y, Orientation, Depth;
        public static List<RefreshDesktopState> Read()
        {
            return DisplayService.GetDisplays().Select(display => {
                DisplayMode mode = DisplayService.GetCurrentMode(display.DeviceName);
                if (mode == null) throw new InvalidOperationException("Could not read every active monitor before the refresh test.");
                return new RefreshDesktopState { Device = display.DeviceName, Width = mode.Width, Height = mode.Height, Hertz = mode.Frequency, X = mode.NativeMode.dmPositionX, Y = mode.NativeMode.dmPositionY, Orientation = mode.NativeMode.dmDisplayOrientation, Depth = mode.BitsPerPixel };
            }).ToList();
        }
        internal static bool OnlyRefreshChanged(IEnumerable<RefreshDesktopState> before, IEnumerable<RefreshDesktopState> after, string device, int hertz)
        {
            var first = before.ToArray(); var second = after.ToArray();
            return first.Any(old => old.Device == device) && first.Length == second.Length && first.All(old => second.Any(current =>
                current.Device == old.Device && current.Width == old.Width && current.Height == old.Height && current.X == old.X && current.Y == old.Y && current.Orientation == old.Orientation && current.Depth == old.Depth &&
                current.Hertz == (old.Device == device ? hertz : old.Hertz)));
        }
    }
    internal interface IRefreshRateTrial
    {
        void ApplyAndVerify(); void Restore();
    }
    internal sealed class RefreshRateOutcome { public bool Kept; public string Error = ""; }
    internal static class RefreshRateTransaction
    {
        public static RefreshRateOutcome ApplyImmediately(IRefreshRateTrial trial) {return Run(trial,()=>true);}
        public static RefreshRateOutcome ApplySupported(string device,int hertz)
        {
            using(Mutex gate=new Mutex(false,Lucent.LegacyNames.CustomTrialMutex))
            {
                bool owns=false;
                try
                {
                    try {owns=gate.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
                    if(!owns)return new RefreshRateOutcome {Error="Another display change is already running."};
                    return ApplyImmediately(new WindowsRefreshRateTrial(device,hertz));
                }
                finally {if(owns)gate.ReleaseMutex();}
            }
        }
        public static RefreshRateOutcome Run(IRefreshRateTrial trial, Func<bool> keep)
        {
            var outcome = new RefreshRateOutcome();
            try { trial.ApplyAndVerify(); outcome.Kept = keep(); }
            catch (Exception error) { outcome.Error = error.Message; }
            finally { if (!outcome.Kept) try { trial.Restore(); } catch (Exception error) { outcome.Error += " Restore: " + error.Message; } }
            return outcome;
        }
    }
    internal sealed class WindowsRefreshRateTrial : IRefreshRateTrial
    {
        private readonly string device;
        private readonly int hertz;
        private DisplayLayoutSnapshot layout;
        private bool attempted;
        public WindowsRefreshRateTrial(string device, int hertz) { this.device = device; this.hertz = hertz; }
        public void ApplyAndVerify()
        {
            DisplayMode current = DisplayService.GetCurrentMode(device);
            if (current == null) throw new InvalidOperationException("The selected display is no longer available.");
            DisplayMode target = DisplayService.GetModes(device).FirstOrDefault(mode => mode.Width == current.Width && mode.Height == current.Height && mode.BitsPerPixel == current.BitsPerPixel && mode.Frequency == hertz);
            if (target == null) throw new InvalidOperationException("That refresh rate is not currently reported for this exact resolution and color depth.");
            List<RefreshDesktopState> before = RefreshDesktopState.Read();
            layout = DisplayLayoutSnapshot.Capture(); attempted = true;
            DisplayChangeResult result = DisplayService.Apply(target, current, false, false);
            if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
            if (!RefreshDesktopState.OnlyRefreshChanged(before, RefreshDesktopState.Read(), device, hertz))
                throw new InvalidOperationException("Windows changed more than the selected refresh rate. Restoring the previous display layout.");
        }
        public void Restore() { if (attempted && layout != null) layout.Restore(); }
    }
    internal static class RefreshRateWorker
    {
        public static int Run(string[] args)
        {
            Guid guid; int hertz, parentId;
            if (args.Length != 6 || !Guid.TryParseExact(args[1], "N", out guid) || !int.TryParse(args[3], out hertz) || hertz < 2 || hertz > 1000 || !int.TryParse(args[4], out parentId) || args[5] != "v1") return 2;
            string token = guid.ToString("N");
            try
            {
                using (Process parent = Process.GetProcessById(parentId))
                using (EventWaitHandle keep = EventWaitHandle.OpenExisting(CustomResolutionWorker.EventName(token, "save")))
                using (EventWaitHandle cancel = EventWaitHandle.OpenExisting(CustomResolutionWorker.EventName(token, "cancel")))
                using (Mutex gate = new Mutex(false, Lucent.LegacyNames.CustomTrialMutex))
                {
                    if (parent.HasExited || !string.Equals(parent.MainModule.FileName, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return 2;
                    bool owns = false;
                    try
                    {
                        try { owns = gate.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
                        if (!owns) throw new InvalidOperationException("Another display test is running.");
                        string device = Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
                        Write(token, "preparing", "Changing only the refresh rate…");
                        var outcome = RefreshRateTransaction.Run(new WindowsRefreshRateTrial(device, hertz), () => {
                            Stopwatch clock = Stopwatch.StartNew(); Write(token, "testing", "Testing " + hertz + " Hz. Keep within 15 seconds, otherwise it reverts.");
                            return CustomResolutionWorker.WaitForDecision(() => clock.ElapsedMilliseconds, () => cancel.WaitOne(0), () => parent.HasExited, () => keep.WaitOne(50));
                        });
                        Write(token, outcome.Error.Length > 0 ? "error" : outcome.Kept ? "kept" : "reverted", outcome.Error.Length > 0 ? outcome.Error : outcome.Kept ? "Kept " + hertz + " Hz. Resolution and monitor layout are unchanged." : "Previous refresh rate restored.");
                        return outcome.Error.Length > 0 ? 1 : 0;
                    }
                    finally { if (owns) gate.ReleaseMutex(); }
                }
            }
            catch (Exception error) { try { Write(token, "error", error.Message); } catch { } return 1; }
        }
        private static void Write(string token, string state, string message)
        {
            string path = CustomResolutionWorker.StatePath(token); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, new[] { state, message.Replace('\r', ' ').Replace('\n', ' ') });
        }
    }
    internal sealed class RefreshRateDialog : Form
    {
        private readonly string device;
        private readonly int hertz;
        private readonly Label status;
        private readonly Button keep, revert;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
        private readonly Stopwatch clock = new Stopwatch();
        private string token;
        private Process worker;
        private EventWaitHandle keepEvent, cancelEvent;
        private bool testing, closeWhenDone;
        public RefreshRateDialog(string device, int hertz)
        {
            this.device = device; this.hertz = hertz;
            Text = "Lucent · Refresh rate only"; ClientSize = new Size(430, 160); Font = new Font(Synthwave.FontFamily, 9); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false; AutoScaleMode = AutoScaleMode.Dpi;
            status = new Label { Text = "Preparing " + hertz + " Hz. Resolution stays unchanged.", Bounds = new Rectangle(20, 20, 390, 68) }; Controls.Add(status);
            keep = new Button { Text = "Keep " + hertz + " Hz", Bounds = new Rectangle(118, 108, 142, 31), Enabled = false };
            revert = new Button { Text = "Revert", Bounds = new Rectangle(272, 108, 138, 31) };
            Controls.Add(keep); Controls.Add(revert);
            keep.Click += (s, e) => { if (worker == null || worker.HasExited || !testing) return; keep.Enabled = false; keepEvent.Set(); status.Text = "Keeping " + hertz + " Hz…"; };
            revert.Click += (s, e) => { if (worker != null && !worker.HasExited) { cancelEvent.Set(); keep.Enabled = false; status.Text = "Restoring the previous refresh rate…"; } else Close(); };
            Shown += (s, e) => Start(); timer.Tick += (s, e) => Poll();
            FormClosing += (s, e) => { if (worker != null && !worker.HasExited) { e.Cancel = true; closeWhenDone = true; cancelEvent.Set(); keep.Enabled = false; } };
            Synthwave.StyleDialog(this);
        }
        private void Start()
        {
            try
            {
                token = Guid.NewGuid().ToString("N");
                keepEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CustomResolutionWorker.EventName(token, "save"));
                cancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CustomResolutionWorker.EventName(token, "cancel"));
                string args = "--refresh-only-trial " + token + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes(device)) + " " + hertz + " " + Process.GetCurrentProcess().Id + " v1";
                worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                if (worker == null) throw new InvalidOperationException("Could not start the refresh test.");
                timer.Start();
            }
            catch (Exception error) { status.Text = error.Message; revert.Text = "Close"; }
        }
        private void Poll()
        {
            if (worker == null) return;
            try
            {
                string path = CustomResolutionWorker.StatePath(token);
                string[] state = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
                if (state.Length >= 2)
                {
                    if (state[0] == "testing")
                    {
                        if (!testing) { testing = true; clock.Restart(); keep.Enabled = true; }
                        if (keep.Enabled) status.Text = "Keep " + hertz + " Hz? Resolution stays unchanged.\nAutomatically reverts in " + Math.Max(0, 15 - (int)clock.Elapsed.TotalSeconds) + " seconds.";
                    }
                    else { status.Text = state[1]; keep.Enabled = false; }
                }
                if (!worker.HasExited) return;
                timer.Stop(); keep.Enabled = false; revert.Text = "Close";
                if (state.Length < 2) status.Text = "The refresh helper exited unexpectedly. Check the active display settings.";
                bool success = state.Length >= 2 && state[0] == "kept";
                Cleanup(); if (success || closeWhenDone) Close();
            }
            catch (IOException) { }
        }
        private void Cleanup()
        {
            if (worker != null) { worker.Dispose(); worker = null; }
            if (keepEvent != null) { keepEvent.Dispose(); keepEvent = null; }
            if (cancelEvent != null) { cancelEvent.Dispose(); cancelEvent = null; }
            if (token != null) { try { File.Delete(CustomResolutionWorker.StatePath(token)); } catch { } token = null; }
        }
        protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); if (cancelEvent != null) cancelEvent.Set(); Cleanup(); } base.Dispose(disposing); }
    }
}
