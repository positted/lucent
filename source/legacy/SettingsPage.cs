using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class SettingsPageControl : UserControl
    {
        private readonly SwitcherSettings settings;
        private readonly InlineBinding switchBind, crosshairBind;
        public bool IsCapturing { get { return switchBind.Capturing || crosshairBind.Capturing; } }
        public static string BindingText(int key) { return key==0 ? "Unbound" : new KeysConverter().ConvertToString((Keys)key); }
        public SettingsPageControl(SwitcherSettings settings, Action changed, Func<bool,Keys,bool> bind, Action close)
        {
            this.settings=settings; BackColor=Color.Transparent; Size=new Size(624,366);
            AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
            Label("Settings",new Rectangle(24,12,320,34),20,true,Controls);
            Label("Your look. Your shortcuts.",new Rectangle(26,48,360,23),9,false,Controls);
            AccentButton dismiss=new AccentButton {Text="×",SimpleStyle=true,Bounds=new Rectangle(576,16,28,28),Font=new Font(Synthwave.FontFamily,12),AccessibleName="Close Settings"};dismiss.Click+=(s,e)=>close();Controls.Add(dismiss);
            RoundedPanel appearance=Card(new Rectangle(20,86,258,242));
            RoundedPanel shortcuts=Card(new Rectangle(292,86,312,242));
            Label("APPEARANCE",new Rectangle(16,12,226,25),9,true,appearance.Controls);
            SettingsToggle background=ToggleRow(appearance,"Background","Sun, stars and grid",47,settings.BackgroundEnabled);
            SettingsToggle motion=ToggleRow(appearance,"Motion","Keep the scene moving",111,settings.BackgroundMotion);
            SettingsToggle comets=ToggleRow(appearance,"Comets","A little glow every 30 seconds",175,settings.CometsEnabled);
            Action refresh=()=>{motion.Enabled=background.Checked;comets.Enabled=background.Checked && motion.Checked;};
            EventHandler update=(s,e)=>{
                settings.BackgroundEnabled=background.Checked; settings.BackgroundMotion=motion.Checked; settings.CometsEnabled=comets.Checked;
                refresh();changed();
            };
            background.CheckedChanged+=update; motion.CheckedChanged+=update; comets.CheckedChanged+=update; refresh();
            Label("KEY BINDS",new Rectangle(16,12,280,25),9,true,shortcuts.Controls);
            Label("Switch resolution",new Rectangle(16,46,280,24),10,true,shortcuts.Controls);
            Label("Toggle crosshair",new Rectangle(16,135,280,24),10,true,shortcuts.Controls);
            switchBind=new InlineBinding {Bounds=new Rectangle(16,77,178,38),Hotkey=(Keys)settings.SwitchHotkey,AccessibleName="Resolution switch bind",TryBind=key=>bind(false,key)};
            crosshairBind=new InlineBinding {Bounds=new Rectangle(16,166,178,38),Hotkey=(Keys)settings.CrosshairHotkey,AccessibleName="Crosshair bind",TryBind=key=>bind(true,key)};
            shortcuts.Controls.Add(switchBind); shortcuts.Controls.Add(crosshairBind);
            UnbindButton(shortcuts,switchBind,77); UnbindButton(shortcuts,crosshairBind,166);
            Label("Click a bind. Esc cancels.",new Rectangle(16,216,280,22),8.3f,false,shortcuts.Controls);
            Label("Changes save automatically",new Rectangle(26,337,420,22),8.5f,false,Controls);
        }
        public void RefreshBindings() {switchBind.Hotkey=(Keys)settings.SwitchHotkey;crosshairBind.Hotkey=(Keys)settings.CrosshairHotkey;}
        public void BeginBinding(bool crosshair) {(crosshair?crosshairBind:switchBind).BeginCapture();}
        public void CancelCapture() {switchBind.CancelCapture();crosshairBind.CancelCapture();}
        private RoundedPanel Card(Rectangle bounds) {RoundedPanel card=new RoundedPanel {Bounds=bounds,BackColor=Color.Transparent,CornerRadius=16};Controls.Add(card);return card;}
        private static void Label(string text,Rectangle bounds,float size,bool bold,Control.ControlCollection controls)
        {
            controls.Add(new NeonLabel {Text=text,Bounds=bounds,BackColor=Color.Transparent,Font=new Font(Synthwave.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=bold?Color.White:Color.FromArgb(174,174,174)});
        }
        private static SettingsToggle ToggleRow(RoundedPanel card,string title,string description,int top,bool value)
        {
            Label(title,new Rectangle(16,top,190,24),10,true,card.Controls);
            Label(description,new Rectangle(16,top+27,226,23),8.2f,false,card.Controls);
            SettingsToggle toggle=new SettingsToggle {Bounds=new Rectangle(206,top,36,28),Checked=value,AccessibleName=title};card.Controls.Add(toggle);return toggle;
        }
        private static void UnbindButton(RoundedPanel card,InlineBinding field,int top)
        {
            AccentButton button=new AccentButton {Text="Unbind",SimpleStyle=true,Bounds=new Rectangle(204,top,88,38),Font=new Font(Synthwave.FontFamily,8.5f)};
            button.Click+=(s,e)=>field.ClearBinding();card.Controls.Add(button);
        }
        protected override void OnPaintBackground(PaintEventArgs e) {Synthwave.Background(e.Graphics,this);}
    }
    internal sealed class SettingsOverlay : Control, IThemeSurface
    {
        private Bitmap backdrop;
        private readonly Action close;
        private readonly PopupCard card;
        public Control.ControlCollection Content {get{return card.Controls;}}
        public SettingsOverlay(Bitmap backdrop,Action close)
        {
            this.backdrop=backdrop;this.close=close;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            card=new PopupCard();Controls.Add(card);
        }
        protected override CreateParams CreateParams {get{CreateParams value=base.CreateParams;value.ExStyle|=0x02000000;return value;}}
        public void SetBackdrop(Bitmap image) {Bitmap old=backdrop;backdrop=image;if(old!=null)old.Dispose();Invalidate(true);}
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);if(card==null)return;
            float scale=Math.Min(Width/720f,Height/466f);card.Bounds=new Rectangle((int)((Width-624*scale)/2),(int)((Height-366*scale)/2),(int)(624*scale),(int)(366*scale));
        }
        public void PaintSurface(Graphics graphics)
        {
            graphics.DrawImageUnscaled(backdrop,0,0);using(SolidBrush shade=new SolidBrush(Color.FromArgb(175,Color.Black)))graphics.FillRectangle(shade,ClientRectangle);
        }
        protected override void OnPaintBackground(PaintEventArgs e) {PaintSurface(e.Graphics);}
        protected override void OnMouseDown(MouseEventArgs e) {if(!card.Bounds.Contains(e.Location))close();base.OnMouseDown(e);}
        protected override void Dispose(bool disposing) {if(disposing)backdrop.Dispose();base.Dispose(disposing);}
    }
    internal sealed class PopupCard : Panel, IThemeSurface
    {
        public PopupCard() {DoubleBuffered=true;BackColor=Color.Transparent;}
        public void PaintSurface(Graphics graphics)
        {
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath path=RoundedPanel.RoundedPath(new Rectangle(0,0,Width-1,Height-1),18))
            using(SolidBrush fill=new SolidBrush(Color.FromArgb(18,18,18)))
            using(Pen edge=new Pen(Color.FromArgb(55,55,55))) {graphics.FillPath(fill,path);graphics.DrawPath(edge,path);}
        }
        protected override void OnPaintBackground(PaintEventArgs e) {Synthwave.Background(e.Graphics,this);}
        protected override void OnPaint(PaintEventArgs e) {PaintSurface(e.Graphics);base.OnPaint(e);}
    }
    internal sealed class InlineBinding : Control
    {
        private Keys hotkey;
        private bool error;
        public bool Capturing { get; private set; }
        public Func<Keys,bool> TryBind;
        public Keys Hotkey {get{return hotkey;}set{hotkey=value;error=false;Invalidate();}}
        public InlineBinding()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PushButton;Font=new Font(Synthwave.FontFamily,10,FontStyle.Bold);
        }
        public void BeginCapture() {Focus();Capturing=true;error=false;Invalidate();}
        public void CancelCapture() {Capturing=false;error=false;Invalidate();}
        public void ClearBinding() {CancelCapture();Commit(Keys.None);}
        private void Commit(Keys key)
        {
            if(TryBind!=null && TryBind(key)) {hotkey=key;Capturing=false;error=false;AccessibleDescription=SettingsPageControl.BindingText((int)key);}
            else error=true;
            Invalidate();
        }
        protected override void OnClick(EventArgs e) {BeginCapture();base.OnClick(e);}
        protected override void OnLostFocus(EventArgs e) {CancelCapture();base.OnLostFocus(e);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(!Capturing) {if(e.KeyCode==Keys.Enter || e.KeyCode==Keys.Space) {e.SuppressKeyPress=true;BeginCapture();}base.OnKeyDown(e);return;}
            e.SuppressKeyPress=true;
            Keys key=e.KeyCode;
            if(key==Keys.Escape) {CancelCapture();return;}
            if(key==Keys.ControlKey || key==Keys.ShiftKey || key==Keys.Menu) return;
            if(key==Keys.Delete || key==Keys.Back) {ClearBinding();return;}
            if((key>=Keys.F1 && key<=Keys.F24) || ((e.Control || e.Alt) && key!=Keys.None)) Commit(e.KeyData);
            else {error=true;Invalidate();}
        }
        protected override void OnPaintBackground(PaintEventArgs e) {Synthwave.Background(e.Graphics,this);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath path=RoundedPanel.RoundedPath(new Rectangle(0,0,Width-1,Height-1),10))
            using(SolidBrush fill=new SolidBrush(Color.FromArgb(Capturing?39:24,Capturing?39:24,Capturing?39:24)))
            using(Pen edge=new Pen(Color.FromArgb(Capturing?145:Focused?70:34,Capturing?145:Focused?70:34,Capturing?145:Focused?70:34))) {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(edge,path);}
            string text=error?"Shortcut unavailable":Capturing?"Press a shortcut…":SettingsPageControl.BindingText((int)hotkey);
            Synthwave.Text(e.Graphics,text,Font,ClientRectangle,hotkey==Keys.None&&!Capturing?Color.LightGray:Color.White,true);
        }
    }
    internal sealed class SettingsToggle : Control
    {
        private bool value;
        public event EventHandler CheckedChanged;
        public bool Checked {get{return value;}set{if(this.value==value)return;this.value=value;Invalidate();AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
        public SettingsToggle() {SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnClick(EventArgs e) {Focus();Checked=!Checked;base.OnClick(e);}
        protected override void OnKeyDown(KeyEventArgs e) {if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter) {e.SuppressKeyPress=true;OnClick(EventArgs.Empty);}base.OnKeyDown(e);}
        protected override void OnEnabledChanged(EventArgs e) {Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaintBackground(PaintEventArgs e) {Synthwave.Background(e.Graphics,this);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath path=RoundedPanel.RoundedPath(new Rectangle(1,5,34,18),9))
            using(SolidBrush track=new SolidBrush(!Enabled?Color.FromArgb(52,52,52):Checked?Color.FromArgb(232,232,232):Color.FromArgb(62,62,62)))
            using(SolidBrush knob=new SolidBrush(!Enabled?Color.Gray:Checked?Color.FromArgb(30,30,30):Color.FromArgb(205,205,205)))
            {e.Graphics.FillPath(track,path);e.Graphics.FillEllipse(knob,Checked?20:4,8,12,12);if(Focused)using(Pen focus=new Pen(Color.Gray))e.Graphics.DrawPath(focus,path);}
        }
    }
}
