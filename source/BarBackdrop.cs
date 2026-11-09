using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Lucent
{
    // The player bar's color: a band that runs across it from the left of the cover's colors to the right of them, so a cover with
    // several colors gives a bar with several colors. It is a plain picture (nothing behind the bar is copied or blurred), which makes it
    // cheap and keeps it clean. The band is see-through, so the moving colors of the window show through it; its colors are bright but kept
    // dark enough for the white text and the muted text on top.
    internal sealed class BarBackdrop:Border
    {
        internal const int Count=7;
        // Brightest a color along the bar may get (0-255) and how much more colorful than the cover it is made.
        const double LumaLimit=150,Vividness=2.0;
        // How much of the color is laid over what is behind the bar (the moving colors of the window), so the bar is see-through; a plain bar is lighter still.
        // Mostly see-through, so the bar is the same moving colors as the window around it, darkened.
        // Like glass: little of the color is laid on, so the moving colors of the window show clearly through the bar.
        const byte Cover=66,PlainAlpha=54;
        // Without the glass (its setting is off) the bar is the older, more solid band.
        const byte SolidCover=132,SolidPlainAlpha=112;
        internal static readonly Color Plain=Color.FromArgb(PlainAlpha,30,30,34);
        internal static readonly Color SolidPlain=Color.FromArgb(SolidPlainAlpha,30,30,34);
        internal static Color PlainFor(bool glass){return glass?Plain:SolidPlain;}
        // Whether the bar is glass; set by the app.
        internal bool Glass=true;
        internal Color[] Target {get;private set;}
        // The band is drawn as a picture, one shade per pixel with the rounding error spread out as a very fine, even noise (dithering). A plain
        // gradient of dark colors shows faint bands (streaks) where each shade of the 8-bit colors ends and the next begins; this does not.
        // It is made at the bar's own size in pixels, so each pixel of noise is one pixel of the screen (stretched, the noise became coarse and made the bar look fuzzy).
        int pictureWidth=1536,pictureHeight=32;
        readonly Image shown=new Image {Stretch=Stretch.Fill},coming=new Image {Stretch=Stretch.Fill,Opacity=0};
        public BarBackdrop()
        {
            IsHitTestVisible=false;
            Target=new Color[Count];
            for(int i=0;i<Count;i++)Target[i]=Plain;
            foreach(var image in new[]{shown,coming})RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);
            shown.Source=Picture(Target,pictureWidth,pictureHeight);
            sizeSettle.Tick+=(s,e)=>{sizeSettle.Stop();Resize();};
            SizeChanged+=(s,e)=>{sizeSettle.Stop();sizeSettle.Start();};
            var layers=new Grid();layers.Children.Add(shown);layers.Children.Add(coming);
            Child=layers;
        }
        // The band as a picture: the colors run across it from stop to stop; the noise is the same on every channel, so it only moves the light a little.
        readonly System.Windows.Threading.DispatcherTimer sizeSettle=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};
        // After the bar has changed size (and stayed that size for a moment), the picture is made again at the new size.
        void Resize()
        {
            var dpi=VisualTreeHelper.GetDpi(this);
            int width=(int)Math.Ceiling(ActualWidth*dpi.DpiScaleX),height=(int)Math.Ceiling(ActualHeight*dpi.DpiScaleY);
            if(width<16||height<4||(width==pictureWidth&&height==pictureHeight))return;
            pictureWidth=width;pictureHeight=height;
            var colors=Target;int token=++version;
            System.Threading.Tasks.Task.Run(()=>Picture(colors,width,height)).ContinueWith(task=>
            {
                if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion)return;
                var picture=task.Result;
                try{Dispatcher.BeginInvoke(new Action(()=>{if(token!=version)return;coming.BeginAnimation(OpacityProperty,null);coming.Opacity=0;shown.Source=picture;}));}catch(Exception){}
            });
        }
        internal static BitmapSource Picture(Color[] colors,int width,int height)
        {
            int PictureWidth=width,PictureHeight=height;
            var pixels=new byte[PictureWidth*PictureHeight*4];
            for(int x=0;x<PictureWidth;x++)
            {
                double at=x/(double)(PictureWidth-1)*(Count-1);int i=Math.Min(Count-2,(int)at);double f=at-i;
                Color a=colors[i],b=colors[i+1];
                double alpha=a.A+(b.A-a.A)*f,red=a.R+(b.R-a.R)*f,green=a.G+(b.G-a.G)*f,blue=a.B+(b.B-a.B)*f,scale=alpha/255;
                for(int y=0;y<PictureHeight;y++)
                {
                    uint hash=unchecked((uint)x*73856093u^(uint)y*19349663u);
                    hash=unchecked((hash^(hash>>15))*2246822519u);
                    hash=unchecked((hash^(hash>>13))*3266489917u);
                    hash^=hash>>16;
                    double noise=(((hash&65535)+(hash>>16))/65535d-1)*.75;
                    int to=(y*PictureWidth+x)*4;
                    byte a8=(byte)Math.Max(0,Math.Min(255,Math.Round(alpha+noise)));
                    pixels[to]=(byte)Math.Max(0,Math.Min(a8,Math.Round(blue*scale+noise)));
                    pixels[to+1]=(byte)Math.Max(0,Math.Min(a8,Math.Round(green*scale+noise)));
                    pixels[to+2]=(byte)Math.Max(0,Math.Min(a8,Math.Round(red*scale+noise)));
                    pixels[to+3]=a8;
                }
            }
            var picture=BitmapSource.Create(PictureWidth,PictureHeight,96,96,PixelFormats.Pbgra32,null,pixels,PictureWidth*4);
            picture.Freeze();
            return picture;
        }
        // Whether the colored band is on. Off (performance mode), the bar is a plain tinted strip.
        bool on=true;
        internal bool On
        {
            get{return on;}
            set{if(value==on)return;on=value;Visibility=on?Visibility.Visible:Visibility.Collapsed;var changed=Toggled;if(changed!=null)changed(on);}
        }
        // Told when the band is switched on or off (performance mode), so what belongs with it can follow.
        internal Action<bool> Toggled;        // The colors for a cover, left to right: each is the average of a band of the picture, a little more colorful, and no brighter than
        // the limit. Strength 0 is plain dark, 1 is the full colors.
        internal static Color[] ColorsFor(BitmapSource cover,Color accent,double strength,bool glass=true)
        {
            byte coverAlpha=glass?Cover:SolidCover,plainAlpha=glass?PlainAlpha:SolidPlainAlpha;
            Color plain=PlainFor(glass);
            var colors=new Color[Count];
            strength=Math.Max(0,Math.Min(1,strength));
            double[] r=new double[Count],g=new double[Count],b=new double[Count];
            if(cover!=null)
            {
                const int Detail=28;
                var small=new FormatConvertedBitmap(new TransformedBitmap(cover,new ScaleTransform(Detail/(double)cover.PixelWidth,Detail/(double)cover.PixelHeight)),PixelFormats.Bgra32,null,0);
                int w=small.PixelWidth,h=small.PixelHeight,stride=w*4;
                var bytes=new byte[stride*h];small.CopyPixels(bytes,stride,0);
                for(int i=0;i<Count;i++)
                {
                    double centre=i*(w-1)/(double)(Count-1),sum=0,sr=0,sg=0,sb=0,plainLuma=0,plainWeight=0;
                    for(int x=0;x<w;x++)
                    {
                        double weight=Math.Max(0,1-Math.Abs(x-centre)/(w/(double)(Count-1)));
                        if(weight<=0)continue;
                        for(int y=0;y<h;y++)
                        {
                            // The colorful pixels count for more, so a bright cover with a few greys in it still gives a colored bar, not a muddy average.
                            int at=y*stride+x*4;double top=Math.Max(bytes[at],Math.Max(bytes[at+1],bytes[at+2])),low=Math.Min(bytes[at],Math.Min(bytes[at+1],bytes[at+2])),sat=top<=0?0:(top-low)/top,pixel=weight*(.15+sat*sat*6);
                            sb+=bytes[at]*pixel;sg+=bytes[at+1]*pixel;sr+=bytes[at+2]*pixel;sum+=pixel;plainLuma+=(bytes[at+2]*.2126+bytes[at+1]*.7152+bytes[at]*.0722)*weight;plainWeight+=weight;
                        }
                    }
                    if(sum>0)
                    {
                        r[i]=sr/sum;g[i]=sg/sum;b[i]=sb/sum;
                        // As bright as that part of the cover really is on the whole (the colorful pixels only choose the hue): a nearly black cover gives a dark bar.
                        double weighted=r[i]*.2126+g[i]*.7152+b[i]*.0722,real=plainLuma/Math.Max(1e-6,plainWeight),fit=Math.Max(.25,Math.Min(1.6,(real*1.3+6)/Math.Max(1,weighted)));
                        r[i]*=fit;g[i]*=fit;b[i]*=fit;
                    }
                }
            }
            else
            {
                // Without a cover the one accent color, a little differently lit along the bar.
                for(int i=0;i<Count;i++){double lit=.7+.3*Math.Sin(i*1.1);r[i]=accent.R*lit;g[i]=accent.G*lit;b[i]=accent.B*lit;}
            }
            for(int i=0;i<Count;i++)
            {
                double luma=r[i]*.2126+g[i]*.7152+b[i]*.0722;
                double cr=luma+(r[i]-luma)*Vividness,cg=luma+(g[i]-luma)*Vividness,cb=luma+(b[i]-luma)*Vividness;
                cr=Math.Max(0,Math.Min(255,cr));cg=Math.Max(0,Math.Min(255,cg));cb=Math.Max(0,Math.Min(255,cb));
                double now=cr*.2126+cg*.7152+cb*.0722,gain=now>LumaLimit?LumaLimit/now:1;
                cr*=gain;cg*=gain;cb*=gain;
                colors[i]=Color.FromArgb((byte)Math.Round(plainAlpha+(coverAlpha-plainAlpha)*strength),(byte)Math.Round(plain.R+(cr-plain.R)*strength),(byte)Math.Round(plain.G+(cg-plain.G)*strength),(byte)Math.Round(plain.B+(cb-plain.B)*strength));
            }
            return colors;
        }
        // Moves the band to a cover's colors (to plain dark when there is none), fading from the colors it had.
        internal ColorSync Sync;
        // True when the colors changed (a new picture is being made, and the sync will hear from it).
        internal bool SetCover(BitmapSource cover,Color accent,bool colored,double strength)
        {
            var colors=colored?ColorsFor(cover,accent,strength,Glass):ColorsFor(null,PlainFor(Glass),0,Glass);
            if(Target!=null&&Target.Length==colors.Length&&System.Linq.Enumerable.SequenceEqual(Target,colors))return false;
            Target=colors;
            int token=++version;
            // The picture is made off the app's own thread (it is a few tens of thousands of pixels), then fades in over the old one and takes its place.
            int width=pictureWidth,height=pictureHeight,forRound=Sync==null?0:Sync.Round;
            System.Threading.Tasks.Task.Run(()=>Picture(colors,width,height)).ContinueWith(task=>
            {
                if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion)return;
                var picture=task.Result;
                try
                {
                    Dispatcher.BeginInvoke(new Action(()=>
                    {
                        if(token!=version)return;
                        Action start=()=>{if(token==version)Show(picture);};
                        if(Sync!=null)Sync.Offer(forRound,start);else start();
                    }));
                }
                catch(Exception){}
            });
            return true;
        }
        int version;
        void Show(BitmapSource picture)
        {
            if(Design.Instant){coming.BeginAnimation(OpacityProperty,null);coming.Opacity=0;shown.Source=picture;return;}
            coming.BeginAnimation(OpacityProperty,null);
            coming.Source=picture;coming.Opacity=0;
            var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(500)){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
            fade.Completed+=(s,e)=>{if(ReferenceEquals(coming.Source,picture)){shown.Source=picture;coming.BeginAnimation(OpacityProperty,null);coming.Opacity=0;}};
            coming.BeginAnimation(OpacityProperty,fade);
        }
    }
}
