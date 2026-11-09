using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace ResolutionSwitcher
{
    internal static class Brand
    {
        // The Lucent mark: a ring of light with a bead where it opens, on a 32-unit grid.
        // The in-app logo draws the same geometry (Art.Logo), so the icon, tray and window all match.
        internal const float RingStart=-12,RingSweep=294,RingWidth=4.4f,BeadX=23.43f,BeadY=8.57f,BeadRadius=3.1f;
        public static void DrawIcon(Graphics g,RectangleF r)
        {
            var saved=g.Save();
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=Math.Min(r.Width,r.Height)/32f;
            g.TranslateTransform(r.X+(r.Width-32*scale)/2,r.Y+(r.Height-32*scale)/2);
            g.ScaleTransform(scale,scale);
            using(var pen=new Pen(Color.White,RingWidth){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(pen,5.5f,5.5f,21f,21f,RingStart,RingSweep);
            using(var fill=new SolidBrush(Color.White))g.FillEllipse(fill,BeadX-BeadRadius,BeadY-BeadRadius,BeadRadius*2,BeadRadius*2);
            g.Restore(saved);
        }
        public static void DrawBadge(Graphics g,RectangleF r)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=RoundedPanel.RoundedPath(Rectangle.Round(r),(int)(r.Width*.22f)))using(var fill=new SolidBrush(Color.FromArgb(8,8,9)))g.FillPath(fill,path);
            DrawIcon(g,new RectangleF(r.X+r.Width*.16f,r.Y+r.Height*.16f,r.Width*.68f,r.Height*.68f));
        }
        public static void DrawWordmark(Graphics g,RectangleF r)
        {
            using(var font=new Font(Synthwave.FontFamily,Math.Max(8,r.Height*.3f),FontStyle.Bold))using(var b=new SolidBrush(Color.White))using(var sf=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("Lucent",font,b,r,sf);
        }
    }
    internal sealed class BrandWordmark:Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            Brand.DrawWordmark(e.Graphics,ClientRectangle);
        }
    }
}
