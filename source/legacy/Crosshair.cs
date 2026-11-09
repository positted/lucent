using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal static class CrosshairArt
    {
        // The old entry point: a shape name and one size. It maps onto the same renderer as everything else.
        public static void Draw(Graphics g, Rectangle bounds, string shape, Color color, float scale, bool outline = true, Color? outlineColor = null)
        {
            var look = Lucent.CrosshairPresets.FromLegacy(shape, scale, Lucent.CrosshairColors.Format(color), outline, Lucent.CrosshairColors.Format(outlineColor ?? Color.Black));
            Lucent.CrosshairRenderer.Draw(g, bounds, look);
        }

        public static float DotRadius(float scale)
        {
            return Math.Max(1.5f, scale * 2f + .5f);
        }
    }
    internal sealed class CrosshairOverlay : Form
    {
        private Lucent.CrosshairSettings look = Lucent.CrosshairPresets.Create("Gap cross");
        public CrosshairOverlay()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            // This is a display-only window, including its opaque crosshair pixels.
            Enabled = false;
            TabStop = false;
            BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
            Bounds = TargetScreen().Bounds;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        // The display the crosshair is drawn on, by its Windows device name; the main display when none is chosen or the chosen one is not connected.
        private string monitor = "";
        public string Monitor { get { return monitor; } }
        public void SetMonitor(string deviceName)
        {
            monitor = deviceName ?? "";
            if (IsDisposed || Disposing) return;
            RefreshDisplayBounds();
        }
        private Screen TargetScreen()
        {
            if (!string.IsNullOrEmpty(monitor))
                foreach (Screen screen in Screen.AllScreens)
                    if (string.Equals(screen.DeviceName, monitor, StringComparison.OrdinalIgnoreCase)) return screen;
            return Screen.PrimaryScreen;
        }
        public void RefreshDisplayBounds()
        {
            Rectangle current = TargetScreen().Bounds;
            if (Bounds != current) { Bounds = current; Invalidate(); }
        }
        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed && !Disposing) RefreshDisplayBounds(); })); }
            catch (InvalidOperationException) { }
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            if (Visible) RefreshDisplayBounds();
            base.OnVisibleChanged(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.Dispose(disposing);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x08000000; // WS_DISABLED: never receive mouse or keyboard input.
                cp.ExStyle |= 0x00080000 | 0x20 | 0x80 | 0x08000000; // Layered, transparent, tool window, no activation.
                return cp;
            }
        }
        public void Configure(string newShape, Color newColor, float newScale, bool newOutline = true, Color? newOutlineColor = null)
        {
            Configure(Lucent.CrosshairPresets.FromLegacy(newShape, newScale, Lucent.CrosshairColors.Format(newColor), newOutline, Lucent.CrosshairColors.Format(newOutlineColor ?? Color.Black)));
        }
        public void Configure(Lucent.CrosshairSettings settings)
        {
            look = settings.Copy();
            Color fill = Lucent.CrosshairColors.Parse(look.FillColor, Color.White), edge = Lucent.CrosshairColors.Parse(look.OutlineColor, Color.Black);
            // The window's transparent color must differ from every color the crosshair uses.
            Color key = (fill.ToArgb() == Color.Magenta.ToArgb() || edge.ToArgb() == Color.Magenta.ToArgb()) ? Color.FromArgb(1, 2, 3) : Color.Magenta;
            while (key.ToArgb() == fill.ToArgb() || key.ToArgb() == edge.ToArgb()) key = Color.FromArgb(1, 2, (key.B + 1) % 256);
            BackColor = key; TransparencyKey = key;
            Opacity = look.Opacity / 100d;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) { Lucent.CrosshairRenderer.Draw(e.Graphics, ClientRectangle, look); }        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) { m.Result = (IntPtr)(-1); return; } // HTTRANSPARENT
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE; do not discard the click.
            base.WndProc(ref m);
        }
    }

    internal sealed class CrosshairPreview : Control
    {
        private string shape = "Classic"; private Color color = Color.White; private float scale = 0.6f; private bool outline = true;
        public string Shape { get { return shape; } set { shape = value; Invalidate(); } }
        public Color CrosshairColor { get { return color; } set { color = value; Invalidate(); } }
        public float DisplayScale { get { return scale; } set { scale = value; Invalidate(); } }
        public bool Outline { get { return outline; } set { outline = value; Invalidate(); } }
        public CrosshairPreview() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(1, 1, Width - 3, Height - 3), 18))
            using (LinearGradientBrush fill = Synthwave.Gradient(ClientRectangle, Synthwave.Surface(Color.FromArgb(135, 38, 10, 65),16), Synthwave.Surface(Color.FromArgb(115, 12, 78, 96),23), 45f))
            using (Pen edge = Synthwave.Pen(Color.FromArgb(135, 74, 227, 238))) { e.Graphics.FillPath(fill, path); }
            CrosshairArt.Draw(e.Graphics, ClientRectangle, shape, color, scale, outline);
        }
    }

    internal sealed class NeonColorSwatch : Control
    {
        public Color SwatchColor { get; set; }
        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public NeonColorSwatch() { Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circle = new Rectangle(5, 5, Width - 11, Height - 11);
            using (SolidBrush b = Synthwave.Brush(SwatchColor)) e.Graphics.FillEllipse(b, circle);
            if (Selected) using (Pen p = Synthwave.Pen(Color.FromArgb(225, 50, 224, 255), 1.2f)) e.Graphics.DrawEllipse(p, 2, 2, Width - 5, Height - 5);
        }
    }

    internal sealed class MiamiTab : Control
    {
        public bool Selected { get; set; }
        public MiamiTab(string text) { Text = text; Size = new Size(118, 39); Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Selected) using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 10)) using (SolidBrush b = Synthwave.Brush(Synthwave.Surface(Color.FromArgb(112, 122, 26, 126),31))) using (Pen p = Synthwave.Pen(Color.FromArgb(210, 255, 73, 203))) { e.Graphics.FillPath(b, path); }
            using(Font font=new Font(Synthwave.FontFamily,9.5f,Selected?FontStyle.Bold:FontStyle.Regular)) Synthwave.Text(e.Graphics,Text,font,ClientRectangle,Selected?Color.White:Color.FromArgb(195,170,207),true);
        }
    }

    internal sealed class DecimalEntry : Control
    {
        private string editText = "0.6"; private float value = 0.6f; private bool error;
        public event EventHandler ValueChanged;
        public float Value { get { return value; } set { this.value = Math.Max(.1f, Math.Min(20f, value)); editText = this.value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); Invalidate(); } }
        public DecimalEntry() { TabStop = true; Cursor = Cursors.IBeam; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); SelectAllText(); base.OnMouseDown(e); }
        private void SelectAllText() { editText = ""; error = false; Invalidate(); }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar) && editText.Length < 4) editText += e.KeyChar;
            else if ((e.KeyChar == '.' || e.KeyChar == ',') && !editText.Contains(".")) editText += ".";
            else if (e.KeyChar == '\b' && editText.Length > 0) editText = editText.Substring(0, editText.Length - 1);
            else if (e.KeyChar == '\r') Commit();
            e.Handled = true; Invalidate();
        }
        protected override void OnLostFocus(EventArgs e) { Commit(); base.OnLostFocus(e); }
        private void Commit()
        {
            float parsed;
            if (!float.TryParse(editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) || parsed < .1f || parsed > 20f) { error = true; editText = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); Invalidate(); return; }
            value = parsed; editText = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); error = false;
            EventHandler handler = ValueChanged; if (handler != null) handler(this, EventArgs.Empty); Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 8))
            using (SolidBrush fill = Synthwave.Brush(error ? Color.FromArgb(235, 110, 28, 54) : Synthwave.Surface(Focused ? Color.FromArgb(245, 83, 39, 103) : Color.FromArgb(235, 65, 24, 79), Focused ? 31 : 22)))
            using (Pen edge = Synthwave.Pen(error ? Color.FromArgb(255, 90, 110) : Focused ? Color.FromArgb(50, 224, 255) : Color.FromArgb(190, 60, 168))) { e.Graphics.FillPath(fill, path); }
            Synthwave.Text(e.Graphics, (Focused && editText.Length == 0) ? "Type 0.1 – 20.0" : editText + (Focused ? "" : "×"), Font, new RectangleF(12, 0, Width - 24, Height), Color.White, false);
        }
    }

    internal sealed class HotkeyCapture : Control
    {
        private Keys hotkey = Keys.Control | Keys.Alt | Keys.C; private bool error;
        public Color AccentColor { get; set; }
        public event EventHandler HotkeyChanged;
        public Keys Hotkey { get { return hotkey; } set { hotkey = value; error = false; Invalidate(); } }
        public HotkeyCapture() { AccentColor = Color.FromArgb(50, 224, 255); Cursor = Cursors.IBeam; TabStop = true; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); }
        public void ShowError() { error = true; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); error = false; Invalidate(); base.OnMouseDown(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true; Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu) return;
            if (!((key >= Keys.F1 && key <= Keys.F24) || ((e.Control || e.Alt) && key != Keys.None))) { error = true; Invalidate(); return; }
            hotkey = e.KeyData; error = false; EventHandler handler = HotkeyChanged; if (handler != null) handler(this, EventArgs.Empty); Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 9))
            using (SolidBrush fill = Synthwave.Brush(error ? Color.FromArgb(235, 110, 28, 54) : Synthwave.Surface(Focused ? Color.FromArgb(245, 45, 62, 94) : Color.FromArgb(235, 35, 35, 68), Focused ? 31 : 22)))
            using (Pen edge = Synthwave.Pen(error ? Color.FromArgb(255, 90, 110) : Focused ? AccentColor : Color.FromArgb(79, 162, 189))) { e.Graphics.FillPath(fill, path); }
            string text = error ? "Key unavailable — try another" : Focused ? "Press your new shortcut…" : "Toggle key:  " + (hotkey==Keys.None ? "Unbound" : new KeysConverter().ConvertToString(hotkey));
            using(Font font=new Font(Synthwave.FontFamily,9f,FontStyle.Bold))Synthwave.Text(e.Graphics,text,font,ClientRectangle,Color.White,true);
        }
    }
}


