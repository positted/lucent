using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal interface IThemeSurface { void PaintSurface(Graphics graphics); }
    internal interface ISceneInteractionHost {void SetSceneInteraction(object source,bool active);}
    internal static class Synthwave
    {
        public static float Phase = 0;
        private static readonly System.Drawing.Text.PrivateFontCollection privateFonts=new System.Drawing.Text.PrivateFontCollection(); public static readonly System.Drawing.FontFamily FontFamily=ChooseFont();
        private static System.Drawing.FontFamily ChooseFont() {try{string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts");foreach(string path in System.IO.Directory.GetFiles(folder,"Inter-*.ttf"))privateFonts.AddFontFile(path);if(privateFonts.Families.Length>0)return privateFonts.Families[0];}catch{}return new System.Drawing.FontFamily("Segoe UI");}
        public static bool BackgroundEnabled { get; private set; }
        public static bool BackgroundMotion { get; private set; }
        public static bool CometsEnabled { get; private set; }
        static Synthwave() { BackgroundEnabled=BackgroundMotion=CometsEnabled=true; }
        public static void ConfigureBackground(bool background, bool motion, bool comets)
        {
            BackgroundEnabled=background; BackgroundMotion=motion; CometsEnabled=comets;
            renderedPhase=float.NaN;
        }
        public static bool DarkMode { get { return true; } }
        public static Color Surface(Color light, int dark = 20) { return DarkMode ? Color.FromArgb(light.A, dark, dark, dark) : light; }
        public static Color Ink(Color color)
        {
            if (!DarkMode) return color;
            int value = (color.R * 299 + color.G * 587 + color.B * 114 + 500) / 1000;
            return Color.FromArgb(color.A, value, value, value);
        }
        public static SolidBrush Brush(Color color) { return new SolidBrush(Ink(color)); }
        public static Pen Pen(Color color) { return new Pen(Ink(color)); }
        public static Pen Pen(Color color, float width) { return new Pen(Ink(color), width); }
        public static LinearGradientBrush Gradient(Rectangle rect, Color first, Color last, float angle) { return new LinearGradientBrush(rect, Ink(first), Ink(last), angle); }
        public static LinearGradientBrush Gradient(RectangleF rect, Color first, Color last, float angle) { return new LinearGradientBrush(rect, Ink(first), Ink(last), angle); }
        public static LinearGradientBrush Gradient(Rectangle rect, Color first, Color last, LinearGradientMode mode) { return new LinearGradientBrush(rect, Ink(first), Ink(last), mode); }
        public static void DrawText(IDeviceContext context, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags, int sampling=3)
        {
            Graphics graphics = context as Graphics;
            if (!DarkMode || graphics == null) { TextRenderer.DrawText(context, text, font, bounds, Ink(color), flags); return; }
            if (bounds.Width < 1 || bounds.Height < 1 || string.IsNullOrEmpty(text)) return;
            // Rasterize small UI text at 3x, then downsample once. Cache the result
            // so the animated background never rerasterizes unchanged process rows.
            color = Ink(color);
            string key = "ui|" + text + "|" + font.Name + "|" + font.SizeInPoints + "|" + (int)font.Style + "|" + bounds.Size + "|" + color.ToArgb() + "|" + (int)flags + "|" + graphics.DpiX + "|" + graphics.DpiY+"|"+sampling;
            Bitmap image;
            if (!textCache.TryGetValue(key,out image))
            {
                using (Bitmap large = new Bitmap(bounds.Width*sampling,bounds.Height*sampling,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    large.SetResolution(graphics.DpiX,graphics.DpiY);
                    using (Graphics canvas = Graphics.FromImage(large))
                    using (StringFormat format = new StringFormat(StringFormatFlags.NoWrap))
                    using (SolidBrush brush = Brush(color))
                    {
                        canvas.ScaleTransform(sampling,sampling);
                        canvas.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        format.Alignment = (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near;
                        format.LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near;
                        if ((flags & TextFormatFlags.EndEllipsis) != 0) format.Trimming = StringTrimming.EllipsisCharacter;
                        canvas.DrawString(text,font,brush,new Rectangle(0,0,bounds.Width,bounds.Height),format);
                    }
                    image = Downsample(large,bounds.Width,bounds.Height);
                }
                RememberText(key,image);
            }
            graphics.DrawImageUnscaled(image,bounds.Location);
        }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public static void StyleDialog(Form dialog)
        {
            if (!DarkMode) return;
            StyleDialogControls(dialog);
            dialog.HandleCreated += (s, e) => { int enabled = 1; DwmSetWindowAttribute(dialog.Handle, 20, ref enabled, 4); };
        }
        private static void StyleDialogControls(Control control)
        {
            control.BackColor = control is TextBoxBase || control is NumericUpDown || control is ComboBox || control is ListBox ? Color.FromArgb(22,22,22) : Color.Black;
            control.ForeColor = Color.White;
            Button button = control as Button;
            if (button != null) { button.UseVisualStyleBackColor = false; button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(27,27,27); button.FlatAppearance.BorderColor = Color.FromArgb(70,70,70); }
            ComboBox combo = control as ComboBox; if (combo != null) combo.FlatStyle = FlatStyle.Flat;
            LinkLabel link = control as LinkLabel; if (link != null) { link.LinkColor = Color.LightGray; link.ActiveLinkColor = Color.White; }
            foreach (Control child in control.Controls) StyleDialogControls(child);
        }
        private static Bitmap backdrop;
        private static float renderedPhase = -1;
        private static readonly System.Collections.Generic.Dictionary<string,Bitmap> textCache = new System.Collections.Generic.Dictionary<string,Bitmap>();
        private static readonly System.Collections.Generic.Queue<string> textOrder=new System.Collections.Generic.Queue<string>();
        private static long textBytes;
        private static void RememberText(string key,Bitmap image)
        {
            long bytes=(long)image.Width*image.Height*4;
            while(textOrder.Count>0 && (textCache.Count>=256 || textBytes+bytes>12*1024*1024))
            {string oldest=textOrder.Dequeue();Bitmap old=textCache[oldest];textBytes-=(long)old.Width*old.Height*4;textCache.Remove(oldest);old.Dispose();}
            textCache.Add(key,image);textOrder.Enqueue(key);textBytes+=bytes;
        }
        public static void Background(Graphics g, Control c)
        {
            Control root = c; int x = 0, y = 0;
            while (root.Parent != null) { x += root.Left; y += root.Top; root = root.Parent; }
            if(backdrop == null || backdrop.Size != root.ClientSize)
            {
                if(backdrop != null) backdrop.Dispose();
                backdrop = new Bitmap(Math.Max(1,root.Width), Math.Max(1,root.Height)); renderedPhase = -1;
            }
            if(renderedPhase != Phase)
            {
                using(Graphics canvas = Graphics.FromImage(backdrop)) Draw(canvas, new Rectangle(Point.Empty,backdrop.Size), Phase);
                renderedPhase = Phase;
            }
            GraphicsState s = g.Save(); g.SetClip(c.ClientRectangle, CombineMode.Intersect);
            g.DrawImageUnscaled(backdrop,-x,-y);
            var ancestors = new System.Collections.Generic.List<Control>();
            for (Control parent = c.Parent; parent != null; parent = parent.Parent) ancestors.Add(parent);
            ancestors.Reverse();
            foreach (Control parent in ancestors)
            {
                IThemeSurface card = parent as IThemeSurface;
                if (card == null) continue;
                int dx = 0, dy = 0;
                for (Control child = c; child != parent; child = child.Parent) { dx += child.Left; dy += child.Top; }
                GraphicsState layer = g.Save();
                g.TranslateTransform(-dx, -dy);
                card.PaintSurface(g);
                g.Restore(layer);
            }
            g.Restore(s);
        }
        private static Bitmap skyLayer;
        public static void Draw(Graphics g, Rectangle r, float phase)
        {
            if (!BackgroundEnabled) { using(SolidBrush black=Brush(Color.Black)) g.FillRectangle(black,r); return; }
            if (!BackgroundMotion) phase=0;
            if(skyLayer == null || skyLayer.Size != r.Size)
            {
                if(skyLayer != null) skyLayer.Dispose();
                using(Bitmap large=new Bitmap(r.Width*3,r.Height*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    using(Graphics canvas=Graphics.FromImage(large)) { canvas.ScaleTransform(3,3); DrawSky(canvas,r,0); }
                    skyLayer=Downsample(large,r.Width,r.Height);
                }
            }
            g.DrawImageUnscaled(skyLayer,0,0);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float w=r.Width,h=r.Height,horizon=h*.55f;
            using (Pen grid = Synthwave.Pen(Color.FromArgb(65, 245, 54, 201), 1f))
            {
                for (int i=-14;i<=14;i++) g.DrawLine(grid, w/2+i*15,horizon,w/2+i*105,h);
                for (int i=0;i<12;i++) { float t=((i+phase*.6f)%12)/12f; float y=horizon+(h-horizon)*t*t; g.DrawLine(grid,0,y,w,y); }
            }
            using (Pen line = Synthwave.Pen(Color.FromArgb(80, 255, 104, 221), 1)) g.DrawLine(line,0,horizon,w,horizon);
            if (CometsEnabled && BackgroundMotion) DrawComet(g,r,phase);
        }
        private static void DrawComet(Graphics graphics, Rectangle bounds, float phase)
        {
            // A brief glowing pass, using the existing scene clock.
            if (phase < 30) return;
            int pass = (int)((phase-30)/30);
            float age = (phase-30)-pass*30;
            const float duration = 2.5f;
            if (age > duration) return;
            uint seed = unchecked((uint)(pass+1)*2654435761u);
            float x = bounds.X + bounds.Width*(.12f + ((seed>>8)%420)/1000f) + age*88;
            float y = bounds.Y + bounds.Height*(.08f + ((seed>>20)%160)/1000f) + age*28;
            float fade = Math.Min(1,age/.25f)*Math.Min(1,(duration-age)/.5f);
            GraphicsState state = graphics.Save(); graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PointF end=new PointF(x,y), start=new PointF(x-46,y-15);
            using(LinearGradientBrush trail=new LinearGradientBrush(start,end,Color.Transparent,Color.FromArgb((int)(fade*190),Color.White)))
            using(Pen tail=new Pen(trail,1.3f)) graphics.DrawLine(tail,start,end);
            using(Pen haze=new Pen(Color.FromArgb((int)(fade*20),Color.White),3.5f)) graphics.DrawLine(haze,x-26,y-8.5f,x,y);
            using(GraphicsPath halo=new GraphicsPath())
            {
                halo.AddEllipse(x-5,y-5,10,10);
                using(PathGradientBrush glow=new PathGradientBrush(halo)) { glow.CenterColor=Color.FromArgb((int)(fade*155),Color.White); glow.SurroundColors=new[]{Color.Transparent}; graphics.FillPath(glow,halo); }
            }
            using(Pen shine=new Pen(Color.FromArgb((int)(fade*85),Color.White),.7f)) { graphics.DrawLine(shine,x-3,y,x+3,y); graphics.DrawLine(shine,x,y-3,x,y+3); }
            using (SolidBrush head = new SolidBrush(Color.FromArgb((int)(fade*245),Color.White))) graphics.FillEllipse(head,x-1.1f,y-1.1f,2.2f,2.2f);
            graphics.Restore(state);
        }
        private static void DrawSky(Graphics g, Rectangle r, float phase)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush sky = Synthwave.Gradient(r, Color.FromArgb(15, 5, 31), Color.FromArgb(91, 17, 96), 90f)) g.FillRectangle(sky, r);
            float w = r.Width, h = r.Height, horizon = h * .55f, cx = w * .72f;
            using (GraphicsPath halo = new GraphicsPath())
            {
                halo.AddEllipse(cx - 280, horizon - 230, 560, 420);
                using (PathGradientBrush glow = new PathGradientBrush(halo)) { glow.CenterColor = Ink(Color.FromArgb(105, 248, 39, 192)); glow.SurroundColors = new[] { Ink(Color.FromArgb(0, 130, 20, 180)) }; g.FillPath(glow, halo); }
            }
            GraphicsState state = g.Save();
            using (GraphicsPath sun = new GraphicsPath())
            {
                sun.AddEllipse(cx - 69, horizon - 112, 138, 138); g.SetClip(sun, CombineMode.Intersect);
                using (LinearGradientBrush fill = Synthwave.Gradient(new RectangleF(cx-70,horizon-112,140,140), Color.FromArgb(185,255,148,175), Color.FromArgb(150,243,25,179),90f))
                {
                    g.FillRectangle(fill,cx-70,horizon-112,140,67);
                    for(int i=0;i<7;i++) g.FillRectangle(fill,cx-70,horizon-41+i*11,140,Math.Max(2,8-i));
                }
            }
            g.Restore(state);
            for(int i=0;i<32;i++)
            {
                float x=(i*137+29)%r.Width, y=(i*53+13)%(Math.Max(1,(int)horizon));
                using(SolidBrush star=Synthwave.Brush(Color.FromArgb(35+(int)(20*(1+Math.Sin(phase+i))),211,182,255))) g.FillEllipse(star,x,y,1.4f,1.4f);
            }
        }
        private static Bitmap markImage;
        public static void Mark(Graphics g, RectangleF r)
        {
            int w=Math.Max(1,(int)Math.Ceiling(r.Width)), h=Math.Max(1,(int)Math.Ceiling(r.Height));
            if(markImage==null || markImage.Width!=w || markImage.Height!=h)
            {
                if(markImage!=null)markImage.Dispose();
                using(Bitmap large=new Bitmap(w*3,h*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    using(Graphics canvas=Graphics.FromImage(large)){canvas.ScaleTransform(3,3);DrawMark(canvas,new RectangleF(0,0,w,h));}
                    markImage=Downsample(large,w,h);
                }
            }
            g.DrawImageUnscaled(markImage,(int)r.X,(int)r.Y);
        }
        private static void DrawMark(Graphics graphics, RectangleF bounds) { Brand.DrawBadge(graphics, bounds); }
        public static void Text(Graphics g,string text,Font font,RectangleF rect,Color color,bool center)
        {
            if(rect.Width<=0 || rect.Height<=0 || string.IsNullOrEmpty(text)) return;
            if (DarkMode && color.ToArgb() != Color.White.ToArgb()) color = color.R > 230 && color.G > 220 ? Color.White : Color.FromArgb(190,190,190);
            string key=text+"|"+font.Name+"|"+font.Size+"|"+(int)font.Style+"|"+rect.Size+"|"+color.ToArgb()+"|"+center+"|"+g.DpiY;
            Bitmap bitmap;
            if(!textCache.TryGetValue(key,out bitmap))
            {
                int width=(int)Math.Ceiling(rect.Width)+8,height=(int)Math.Ceiling(rect.Height)+8;
                using(Bitmap large=new Bitmap(width*3,height*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    large.SetResolution(g.DpiX,g.DpiY);
                    using(Graphics canvas=Graphics.FromImage(large)) {canvas.ScaleTransform(3,3);canvas.SmoothingMode=SmoothingMode.AntiAlias; DrawText(canvas,text,font,new RectangleF(4,4,rect.Width,rect.Height),color,center);}
                    bitmap=Downsample(large,width,height);
                }
                RememberText(key,bitmap);
            }
            g.DrawImageUnscaled(bitmap,(int)rect.X-4,(int)rect.Y-4);
        }
        internal static Bitmap Downsample(Bitmap large,int width,int height)
        {
            Bitmap result=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(Graphics canvas=Graphics.FromImage(result))
            using(System.Drawing.Imaging.ImageAttributes attributes=new System.Drawing.Imaging.ImageAttributes())
            {
                canvas.InterpolationMode=InterpolationMode.HighQualityBicubic;
                canvas.PixelOffsetMode=PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                canvas.DrawImage(large,new Rectangle(0,0,width,height),0,0,large.Width,large.Height,GraphicsUnit.Pixel,attributes);
            }
            return result;
        }
        private static void DrawText(Graphics g,string text,Font font,RectangleF rect,Color color,bool center)
        {
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(StringFormat sf=new StringFormat { Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap })
            using(GraphicsPath path=new GraphicsPath())
            {
                path.AddString(text,font.FontFamily,(int)font.Style,font.SizeInPoints*g.DpiY/72f,rect,sf);
                if (!DarkMode) for(int i=6;i>=2;i-=2) using(Pen glow=Synthwave.Pen(Color.FromArgb(font.SizeInPoints<12?7:11,255,91,215),i)){glow.LineJoin=LineJoin.Round;g.DrawPath(glow,path);}
                using(SolidBrush fill=Synthwave.Brush(color)) g.FillPath(fill,path);
            }
        }
    }
    internal sealed class NeonLabel : Label
    {
        public NeonLabel(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);UseCompatibleTextRendering=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Synthwave.Text(e.Graphics,Text,Font,ClientRectangle,ForeColor,false);}
    }
}
