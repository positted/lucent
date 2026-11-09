using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class BoostAppDefinition
    {
        public string Id, DisplayName, RestoreProcessName, Reason;
        public string[] ProcessNames;
        public bool Recommended;
        public string IdentityHint; internal bool Dynamic; internal List<BoostStamp> Captured;

        public BoostAppDefinition(string id, string displayName, string restoreProcessName, params string[] processNames)
            : this(id, displayName, restoreProcessName, true, "Optional background app", processNames) { }

        public BoostAppDefinition(string id, string displayName, string restoreProcessName, bool recommended, string reason, params string[] processNames)
        {
            Id = id; DisplayName = displayName; RestoreProcessName = restoreProcessName;
            Recommended = recommended; Reason = reason; ProcessNames = processNames;
        }
    }

    internal sealed class BoostCandidate
    {
        public BoostAppDefinition Definition;
        public int Instances;
        public long MemoryBytes;
        public double CpuPercent; public bool IsProtected; public string ProtectionReason,Category;
        public readonly List<string> Evidence = new List<string>();
        public int MemoryMb { get { return (int)Math.Max(1, Math.Round(MemoryBytes / 1048576d)); } }
        public string MemoryText { get { return MemoryBytes < 1024 ? Math.Max(0, MemoryBytes) + " B" : MemoryBytes < 1048576 ? (MemoryBytes / 1024d).ToString("N0") + " KB" : (MemoryBytes / 1048576d).ToString("N0") + " MB"; } }
    }

    internal sealed class BoostResult
    {
        public int StoppedProcesses, AffectedApps;
        public string Message;
    }

    internal sealed class BackgroundAppBoost
    {
        internal static readonly BoostAppDefinition[] Catalog =
        {
            new BoostAppDefinition("riot-client", "Riot Client", "RiotClientServices", false, "Launcher backend; kept while a Riot game is running", "RiotClientServices") { IdentityHint="Riot Games|Riot Client" },
            new BoostAppDefinition("riot-interface", "Riot Client interface", "RiotClientUx", false, "Launcher UI and web helpers · review before closing", "RiotClientUx", "RiotClientUxRender", "VALORANT") { IdentityHint="Riot Games|Riot Client|VALORANT" },
            new BoostAppDefinition("liner", "Liner", "liner", false, "Music player and helpers", "liner"),
            new BoostAppDefinition("nvidia-overlay", "NVIDIA overlay", "NVIDIA Overlay", false, "Optional capture overlay; driver containers are protected", "NVIDIA Overlay", "NVIDIA Share") { IdentityHint="NVIDIA" },
            new BoostAppDefinition("guilded", "Guilded", "Guilded", false, "Chat and helper processes", "Guilded"),
            new BoostAppDefinition("steelseries-ui", "SteelSeries GG interface", "SteelSeriesGG", false, "Interface only; device and audio services remain protected", "SteelSeriesGG") { IdentityHint="SteelSeries" },
            new BoostAppDefinition("medal", "Medal", "Medal", true, "Recorder and encoder", "Medal", "MedalEncoder"),
            new BoostAppDefinition("discord", "Discord", "Discord", true, "Chat and helper processes", "Discord", "DiscordPTB", "DiscordCanary"),
            new BoostAppDefinition("spotify", "Spotify", "Spotify", true, "Music and web helpers", "Spotify"),
            new BoostAppDefinition("steam", "Steam", "steam", true, "Launcher and web helpers", "steam", "steamwebhelper", "gameoverlayui"),
            new BoostAppDefinition("epic", "Epic Games launcher", "EpicGamesLauncher", true, "Verified Epic launcher", "EpicGamesLauncher") { IdentityHint = "Epic Games|EpicGamesLauncher" },
            new BoostAppDefinition("epic-helper", "Epic web helper", "EpicWebHelper", false, "Shared helper; check the owning app", "EpicWebHelper") { IdentityHint = "Epic Games|EpicGamesLauncher" },
            new BoostAppDefinition("chatgpt", "ChatGPT", "ChatGPT", false, "Closes the standalone ChatGPT app", "ChatGPT"),
            new BoostAppDefinition("codex", "Codex", "ChatGPT", false, "Ends Codex chats, terminals and active work", "ChatGPT", "Codex", "codex-code-mode-host", "codex-computer-use-swift", "node_repl"),
            new BoostAppDefinition("xbox", "Xbox / Game Bar", "XboxPcApp", true, "Game Bar and Xbox widgets", "XboxPcApp", "GameBar", "GameBarFTServer", "XboxGameBarWidgets"),
            new BoostAppDefinition("onedrive", "OneDrive", "OneDrive", true, "File sync pauses until restored", "OneDrive"),
            new BoostAppDefinition("brave", "Brave", "brave", true, "Browser tabs and extensions", "brave"),
            new BoostAppDefinition("chrome", "Google Chrome", "chrome", true, "Browser tabs and extensions", "chrome"),
            new BoostAppDefinition("edge", "Microsoft Edge", "msedge", true, "Browser tabs and extensions", "msedge"),
            new BoostAppDefinition("firefox", "Firefox", "firefox", true, "Browser tabs and extensions", "firefox"),
            new BoostAppDefinition("opera", "Opera", "opera", true, "Browser tabs and extensions", "opera", "opera_gx_splash"),
            new BoostAppDefinition("teams", "Microsoft Teams", "ms-teams", true, "Calls and chat close", "ms-teams", "Teams"),
            new BoostAppDefinition("phone", "Phone Link", "PhoneExperienceHost", true, "Phone sync and cross-device tasks", "PhoneExperienceHost", "CrossDeviceResume"),
            new BoostAppDefinition("overwolf", "Overwolf", "Overwolf", true, "Overlay and companion apps", "Overwolf", "OverwolfBrowser"),
            new BoostAppDefinition("obs", "OBS Studio", "obs64", false, "Stops recording or streaming", "obs64", "obs32"),
            new BoostAppDefinition("streamlabs", "Streamlabs", "Streamlabs OBS", false, "Stops recording or streaming", "Streamlabs OBS", "Streamlabs"),
            new BoostAppDefinition("adobe", "Adobe Creative Cloud", "Creative Cloud", true, "Sync and Creative Cloud helpers", "Creative Cloud", "CCXProcess", "CoreSync", "AdobeIPCBroker"),
            new BoostAppDefinition("docker", "Docker Desktop", "Docker Desktop", false, "Stops local containers", "Docker Desktop", "com.docker.backend", "com.docker.proxy"),
            new BoostAppDefinition("bluestacks", "BlueStacks", "BlueStacksServices", true, "Android emulator services", "BlueStacksServices", "HD-Player", "BstkSVC"),
            new BoostAppDefinition("blitz", "Blitz", "Blitz", true, "Game companion and overlay", "Blitz"),
            new BoostAppDefinition("roblox", "Roblox", "RobloxPlayerBeta", false, "Closes the running game", "RobloxPlayerBeta"),
            new BoostAppDefinition("dawn", "Dawn Launcher", "Dawn (Feather)", true, "Optional game launcher", "Dawn (Feather)"),
            new BoostAppDefinition("iriun", "Iriun Webcam", "IriunWebcam", false, "Stops the virtual webcam", "IriunWebcam"),
            new BoostAppDefinition("bongocat", "Bongo Cat", "BongoCat", true, "Desktop cosmetic app", "BongoCat"),
            new BoostAppDefinition("voicemod", "Voicemod", "Voicemod", false, "May change your microphone device", "Voicemod"),
            new BoostAppDefinition("crosshairx", "Crosshair X", "CrosshairX", true, "Other crosshair overlay", "CrosshairX"),
            new BoostAppDefinition("telegram", "Telegram", "Telegram", true, "Messaging app", "Telegram"),
            new BoostAppDefinition("whatsapp", "WhatsApp", "WhatsApp", true, "Messaging app", "WhatsApp"),
            new BoostAppDefinition("slack", "Slack", "slack", true, "Chat and helper processes", "slack"),
            new BoostAppDefinition("battlenet", "Battle.net", "Battle.net", true, "Verified launcher and update agent", "Battle.net", "Agent") { IdentityHint = "Battle.net|Blizzard" },
            new BoostAppDefinition("ea", "EA app", "EADesktop", true, "Launcher and background helper", "EADesktop", "EALauncher", "EABackgroundService"),
            new BoostAppDefinition("ubisoft", "Ubisoft Connect", "UbisoftConnect", true, "Launcher and web helpers", "UbisoftConnect", "upc"),
            new BoostAppDefinition("sharex", "ShareX", "ShareX", false, "Stops screen captures and uploads", "ShareX"),
            new BoostAppDefinition("lightshot", "Lightshot", "Lightshot", false, "Stops screenshot shortcuts", "Lightshot"),
            new BoostAppDefinition("everything", "Everything", "Everything", false, "Pauses file search", "Everything"),
            new BoostAppDefinition("quicklook", "QuickLook", "QuickLook", true, "Optional file previews", "QuickLook"),
            new BoostAppDefinition("rainmeter", "Rainmeter", "Rainmeter", true, "Desktop widgets", "Rainmeter"),
            new BoostAppDefinition("wallpaper", "Wallpaper Engine", "wallpaper64", true, "Animated desktop wallpapers", "wallpaper32", "wallpaper64"),
            new BoostAppDefinition("translucenttb", "TranslucentTB", "TranslucentTB", true, "Optional taskbar appearance", "TranslucentTB"),
            new BoostAppDefinition("powertoys", "PowerToys", "PowerToys", false, "Stops optional shortcuts and tools", "PowerToys", "PowerToys.PowerLauncher"),
            new BoostAppDefinition("notion", "Notion", "Notion", false, "Close only after saving your work", "Notion"),
            new BoostAppDefinition("obsidian", "Obsidian", "Obsidian", false, "Close only after saving your notes", "Obsidian"),
            new BoostAppDefinition("figma", "Figma", "Figma", false, "Close only after saving your designs", "Figma"),
            new BoostAppDefinition("ditto", "Ditto", "Ditto", false, "Pauses clipboard history", "Ditto"),
            new BoostAppDefinition("copyq", "CopyQ", "copyq", false, "Pauses clipboard history", "copyq"),
            new BoostAppDefinition("f.lux", "f.lux", "flux", false, "Changes screen color temperature", "flux") { IdentityHint = "f.lux|Flux Software" },
            new BoostAppDefinition("eartrumpet", "EarTrumpet", "EarTrumpet", false, "Closes optional volume controls", "EarTrumpet"),
            new BoostAppDefinition("modernflyouts", "ModernFlyouts", "ModernFlyouts", true, "Optional desktop flyouts", "ModernFlyouts"),
            new BoostAppDefinition("buttery", "ButteryTaskbar", "ButteryTaskbar", true, "Optional taskbar appearance", "ButteryTaskbar"),
            new BoostAppDefinition("roundedtb", "RoundedTB", "RoundedTB", true, "Optional taskbar appearance", "RoundedTB"),
            new BoostAppDefinition("taskbarx", "TaskbarX", "TaskbarX", true, "Optional taskbar appearance", "TaskbarX"),
            new BoostAppDefinition("tweety", "Twinkle Tray", "Twinkle Tray", false, "Pauses monitor brightness shortcuts", "Twinkle Tray") { IdentityHint = "Twinkle Tray" },
            new BoostAppDefinition("lively", "Lively Wallpaper", "Lively", true, "Animated desktop wallpapers", "Lively", "Lively.UI.WinUI", "Lively.Player.WebView2"),
            new BoostAppDefinition("treesize", "TreeSize", "TreeSizeFree", false, "Stops the current disk scan", "TreeSizeFree", "TreeSize"),
            new BoostAppDefinition("wiztree", "WizTree", "WizTree64", false, "Stops the current disk scan", "WizTree64", "WizTree"),
            new BoostAppDefinition("windirstat", "WinDirStat", "windirstat", false, "Stops the current disk scan", "windirstat"),
            new BoostAppDefinition("qbittorrent", "qBittorrent", "qbittorrent", false, "Stops downloads and sharing", "qbittorrent"),
            new BoostAppDefinition("deluge", "Deluge", "deluge", false, "Stops downloads and sharing", "deluge", "deluge-gtk"),
            new BoostAppDefinition("jdownloader", "JDownloader", "JDownloader2", false, "Stops current downloads", "JDownloader2", "JDownloader"),
            new BoostAppDefinition("dropbox", "Dropbox", "Dropbox", false, "Pauses file sync", "Dropbox", "DropboxUpdate"),
            new BoostAppDefinition("googledrive", "Google Drive", "GoogleDriveFS", false, "Pauses file sync", "GoogleDriveFS"),
            new BoostAppDefinition("icue", "iCUE interface", "iCUE", false, "Closes UI; device services are left alone", "iCUE") { IdentityHint = "Corsair|iCUE" },
            new BoostAppDefinition("signal", "Signal", "Signal", false, "Closes messages and calls", "Signal"),
            new BoostAppDefinition("zoom", "Zoom", "Zoom", false, "Closes meetings and calls", "Zoom"),
            new BoostAppDefinition("vlc", "VLC", "vlc", false, "Stops current playback", "vlc"),
            new BoostAppDefinition("winamp", "Winamp", "winamp", false, "Stops current playback", "winamp"),
            new BoostAppDefinition("foobar", "foobar2000", "foobar2000", false, "Stops current playback", "foobar2000"),
            new BoostAppDefinition("equalizerapo-ui", "Peace interface", "Peace", false, "Closes UI; audio driver is left alone", "Peace") { IdentityHint = "Peace" }
        };

        readonly AdvancedBoostEngine engine;
        public BackgroundAppBoost():this(Catalog){}
        internal BackgroundAppBoost(IEnumerable<BoostAppDefinition> customDefinitions){engine=new AdvancedBoostEngine((customDefinitions??Catalog).ToArray());}
        internal static bool IsProtectedProcessName(string name){return BoostPolicy.ProtectedName(name);}
        internal static bool CanManage(BoostAppDefinition definition,ProcessIdentity identity){return BoostPolicy.Matches(definition,identity);}
        public IList<BoostCandidate> DetectCandidates(){return engine.Scan();}
        public IDictionary<string,bool> Detect(){return DetectCandidates().Where(x=>!x.IsProtected).ToDictionary(x=>x.Definition.Id,x=>true,StringComparer.OrdinalIgnoreCase);}
        public BoostResult StopSelected(IEnumerable<string> selectedIds){return engine.CloseSelected(selectedIds,null);}
        public BoostResult StopSelected(IEnumerable<string> selectedIds,IEnumerable<string> excluded){return engine.CloseSelected(selectedIds,excluded);}
        public BoostResult RestoreStopped(){return engine.Restore();}
        public bool HasRestorableApps {get{return engine.HasRestorable;}}
    }
    internal sealed class BoostAppRow : Control
    {
        private readonly ToolTip evidenceTip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 100 };
        public BoostCandidate Candidate { get; private set; }
        public string AppId { get { return Candidate.Definition.Id; } }
        public bool SelectedForBoost { get; set; }
        public bool Recommended { get { return Candidate.Definition.Recommended; } }

        public BoostAppRow(BoostCandidate candidate)
        {
            Candidate = candidate; Text = candidate.Definition.DisplayName; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            evidenceTip.SetToolTip(this, candidate.Definition.Reason + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, candidate.Evidence));
        }

        protected override void OnClick(EventArgs e) { SelectedForBoost = !SelectedForBoost; Invalidate(); base.OnClick(e); }
        protected override void Dispose(bool disposing) { if (disposing) evidenceTip.Dispose(); base.Dispose(disposing); }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(1, 1, Width - 3, Height - 3), 10))
            using (SolidBrush fill = Synthwave.Brush(Synthwave.Surface(SelectedForBoost ? Color.FromArgb(235, 28, 82, 94) : Color.FromArgb(238, 48, 25, 63), SelectedForBoost ? 39 : 23))) e.Graphics.FillPath(fill, path);
            using (SolidBrush dot = Synthwave.Brush(Recommended ? Color.FromArgb(53, 240, 255) : Color.FromArgb(255, 184, 77))) e.Graphics.FillEllipse(dot, 13, 12, 8, 8);
            using (Font font = new Font(Synthwave.FontFamily, 9.2f, FontStyle.Bold)) Synthwave.DrawText(e.Graphics, Text, font, new Rectangle(29, 3, Width - 166, 21), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string usage = Candidate.CpuPercent.ToString("0.0") + "% CPU · " + Candidate.MemoryText;
            using (Font font = new Font(Synthwave.FontFamily, 7.6f, FontStyle.Bold)) Synthwave.DrawText(e.Graphics, usage, font, new Rectangle(Width - 158, 3, 134, 21), Color.FromArgb(188, 198, 226), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            string detail = (Recommended ? "Optional · " : "Check first · ") + Candidate.Instances + (Candidate.Instances == 1 ? " process · " : " processes · ") + Candidate.Definition.Reason;
            using (Font font = new Font(Synthwave.FontFamily, 7.7f)) Synthwave.DrawText(e.Graphics, detail, font, new Rectangle(13, 25, Width - 43, 18), Recommended ? Color.FromArgb(161, 224, 229) : Color.FromArgb(230, 190, 134), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (SelectedForBoost) using (Pen check = Synthwave.Pen(Color.FromArgb(53, 240, 255), 2f)) e.Graphics.DrawLines(check, new[] { new Point(Width - 28, 24), new Point(Width - 23, 29), new Point(Width - 14, 18) });
        }
    }

    internal sealed class BoostPageControl : Panel
    {
        private readonly BackgroundAppBoost engine = new BackgroundAppBoost();
        private readonly List<BoostAppRow> rows = new List<BoostAppRow>();
        private readonly FlowLayoutPanel candidateList;
        private readonly AccentButton selectButton, stopButton, restoreButton;
        private readonly Label status;
        private bool busy;

        public BoostPageControl()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            RoundedPanel card = new RoundedPanel { Location = new Point(0, 0), Size = new Size(672, 345), BackColor = Color.Transparent, BorderColor = Color.Transparent, CornerRadius = 16 };
            Controls.Add(card);
            card.Controls.Add(MakeLabel("SMART GAME BOOST", new Point(20, 14), 10f, FontStyle.Bold, Color.FromArgb(235, 194, 241), new Size(250, 20)));
            card.Controls.Add(MakeLabel("Ranks optional apps by live CPU + RAM. Windows, drivers, security, Riot and Vanguard stay protected.", new Point(20, 37), 8.2f, FontStyle.Regular, Color.FromArgb(184, 161, 205), new Size(630, 22)));
            candidateList = new BoostCandidatePanel { Location = new Point(20, 65), Size = new Size(632, 181), BackColor = Color.Transparent, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0) };
            card.Controls.Add(candidateList);
            AccentButton scan = MakeButton("Scan again", new Point(20, 257), new Size(112, 38), Color.FromArgb(68, 38, 84));
            selectButton = MakeButton("Select best", new Point(141, 257), new Size(124, 38), Color.FromArgb(67, 46, 94));
            stopButton = MakeButton("Start Boost", new Point(274, 257), new Size(183, 38), Color.FromArgb(26, 103, 108));
            restoreButton = MakeButton("Restore", new Point(466, 257), new Size(186, 38), Color.FromArgb(75, 43, 96));
            scan.Click += (s, e) => Scan(); selectButton.Click += (s, e) => SelectRecommended(); stopButton.Click += (s, e) => StopSelected(); restoreButton.Click += (s, e) => RestoreStopped();
            card.Controls.Add(scan); card.Controls.Add(selectButton); card.Controls.Add(stopButton); card.Controls.Add(restoreButton);
            status = MakeLabel("Ready — nothing is closed automatically.", new Point(20, 307), 8.4f, FontStyle.Regular, Color.FromArgb(183, 170, 205), new Size(630, 22));
            card.Controls.Add(status); Scan();
        }

        // Buffer the whole Boost subtree: ordinary transparent labels and the
        // scrolling panel otherwise erase independently on every animated frame.
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        private static Label MakeLabel(string text, Point location, float size, FontStyle style, Color color, Size bounds) { return new NeonLabel { Text = text, Location = location, Size = bounds, Font = new Font(Synthwave.FontFamily, size, style), ForeColor = color, BackColor = Color.Transparent }; }
        private static AccentButton MakeButton(string text, Point location, Size size, Color accent) { return new AccentButton { Text = text, Location = location, Size = size, SimpleStyle = true, AccentColor = accent, Font = new Font(Synthwave.FontFamily, 8.8f, FontStyle.Bold) }; }

        private void Scan()
        {
            HashSet<string> selected = new HashSet<string>(rows.Where(row => row.SelectedForBoost).Select(row => row.AppId), StringComparer.OrdinalIgnoreCase);
            IList<BoostCandidate> candidates = engine.DetectCandidates().Where(x=>!x.IsProtected).ToList();
            candidateList.SuspendLayout();
            foreach (BoostAppRow oldRow in rows) oldRow.Dispose();
            candidateList.Controls.Clear(); rows.Clear();
            foreach (BoostCandidate candidate in candidates)
            {
                BoostAppRow row = new BoostAppRow(candidate) { Size = new Size(294, 48), Margin = new Padding(0, 0, 8, 6), SelectedForBoost = selected.Contains(candidate.Definition.Id) };
                rows.Add(row); candidateList.Controls.Add(row);
            }
            candidateList.ResumeLayout(); restoreButton.Enabled = engine.HasRestorableApps; stopButton.Enabled = rows.Count > 0; selectButton.Enabled = rows.Any(row => row.Recommended);
            if (!busy) { int memory = candidates.Sum(item => item.MemoryMb); double cpu = candidates.Sum(item => item.CpuPercent); status.Text = rows.Count == 0 ? "No optional background apps detected." : "Found " + rows.Count + " candidates: " + cpu.ToString("0.0") + "% CPU + " + memory.ToString("N0") + " MB RAM. Nothing closes automatically."; }
        }

        private void SelectRecommended()
        {
            if (busy) return; foreach (BoostAppRow row in rows) { row.SelectedForBoost = row.Recommended; row.Invalidate(); }
            int count = rows.Count(row => row.SelectedForBoost); status.Text = count == 0 ? "No recommended candidates are running." : "Selected " + count + " recommended app" + (count == 1 ? "." : "s. Review them, then start Boost.");
        }

        private void StopSelected()
        {
            if (busy) return; string[] selected = rows.Where(row => row.SelectedForBoost).Select(row => row.AppId).ToArray();
            if (selected.Length == 0) { status.Text = "Select at least one candidate first."; return; }
            if (selected.Contains("codex", StringComparer.OrdinalIgnoreCase) && MessageBox.Show(FindForm(), "This will close Codex and interrupt any active chats, terminals or agent work. Close Codex along with the selected apps?", "Close Codex?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            SetBusy(true, "Stopping selected apps safely…");
            Task.Factory.StartNew(() => engine.StopSelected(selected)).ContinueWith(task => { if (IsDisposed) return; BeginInvoke(new Action(() => { busy = false; Scan(); status.Text = task.IsFaulted ? "Boost could not finish." : task.Result.Message; })); });
        }

        private void RestoreStopped()
        {
            if (busy) return; SetBusy(true, "Restoring apps Lucent stopped…");
            Task.Factory.StartNew(() => engine.RestoreStopped()).ContinueWith(task => { if (IsDisposed) return; BeginInvoke(new Action(() => { busy = false; Scan(); status.Text = task.IsFaulted ? "Restore could not finish." : task.Result.Message; })); });
        }

        private void SetBusy(bool value, string message)
        {
            busy = value; selectButton.Enabled = !value && rows.Any(row => row.Recommended); stopButton.Enabled = !value; restoreButton.Enabled = !value && engine.HasRestorableApps; status.Text = message;
        }
    }

    internal sealed class BoostCandidatePanel : FlowLayoutPanel
    {
        public BoostCandidatePanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
    }
}
