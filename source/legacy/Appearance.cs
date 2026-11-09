using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class GearButton : Control
    {
        private readonly Bitmap icon;
        public GearButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent; Text=""; TabStop=true;
            AccessibleName="Settings"; AccessibleRole=AccessibleRole.PushButton; Cursor=Cursors.Hand;
            using(Bitmap large=new Bitmap(60,60)) {using(Graphics graphics=Graphics.FromImage(large)) TrayGlyph.Draw(graphics,"settings",new Rectangle(0,0,60,60),Color.FromArgb(230,230,230)); icon=Synthwave.Downsample(large,20,20);}
            MouseEnter+=(s,e)=>Invalidate(); MouseLeave+=(s,e)=>Invalidate(); GotFocus+=(s,e)=>Invalidate(); LostFocus+=(s,e)=>Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) Focus(); base.OnMouseDown(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter) {e.SuppressKeyPress=true; OnClick(EventArgs.Empty);} base.OnKeyDown(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics,this); }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.DrawImageUnscaled(icon,(Width-20)/2,(Height-20)/2); }
        protected override void Dispose(bool disposing) {if(disposing) icon.Dispose();base.Dispose(disposing);}
    }
    internal sealed class LucentTrayMenu : ContextMenuStrip
    {
        private readonly TrayHeading heading;
        public LucentTrayMenu()
        {
            BackColor=Color.FromArgb(39,39,39); ForeColor=Color.White; Font=new Font(Synthwave.FontFamily,10.5f,FontStyle.Regular);
            Renderer=new TrayRenderer(); ShowImageMargin=true; ShowCheckMargin=false;
            Padding=new Padding(0,2,0,2); AccessibleName="Lucent tray menu";
            MinimumSize=new Size(300,0);
            AutoSize=false; Size=new Size(300,64);
            Opacity=.96;
            heading=new TrayHeading(); Items.Add(heading); Items.Add(new ToolStripSeparator());
            Opening+=(s,e)=>{
                int height=Padding.Vertical;
                foreach(ToolStripItem item in Items) {item.AutoSize=false;item.Size=new Size(300,item is TrayHeading?56:item is ToolStripSeparator?6:38);height+=item.Height;}
                Size=new Size(300,height);
            };
        }
        public void SetStatus(string status) { heading.Status=status; heading.Invalidate(); }
        public void AddAction(string text,string glyph,Action action)
        {
            Bitmap image;
            using(Bitmap large=new Bitmap(80,80))
            { using(Graphics graphics=Graphics.FromImage(large)) TrayGlyph.Draw(graphics,glyph,new Rectangle(0,0,80,80),Color.White); image=Synthwave.Downsample(large,20,20); }
            ToolStripMenuItem item=new ToolStripMenuItem(text,image,(s,e)=>action()) {Padding=new Padding(0,6,0,6),AccessibleName=text,ImageScaling=ToolStripItemImageScaling.None,Tag=glyph};
            Items.Add(item);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) foreach(ToolStripItem item in Items) if(item.Image!=null) item.Image.Dispose();
            base.Dispose(disposing);
        }
    }
    internal sealed class TrayHeading : ToolStripMenuItem
    {
        public string Status="Display controls";
        public TrayHeading() {Text="Display status"; Enabled=false; AutoSize=false; Size=new Size(292,56); AccessibleName="Lucent display status";}
        protected override void OnPaint(PaintEventArgs e)
        {
            Brand.DrawIcon(e.Graphics,new RectangleF(23,17,18,20));
            using(Font font=new Font(Synthwave.FontFamily,9.2f,FontStyle.Regular))
                Synthwave.DrawText(e.Graphics,Status,font,new Rectangle(49,0,Width-65,Height),Color.White,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding,4);
        }
    }
    internal sealed class TrayRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Color.FromArgb(39,39,39)); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if(e.Item.Selected && e.Item.Enabled)
                using(SolidBrush brush=new SolidBrush(Color.FromArgb(53,53,53))) e.Graphics.FillRectangle(brush,new Rectangle(0,0,e.Item.Width,e.Item.Height));
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Synthwave.DrawText(e.Graphics,e.Text,e.TextFont,new Rectangle(49,0,e.Item.Width-66,e.Item.Height),e.Item.Enabled?Color.White:Color.Gray,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding,4);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) {if(e.Image!=null)e.Graphics.DrawImageUnscaled(e.Image,21,(e.Item.Height-20)/2);}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using(Pen pen=new Pen(Color.FromArgb(65,65,65))) e.Graphics.DrawLine(pen,0,e.Item.Height/2,e.Item.Width,e.Item.Height/2);
        }
    }
    internal static class TrayGlyph
    {
        public static void Draw(Graphics graphics,string glyph,Rectangle bounds,Color color)
        {
            GraphicsState state=graphics.Save(); graphics.TranslateTransform(bounds.X,bounds.Y); graphics.ScaleTransform(bounds.Width/20f,bounds.Height/20f);
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(Pen pen=new Pen(color,1.4f))
            {
                pen.StartCap=pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;
                if(glyph=="settings")
                {
                    PointF[] points=new PointF[32];
                    for(int i=0;i<32;i++) { double angle=i*Math.PI/16; float radius=i%4==0 || i%4==3?9:7.2f; points[i]=new PointF(10+radius*(float)Math.Cos(angle),10+radius*(float)Math.Sin(angle)); }
                    graphics.DrawPolygon(pen,points); graphics.DrawEllipse(pen,6.7f,6.7f,6.6f,6.6f);
                }
                else if(glyph=="video") { graphics.DrawRectangle(pen,2,3,16,11); graphics.DrawLine(pen,10,14,10,18); graphics.DrawLine(pen,6,18,14,18); }
                else if(glyph=="crosshair") { graphics.DrawEllipse(pen,4,4,12,12); graphics.DrawLine(pen,10,1,10,6); graphics.DrawLine(pen,10,14,10,19); graphics.DrawLine(pen,1,10,6,10); graphics.DrawLine(pen,14,10,19,10); }
                else if(glyph=="boost") { graphics.DrawLines(pen,new[]{new Point(12,1),new Point(4,11),new Point(10,11),new Point(8,19),new Point(16,8),new Point(10,8),new Point(12,1)}); }
                else if(glyph=="switch")
                {
                    // Expansion corners and the Lucent mark form one compact glyph.
                    graphics.DrawLines(pen,new[]{new Point(2,7),new Point(2,2),new Point(7,2)});
                    graphics.DrawLines(pen,new[]{new Point(13,2),new Point(18,2),new Point(18,7)});
                    graphics.DrawLines(pen,new[]{new Point(18,13),new Point(18,18),new Point(13,18)});
                    graphics.DrawLines(pen,new[]{new Point(7,18),new Point(2,18),new Point(2,13)});
                    Brand.DrawIcon(graphics,new RectangleF(5.4f,4.85f,9.2f,10.3f));
                }
                else if(glyph=="key") { graphics.DrawRectangle(pen,1,5,18,11); for(int x=4;x<18;x+=4) graphics.DrawLine(pen,x,8,x,9); graphics.DrawLine(pen,5,13,15,13); }
                else if(glyph=="quit") { graphics.DrawArc(pen,3,3,14,14,-55,290); graphics.DrawLine(pen,10,1,10,10); }
            }
            graphics.Restore(state);
        }
    }
}
