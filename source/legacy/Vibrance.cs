using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal struct VibranceLevel
    {
        public int Current, Minimum, Maximum, Default;
        public int FromPercent(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            return percent <= 50
                ? Minimum + (int)Math.Round((Default - (double)Minimum) * percent / 50d)
                : Default + (int)Math.Round((Maximum - (double)Default) * (percent - 50) / 50d);
        }
    }

    internal interface IVibranceDriver
    {
        VibranceLevel Read(string device);
        void Write(string device, int level);
    }

    // Uses the installed NVIDIA driver, with no bundled native DLL or game hooks.
    // DVC is a private NVAPI entry point; unsupported drivers fail closed.
    // ABI references: github.com/falahati/NvAPIWrapper, Native/Delegates/Display.cs,
    // Native/Display/Structures/PrivateDisplayDVCInfoEx.cs and Native/Helpers/FunctionId.cs.
    internal sealed class NvidiaVibranceDriver : IVibranceDriver, IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct DvcInfo
        {
            public uint Version;
            public int Current, Minimum, Maximum, Default;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr QueryInterface(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Lifecycle();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DisplayHandle([MarshalAs(UnmanagedType.LPStr)] string device, out IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DvcOperation(IntPtr handle, uint output, ref DvcInfo info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
        private IntPtr library;
        private QueryInterface query;
        private Lifecycle unload;
        private DisplayHandle findDisplay;
        private DvcOperation get, set;
        private bool initialized;

        public NvidiaVibranceDriver()
        {
            try
            {
                string dll = IntPtr.Size == 8 ? "nvapi64.dll" : "nvapi.dll";
                library = LoadLibraryEx(Path.Combine(Environment.SystemDirectory, dll), IntPtr.Zero, 0x800);
                if (library == IntPtr.Zero) throw new InvalidOperationException("NVIDIA Digital Vibrance is unavailable on this PC.");
                IntPtr entry = GetProcAddress(library, "nvapi_QueryInterface");
                if (entry == IntPtr.Zero) throw new InvalidOperationException("The NVIDIA driver has no NVAPI interface.");
                query = (QueryInterface)Marshal.GetDelegateForFunctionPointer(entry, typeof(QueryInterface));
                Lifecycle initialize = Bind<Lifecycle>(0x0150E828);
                unload = Bind<Lifecycle>(0xD22BDD7E);
                Check(initialize()); initialized = true;
                findDisplay = Bind<DisplayHandle>(0x35C29134);
                get = Bind<DvcOperation>(0x0E45002D);
                set = Bind<DvcOperation>(0x4A82C2B1);
            }
            catch { Dispose(); throw; }
        }
        private T Bind<T>(uint id) where T : class
        {
            IntPtr address = query(id);
            if (address == IntPtr.Zero) throw new InvalidOperationException("This NVIDIA driver does not expose Digital Vibrance.");
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }
        private static void Check(int code)
        {
            if (code != 0) throw new InvalidOperationException("NVIDIA could not access Digital Vibrance (" + code + ").");
        }
        private IntPtr Handle(string device)
        {
            if (library == IntPtr.Zero) throw new ObjectDisposedException("NvidiaVibranceDriver");
            IntPtr handle; Check(findDisplay(device, out handle)); return handle;
        }
        private DvcInfo ReadInfo(IntPtr handle)
        {
            DvcInfo info = new DvcInfo { Version = (uint)Marshal.SizeOf(typeof(DvcInfo)) | 0x10000u };
            Check(get(handle, 0, ref info));
            if (info.Minimum >= info.Maximum || info.Default < info.Minimum || info.Default > info.Maximum || info.Current < info.Minimum || info.Current > info.Maximum)
                throw new InvalidOperationException("The driver returned an unsupported vibrance range.");
            return info;
        }
        public VibranceLevel Read(string device)
        {
            DvcInfo info = ReadInfo(Handle(device));
            return new VibranceLevel { Current = info.Current, Minimum = info.Minimum, Maximum = info.Maximum, Default = info.Default };
        }
        public void Write(string device, int level)
        {
            IntPtr handle = Handle(device);
            DvcInfo info = ReadInfo(handle);
            if (level < info.Minimum || level > info.Maximum) throw new ArgumentOutOfRangeException("level");
            info.Current = level;
            Check(set(handle, 0, ref info));
            if (ReadInfo(handle).Current != level) throw new InvalidOperationException("The driver did not keep the requested vibrance level.");
        }
        public void Dispose()
        {
            if (initialized && unload != null) { unload(); initialized = false; }
            if (library != IntPtr.Zero) { FreeLibrary(library); library = IntPtr.Zero; }
        }
    }

    internal sealed class VibranceSlider : Control
    {
        private int value = 70;
        private bool dragging;
        private bool interactionHeld;
        private Bitmap dragBackdrop;
        private float visualPosition = 70, targetPosition = 70;
        private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer { Interval = 8 };
        private readonly System.Diagnostics.Stopwatch frameClock = new System.Diagnostics.Stopwatch();
        public event EventHandler ValueChanged;
        public event EventHandler DragCompleted;
        public bool IsDragging { get { return dragging; } }
        internal float VisualPercent { get { return visualPosition; } }
        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(0, Math.Min(100, value)); if (this.value == next) return;
                this.value = next; targetPosition = next;
                if (IsHandleCreated && Visible && !dragging) { HoldInteraction();StartAnimation(); }
                else visualPosition = targetPosition;
                Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        public VibranceSlider()
        {
            TabStop = true; Cursor = Cursors.Hand; AccessibleName = "Desktop vibrance"; AccessibleRole = AccessibleRole.Slider;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent;
            animation.Tick += (s, e) => {
                double seconds = Math.Min(.05, frameClock.Elapsed.TotalSeconds); frameClock.Restart();
                visualPosition += (targetPosition - visualPosition) * (float)(1 - Math.Exp(-seconds / (dragging?.012:.025)));
                if (Math.Abs(visualPosition - targetPosition) < .01f) { visualPosition = targetPosition; animation.Stop();if(!dragging)ReleaseInteraction(); }
                Invalidate();Update();
            };
        }
        private void MoveThumb(int x)
        {
            targetPosition = (float)(Math.Max(0, Math.Min(1d, (x - 10d) / Math.Max(1, Width - 20))) * 100);
            if(IsHandleCreated && Visible)StartAnimation();else visualPosition=targetPosition;
            int next = (int)Math.Round(targetPosition);
            if (value != next) { value = next; if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
            Invalidate();
        }
        private void StartAnimation() {if(!animation.Enabled)frameClock.Restart();animation.Start();}
        private void HoldInteraction() {if(interactionHeld)return;interactionHeld=true;ISceneInteractionHost host=FindForm() as ISceneInteractionHost;if(host!=null)host.SetSceneInteraction(this,true);}
        private void ReleaseInteraction() {if(!interactionHeld)return;interactionHeld=false;ISceneInteractionHost host=FindForm() as ISceneInteractionHost;if(host!=null)host.SetSceneInteraction(this,false);if(dragBackdrop!=null){dragBackdrop.Dispose();dragBackdrop=null;}}
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;HoldInteraction();Focus();dragging=true;Capture=true;if(dragBackdrop!=null)dragBackdrop.Dispose();dragBackdrop=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));using(Graphics g=Graphics.FromImage(dragBackdrop))Synthwave.Background(g,this);MoveThumb(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) MoveThumb(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) FinishDrag(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) FinishDrag(); }
        private void FinishDrag()
        {
            if (!dragging) return; dragging = false; Capture = false;
            if(!animation.Enabled)ReleaseInteraction();
            if (DragCompleted != null) DragCompleted(this, EventArgs.Empty);
        }
        protected override bool IsInputKey(Keys keyData) { Keys key = keyData & Keys.KeyCode; return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value--; else if (e.KeyCode == Keys.Right) Value++;
            else if (e.KeyCode == Keys.Home) Value = 0; else if (e.KeyCode == Keys.End) Value = 100;
            else { base.OnKeyDown(e); return; }
            e.SuppressKeyPress = true;
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) {if(dragBackdrop!=null)e.Graphics.DrawImageUnscaled(dragBackdrop,0,0);else Synthwave.Background(e.Graphics,this);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float y = Height / 2f, thumb = 10 + (Width - 20) * visualPosition / 100f;
            using (Pen track = Synthwave.Pen(Synthwave.Surface(Color.FromArgb(92, 53, 114),55), 6)) e.Graphics.DrawLine(track, 10, y, Width - 10, y);
            using (Pen fill = Synthwave.Pen(Enabled ? Color.FromArgb(247, 81, 203) : Color.FromArgb(140, 112, 145), 6)) e.Graphics.DrawLine(fill, 10, y, thumb, y);
            using (SolidBrush brush = Synthwave.Brush(Enabled ? Color.White : Color.Gray)) e.Graphics.FillEllipse(brush, thumb - 8, y - 8, 16, 16);
        }
        protected override void Dispose(bool disposing) { if (disposing) {animation.Dispose();ReleaseInteraction();} base.Dispose(disposing); }
    }
}
