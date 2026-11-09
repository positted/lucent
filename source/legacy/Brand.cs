using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    // Native vector versions of the selected 01 icon and 03 wordmark concepts.
    internal static class Brand
    {
        public static void DrawBadge(Graphics graphics, RectangleF bounds)
        {
            float size = Math.Min(bounds.Width, bounds.Height);
            RectangleF circle = new RectangleF(bounds.X + (bounds.Width-size)/2, bounds.Y + (bounds.Height-size)/2, size, size);
            GraphicsState state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush background = new SolidBrush(Color.FromArgb(29,29,29))) graphics.FillEllipse(background,circle);
            float inset = size * .18f;
            DrawIcon(graphics,new RectangleF(circle.X+inset,circle.Y+inset,size-2*inset,size-2*inset));
            graphics.Restore(state);
        }
        public static void DrawIcon(Graphics graphics, RectangleF bounds)
        {
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 100f, bounds.Height / 112f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = new GraphicsPath())
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                path.StartFigure(); path.AddLine(10, 102, 10, 38);
                path.AddBezier(10, 38, 10, 17, 24, 8, 44, 8);
                path.AddLine(44, 8, 85, 8); path.AddBezier(85, 8, 89, 8, 90, 10, 90, 14);
                path.AddLine(90, 14, 90, 30); path.AddBezier(90, 30, 90, 34, 88, 36, 84, 36);
                path.AddLine(84, 36, 53, 36); path.AddBezier(53, 36, 38, 36, 32, 44, 32, 59);
                path.AddLine(32, 59, 32, 102); path.CloseFigure();
                path.StartFigure(); path.AddLine(49, 44, 89, 44);
                path.AddBezier(89, 44, 81, 59, 67, 68, 49, 68); path.CloseFigure();
                graphics.FillPath(white, path);
            }
            graphics.Restore(state);
        }
        public static void DrawWordmark(Graphics graphics, RectangleF bounds)
        {
            const float width = 316, height = 128;
            float scale = Math.Min(bounds.Width / width, bounds.Height / height);
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(bounds.X + (bounds.Width - width * scale) / 2, bounds.Y + (bounds.Height - height * scale) / 2);
            graphics.ScaleTransform(scale, scale); graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush white = new SolidBrush(Color.White))
            using (GraphicsPath path = new GraphicsPath(FillMode.Alternate))
            {
                // r
                path.StartFigure(); path.AddLine(0, 100, 0, 57); path.AddBezier(0, 57, 0, 39, 12, 30, 30, 30);
                path.AddLine(30, 30, 50, 30); path.AddBezier(50,30,54,30,55,33,55,37); path.AddLine(55,37,55,46);
                path.AddBezier(55,46,55,50,53,51,49,51); path.AddLine(49,51,33,51);
                path.AddBezier(33,51,23,51,21,58,21,68); path.AddLine(21,68,21,100); path.CloseFigure();
                path.StartFigure(); path.AddLine(28,58,54,58); path.AddBezier(54,58,47,69,39,72,28,72); path.CloseFigure();
                // e: open lower-right counter, with a separate upper counter.
                path.StartFigure(); path.AddLine(132,72,85,72); path.AddBezier(85,72,88,87,106,89,118,78);
                path.AddLine(118,78,131,89); path.AddBezier(131,89,114,109,80,108,67,85);
                path.AddBezier(67,85,48,52,77,19,108,31); path.AddBezier(108,31,127,37,134,52,132,72); path.CloseFigure();
                path.StartFigure(); path.AddLine(85,57,112,57); path.AddBezier(112,57,109,42,89,42,85,57); path.CloseFigure();
                // s
                path.StartFigure(); path.AddLine(199,30,163,30); path.AddBezier(163,30,132,30,132,71,160,72);
                path.AddLine(160,72,179,72); path.AddBezier(179,72,188,72,188,82,179,82); path.AddLine(179,82,145,82);
                path.AddLine(145,82,145,101); path.AddLine(145,101,181,101); path.AddBezier(181,101,213,101,213,60,182,59);
                path.AddLine(182,59,164,59); path.AddBezier(164,59,155,59,155,49,164,49); path.AddLine(164,49,199,49); path.CloseFigure();
                // l
                path.StartFigure(); path.AddLine(217,4,217,100); path.AddLine(217,100,239,100); path.AddLine(239,100,239,4);
                path.AddBezier(239,4,239,1,237,0,233,0); path.AddLine(233,0,223,0); path.AddBezier(223,0,219,0,217,1,217,4); path.CloseFigure();
                // y
                path.StartFigure(); path.AddLine(251,31,251,69); path.AddBezier(251,69,251,96,274,104,295,94);
                path.AddBezier(295,94,295,107,284,109,267,109); path.AddLine(267,109,267,128);
                path.AddBezier(267,128,301,128,316,115,316,88); path.AddLine(316,88,316,31); path.AddLine(316,31,295,31);
                path.AddLine(295,31,295,68); path.AddBezier(295,68,295,87,272,87,272,68); path.AddLine(272,68,272,31); path.CloseFigure();
                graphics.FillPath(white, path);
            }
            graphics.Restore(state);
        }
    }
    internal sealed class BrandWordmark : Control
    {
        private Bitmap image;
        public BrandWordmark()
        {
            AccessibleName = "Lucent"; AccessibleRole = AccessibleRole.StaticText;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1) return;
            if (image == null || image.Width != Width || image.Height != Height)
            {
                if (image != null) image.Dispose();
                using (Bitmap large = new Bitmap(Width * 3, Height * 3))
                {
                    using (Graphics graphics = Graphics.FromImage(large)) Brand.DrawWordmark(graphics, new RectangleF(0,0,large.Width,large.Height));
                    image = Synthwave.Downsample(large, Width, Height);
                }
            }
            e.Graphics.DrawImageUnscaled(image,0,0);
        }
        protected override void Dispose(bool disposing) { if (disposing && image != null) image.Dispose(); base.Dispose(disposing); }
    }
}
