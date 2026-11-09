using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class VibranceRecovery
    {
        public string Device;
        public int Before, Applied;
        public int? PreviousApplied;
    }
    internal sealed class DesktopVibranceStore
    {
        private readonly string folder;
        public int Percent = 70;
        public DesktopVibranceStore(string folder) { this.folder = folder; }
        public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResolutionSwitcher"); } }
        private string Journal { get { return Path.Combine(folder, "desktop-vibrance-recovery.ini"); } }
        private string LegacyJournal { get { return Path.Combine(folder, "vibrance-recovery.ini"); } }
        public void LoadProfile()
        {
            string path = Path.Combine(folder, "desktop-vibrance.ini");
            if (!File.Exists(path)) return;
            int value; if (int.TryParse(File.ReadAllText(path).Trim(), out value)) Percent = Math.Max(0, Math.Min(100, value));
        }
        public void SaveProfile() { Write(Path.Combine(folder, "desktop-vibrance.ini"), new[] { Percent.ToString() }); }
        public List<VibranceRecovery> LoadRecovery()
        {
            List<VibranceRecovery> records = new List<VibranceRecovery>();
            foreach (string path in new[] { Journal, LegacyJournal })
            {
                if (!File.Exists(path)) continue;
                string[] lines = File.ReadAllLines(path);
                int stride = lines.Length > 0 && lines[0] == Lucent.LegacyNames.DesktopVibranceV2 ? 4 : 3;
                if (lines.Length < 1 || (lines[0] != Lucent.LegacyNames.DesktopVibranceV1 && lines[0] != Lucent.LegacyNames.VibranceV1 && lines[0] != Lucent.LegacyNames.DesktopVibranceV2) || (lines.Length - 1) % stride != 0)
                    throw new InvalidOperationException("Vibrance recovery data is unreadable. Check your desktop colors before removing the recovery file.");
                for (int i = 1; i < lines.Length; i += stride)
                {
                    int before, applied;
                    if (string.IsNullOrWhiteSpace(lines[i]) || !int.TryParse(lines[i + 1], out before) || !int.TryParse(lines[i + 2], out applied) || records.Any(record => record.Device == lines[i]))
                        throw new InvalidOperationException("Vibrance recovery data is invalid. Desktop colors were left alone.");
                    int previous;
                    int? previousApplied = null;
                    if (stride == 4 && lines[i + 3] != "-") { if (!int.TryParse(lines[i + 3], out previous)) throw new InvalidOperationException("Vibrance recovery transition is invalid."); previousApplied = previous; }
                    records.Add(new VibranceRecovery { Device = lines[i], Before = before, Applied = applied, PreviousApplied = previousApplied });
                }
            }
            return records;
        }
        public void SaveRecovery(IEnumerable<VibranceRecovery> records)
        {
            List<string> lines = new List<string> { Lucent.LegacyNames.DesktopVibranceV2 };
            foreach (VibranceRecovery record in records) { lines.Add(record.Device); lines.Add(record.Before.ToString()); lines.Add(record.Applied.ToString()); lines.Add(record.PreviousApplied.HasValue ? record.PreviousApplied.Value.ToString() : "-"); }
            Write(Journal, lines.ToArray());
        }
        public void ClearRecovery()
        {
            if (File.Exists(Journal)) File.Delete(Journal);
            if (File.Exists(LegacyJournal)) File.Delete(LegacyJournal);
        }
        private void Write(string path, string[] lines)
        {
            Directory.CreateDirectory(folder);
            string temporary = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
            using (FileStream file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
    internal sealed class DesktopVibranceSession
    {
        private readonly IVibranceDriver driver;
        private readonly DesktopVibranceStore store;
        private List<VibranceRecovery> active = new List<VibranceRecovery>();
        private readonly object gate = new object();
        private volatile bool enabled;
        private bool uncertainTransition;
        public bool Enabled { get { return enabled; } private set { enabled = value; } }
        public bool HasPendingRestore { get { lock (gate) return active.Count > 0; } }
        public DesktopVibranceSession(IVibranceDriver driver, DesktopVibranceStore store) { this.driver = driver; this.store = store; }
        public void Enable(IEnumerable<string> devices, int percent)
        {
            lock (gate) EnableCore(devices, percent);
        }
        private void EnableCore(IEnumerable<string> devices, int percent)
        {
            if (Enabled || HasPendingRestore || store.LoadRecovery().Count > 0) throw new InvalidOperationException("Restore desktop colors before enabling vibrance again.");
            List<VibranceRecovery> planned = devices.Distinct(StringComparer.OrdinalIgnoreCase).Select(device => {
                VibranceLevel level = driver.Read(device);
                return new VibranceRecovery { Device = device, Before = level.Current, Applied = level.FromPercent(percent) };
            }).ToList(); // All capability reads finish before any write.
            if (planned.Count == 0) throw new InvalidOperationException("No supported displays were found.");
            store.SaveRecovery(planned); active = planned; uncertainTransition = true;
            try
            {
                foreach (VibranceRecovery record in active) if (record.Applied != record.Before) driver.Write(record.Device, record.Applied);
                uncertainTransition = false; Enabled = true;
            }
            catch { Enabled = false; try { Restore(); } catch { } throw; }
        }
        public bool Restore()
        {
            lock (gate) return RestoreCore();
        }
        private bool RestoreCore()
        {
            Enabled = false;
            if (active.Count == 0) return false;
            bool keptExternal = false;
            List<VibranceRecovery> pending = new List<VibranceRecovery>();
            Exception failure = null;
            foreach (VibranceRecovery record in active)
            {
                try
                {
                    int current = driver.Read(record.Device).Current;
                    if ((current == record.Applied || (uncertainTransition && record.PreviousApplied.HasValue && current == record.PreviousApplied.Value)) && current != record.Before) driver.Write(record.Device, record.Before);
                    else if (current != record.Before) keptExternal = true;
                }
                catch (Exception error) { pending.Add(record); failure = error; }
            }
            if (pending.Count > 0)
            {
                // Keep the full journal if updating it fails; already-restored
                // monitors match Before and will not be written again.
                store.SaveRecovery(pending); active = pending;
                throw new InvalidOperationException("Could not restore every monitor. Reconnect it and retry. " + failure.Message);
            }
            store.ClearRecovery(); active.Clear(); uncertainTransition = false; return keptExternal;
        }
        public bool Recover(List<VibranceRecovery> records) { lock (gate) { active = records; uncertainTransition = true; return RestoreCore(); } }
        public void Adjust(int percent, Func<bool> stillCurrent = null)
        {
            lock (gate)
            {
                if (!Enabled || (stillCurrent != null && !stillCurrent())) return;
                try
                {
                    var readings = active.Select(record => driver.Read(record.Device)).ToArray();
                    if (active.Where((record, index) => readings[index].Current != record.Applied).Any())
                    {
                        // Once a completed level was changed externally, only
                        // the current applied value is ours to restore.
                        foreach (var record in active) record.PreviousApplied = null;
                        uncertainTransition = false; Enabled = false;
                        store.SaveRecovery(active); RestoreCore();
                        throw new InvalidOperationException("Colors changed outside Lucent. Live vibrance stopped and kept that change.");
                    }
                    var planned = active.Select((record, index) => new VibranceRecovery { Device = record.Device, Before = record.Before, PreviousApplied = record.Applied, Applied = readings[index].FromPercent(percent) }).ToList();
                    if (planned.All(record => record.PreviousApplied == record.Applied)) return;
                    // The journal contains both possible levels before any
                    // write, covering a crash between monitor updates.
                    store.SaveRecovery(planned); active = planned; uncertainTransition = true;
                    foreach (var record in active) if (record.PreviousApplied != record.Applied) driver.Write(record.Device, record.Applied);
                    uncertainTransition = false;
                }
                catch
                {
                    Enabled = false; try { RestoreCore(); } catch { }
                    throw;
                }
            }
        }
    }
    internal sealed class VibrancePageControl : Panel
    {
        private readonly DesktopVibranceStore store;
        private NvidiaVibranceDriver driver;
        private DesktopVibranceSession session;
        private List<VibranceRecovery> recovery = new List<VibranceRecovery>();
        private readonly VibranceSlider slider;
        private readonly Label status, value;
        private readonly AccentButton toggle;
        private bool initialized, blocked, ownsMutex;
        private bool dirtyPreference, loadingPreference;
        private readonly System.Windows.Forms.Timer preferenceTimer = new System.Windows.Forms.Timer { Interval = 250 };
        private readonly System.Windows.Forms.Timer liveTimer = new System.Windows.Forms.Timer { Interval = 80 };
        private int? requestedLivePercent;
        private bool liveBusy;
        private int liveGeneration;
        private Mutex owner;
        public VibrancePageControl() : this(new DesktopVibranceStore(DesktopVibranceStore.DefaultFolder)) { }
        internal VibrancePageControl(DesktopVibranceStore preferenceStore)
        {
            DoubleBuffered = true; BackColor = Color.Transparent;
            store = preferenceStore;
            RoundedPanel card = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, BorderColor = Color.Transparent, CornerRadius = 16 };
            Controls.Add(card);
            card.Controls.Add(MakeLabel("DESKTOP VIBRANCE", new Rectangle(18, 8, 300, 22), 9, true));
            value = MakeLabel("70%", new Rectangle(414, 6, 88, 26), 12, true); card.Controls.Add(value);
            slider = new VibranceSlider { Bounds = new Rectangle(18, 34, 484, 28), Value = 70 }; card.Controls.Add(slider);
            toggle = MakeButton("Turn on", new Rectangle(520, 15, 134, 38)); card.Controls.Add(toggle);
            status = MakeLabel("Off · Whole desktop. 50% neutral. The slider also works while on.", new Rectangle(18, 64, 636, 22), 8, false); card.Controls.Add(status);
            slider.ValueChanged += (s, e) => {
                value.Text = slider.Value + "%"; store.Percent = slider.Value;
                if (loadingPreference) return;
                dirtyPreference = true; preferenceTimer.Stop(); preferenceTimer.Start();
                if (session != null && session.Enabled) { requestedLivePercent = slider.Value; liveTimer.Start(); }
            };
            slider.DragCompleted += (s, e) => SavePreference();
            preferenceTimer.Tick += (s, e) => { if (!slider.IsDragging) SavePreference(); };
            liveTimer.Tick += (s, e) => StartLiveUpdate();
            toggle.Click += Toggle;
            loadingPreference = true;
            try { store.LoadProfile(); slider.Value = store.Percent; recovery = store.LoadRecovery(); }
            catch (Exception error) { blocked = true; status.Text = error.Message; }
            finally { loadingPreference = false; }
            VisibleChanged += (s, e) => { if (Visible && !initialized) InitializeDriver(); };
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
            RefreshButtons();
        }
        private void InitializeDriver()
        {
            initialized = true;
            if (blocked) return;
            try
            {
                driver = new NvidiaVibranceDriver();
                foreach (Screen screen in Screen.AllScreens) driver.Read(screen.DeviceName);
                session = new DesktopVibranceSession(driver, store);
                status.Text = recovery.Count == 0 ? "Off · All desktop monitors. 50% neutral. Adjust the slider while on, too." : "Previous session found · Check monitors, then Turn off to restore colors.";
            }
            catch (Exception error) { status.Text = "Unavailable on all monitors: " + error.Message; if (driver != null) { driver.Dispose(); driver = null; } }
            RefreshButtons();
        }
        private void SavePreference()
        {
            preferenceTimer.Stop(); if (!dirtyPreference) return;
            try { store.SaveProfile(); dirtyPreference = false; }
            catch { status.Text = "Could not save vibrance preference."; }
        }
        private void StartLiveUpdate()
        {
            liveTimer.Stop();
            if (liveBusy || !requestedLivePercent.HasValue || session == null || !session.Enabled) return;
            int percent = requestedLivePercent.Value, generation = liveGeneration;
            requestedLivePercent = null; liveBusy = true;
            Task.Factory.StartNew(() => { try { session.Adjust(percent, () => generation == Thread.VolatileRead(ref liveGeneration)); return (Exception)null; } catch (Exception error) { return error; } }).ContinueWith(task => {
                if (IsDisposed || Disposing) return;
                try { BeginInvoke(new Action(() => {
                    if (generation != liveGeneration) return;
                    liveBusy = false;
                    Exception error = task.IsFaulted ? task.Exception : task.Result;
                    if (error != null)
                    {
                        requestedLivePercent = null; liveTimer.Stop(); status.Text = error.Message;
                        if (!session.HasPendingRestore) ReleaseOwnership(); RefreshButtons();
                    }
                    else
                    {
                        status.Text = "On · " + percent + "% across the desktop. Off restores your original colors.";
                        if (requestedLivePercent.HasValue) liveTimer.Start();
                    }
                })); } catch (InvalidOperationException) { }
            });
        }
        private static Label MakeLabel(string text, Rectangle bounds, float size, bool bold) { return new NeonLabel { Text = text, Bounds = bounds, Font = new Font(Synthwave.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = bold ? Color.White : Color.FromArgb(215, 185, 232), BackColor = Color.Transparent }; }
        private static AccentButton MakeButton(string text, Rectangle bounds) { return new AccentButton { Text = text, Bounds = bounds, SimpleStyle = true, AccentColor = Color.FromArgb(65, 43, 88), Font = new Font(Synthwave.FontFamily, 9, FontStyle.Bold) }; }
        private bool TakeOwnership()
        {
            if (ownsMutex) return true;
            // Same name as the previous game-vibrance preview to avoid conflict.
            if (owner == null) owner = new Mutex(false, Lucent.LegacyNames.GameVibranceMutex);
            try { ownsMutex = owner.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
            return ownsMutex;
        }
        private void ReleaseOwnership() { if (ownsMutex) { owner.ReleaseMutex(); ownsMutex = false; } }
        private void Toggle(object sender, EventArgs e)
        {
            if (session == null) return;
            if (session.Enabled || session.HasPendingRestore || recovery.Count > 0) { RestoreClicked(sender, e); return; }
            try
            {
                if (!TakeOwnership()) { status.Text = "Another Lucent window controls vibrance. Turn it off there first."; return; }
                recovery = store.LoadRecovery();
                if (recovery.Count > 0) { status.Text = "Previous session found · Turn off restores its desktop colors."; RefreshButtons(); return; }
                session.Enable(Screen.AllScreens.Select(screen => screen.DeviceName), slider.Value);
                status.Text = "On · " + slider.Value + "% across the desktop. Off restores your previous colors.";
            }
            catch (Exception error) { status.Text = error.Message; if (!session.HasPendingRestore) ReleaseOwnership(); }
            RefreshButtons();
        }
        public bool StopAndRestore()
        {
            liveGeneration++; liveTimer.Stop(); requestedLivePercent = null;
            if (session == null) return true;
            try
            {
                bool external = session.Restore(); liveBusy = false; ReleaseOwnership();
                status.Text = recovery.Count > 0 ? "Previous session found · Check monitors before Turn off." : external ? "Off · Kept colors changed outside Lucent." : "Off · Your previous desktop colors are restored.";
                RefreshButtons(); return true;
            }
            catch (Exception error) { status.Text = error.Message; RefreshButtons(); return false; }
        }
        private void RestoreClicked(object sender, EventArgs e)
        {
            if (session == null) return;
            try
            {
                if (!TakeOwnership()) { status.Text = "Turn off vibrance in the other Lucent window first."; return; }
                if (recovery.Count > 0) { bool external = session.Recover(recovery); recovery.Clear(); status.Text = external ? "Off · Kept colors changed outside Lucent." : "Off · Previous colors restored."; ReleaseOwnership(); RefreshButtons(); }
                else StopAndRestore();
            }
            catch (Exception error) { status.Text = error.Message; RefreshButtons(); }
        }
        private void RefreshButtons()
        {
            bool on = session != null && session.Enabled, pending = recovery.Count > 0 || (session != null && session.HasPendingRestore);
            slider.Enabled = !blocked && (on || !pending);
            toggle.Text = on || pending ? "Turn off" : "Turn on";
            toggle.Enabled = session != null;
            toggle.AccentColor = on ? Color.FromArgb(26, 103, 108) : Color.FromArgb(65, 43, 88);
        }
        private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e) { RestoreOnSystemEvent(); }
        private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e) { if (e.Mode == Microsoft.Win32.PowerModes.Suspend) RestoreOnSystemEvent(); }
        private void RestoreOnSystemEvent()
        {
            if (!IsHandleCreated || IsDisposed) return;
            try { if (InvokeRequired) Invoke(new Action(() => StopAndRestore())); else StopAndRestore(); } catch (InvalidOperationException) { }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                StopAndRestore(); SavePreference(); preferenceTimer.Dispose(); liveTimer.Dispose(); ReleaseOwnership(); if (owner != null) owner.Dispose(); if (driver != null) driver.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
