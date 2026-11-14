using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Lucent
{
    // Lets the things that follow the cover's colors (the window's colors, the moving colors, the bar's band and the tint of the sections and the bar)
    // change together. Each of them works its new colors out in the background and takes a different time to do it; the ones that were ready first used
    // to start fading at once, so the sections changed first and the rest came after. Now the ones that are ready wait for the others, and all begin
    // together (or after a short time at most, if one is slow).
    internal sealed class ColorSync
    {
        internal bool Enabled=true;
        int round,expected;
        bool active;
        readonly System.Collections.Generic.List<Action> ready=new System.Collections.Generic.List<Action>();
        Action sections;
        readonly DispatcherTimer timeout=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(900)};
        public ColorSync(){timeout.Tick+=(s,e)=>Flush();}
        internal int Round {get{return round;}}
        // A change of colors begins. Anything still waiting from the one before is let go first, so nothing is left unapplied.
        internal int Begin()
        {
            if(!Enabled)return 0;
            if(active)Flush();
            round++;active=true;expected=int.MaxValue;sections=null;
            return round;
        }
        // How many of them will report (those that had something to change), and what to do for the sections once they all have.
        internal void Expect(int forRound,int count,Action apply)
        {
            if(!Enabled||!active||forRound!=round){apply();return;}
            sections=apply;expected=count;
            if(ready.Count>=expected){Flush();return;}
            timeout.Stop();timeout.Start();
        }
        // A backdrop has its new colors ready: it waits for the others. Outside a change of colors (a resize, say) it just goes ahead.
        internal void Offer(int forRound,Action start)
        {
            if(!Enabled||!active||forRound!=round){start();return;}
            ready.Add(start);
            if(ready.Count>=expected)Flush();
        }
        // Something that should show up with the new colors (the new cover and title) rather than before them: it goes at the same moment, or at once when no change is under way.
        internal void AfterFlush(Action show)
        {
            if(!Enabled||!active){show();return;}
            later.Add(show);
        }
        readonly System.Collections.Generic.List<Action> later=new System.Collections.Generic.List<Action>();
        void Flush()
        {
            timeout.Stop();
            var starts=ready.ToArray();ready.Clear();
            var apply=sections;sections=null;active=false;
            var shows=later.ToArray();later.Clear();
            foreach(var start in starts)start();
            if(apply!=null)apply();
            foreach(var show in shows)show();
        }
    }
    // Smooth color fields are computed once per palette/size change, off the UI
    // thread. Monochrome dithering avoids quantized dark-gradient bands.
    internal sealed class AmbientBackdrop:FrameworkElement
    {
        const int FieldWidth=32,FieldHeight=20;
        const double LumaLimit=150;
        BitmapSource previous,current;
        Color accent;
        BitmapSource artwork;
        double strength;
        bool colored,configured;
        int revision;
        readonly DispatcherTimer regenerateTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(35)};
        public static readonly DependencyProperty BlendProperty=DependencyProperty.Register("Blend",typeof(double),typeof(AmbientBackdrop),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
        public AmbientBackdrop()
        {
            IsHitTestVisible=false;
            RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
            regenerateTimer.Tick+=(s,e)=>
            {
                regenerateTimer.Stop();
                Regenerate();
            };
            // While the window is being resized the picture is only stretched; it is made again once the size has settled.
            // The high-quality filter is the slow one, and every step of a resize draws this picture across the whole window again: while the size is
            // changing it is drawn with the plain filter, and the sharp one comes back with the picture made for the new size.
            SizeChanged+=(s,e)=>{RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.Linear);Schedule(240);};
            Unloaded+=(s,e)=>
            {
                regenerateTimer.Stop();
                revision++;
            };
        }
        void Schedule(int milliseconds=35){revision++;regenerateTimer.Stop();regenerateTimer.Interval=TimeSpan.FromMilliseconds(milliseconds);regenerateTimer.Start();}
        internal ColorSync Sync;
        // True when the colors changed (a new picture is being made).
        public bool SetPalette(Color color,double amount,bool hasImage,BitmapSource cover=null)
        {
            amount=Math.Max(0,Math.Min(1,amount));
            if(configured&&color==accent&&Math.Abs(amount-strength)<.001&&colored==hasImage&&ReferenceEquals(artwork,cover))return false;
            accent=color;strength=amount;colored=hasImage;artwork=cover;configured=true;
            // The change of colors this belongs to (if one is under way) is noted now, before the picture is made.
            round=Sync==null?0:Sync.Round;
            Schedule();return true;
        }
        int round;
        async void Regenerate()
        {
            if(!configured){RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);return;}
            int width=Math.Max(640,Math.Min(1280,(int)ActualWidth)),height=Math.Max(400,Math.Min(800,(int)ActualHeight));
            int token=++revision;var color=accent;double amount=strength;bool image=colored;var cover=artwork;int forRound=round;
            try
            {
                var bitmap=await Task.Run(()=>Create(color,amount,image,width,height,cover)).ConfigureAwait(false);
                // The initial palette can be requested before Application.Run has
                // installed a synchronization context. Always marshal explicitly.
                if(Dispatcher.HasShutdownStarted)return;
                await Dispatcher.InvokeAsync(()=>{
                    Action apply=()=>{
                    if(token!=revision)return;previous=current;current=bitmap;
                    RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
                    BeginAnimation(BlendProperty,null);
                    // Performance mode: no fade, the new picture just takes the old one's place.
                    if(Design.Instant)previous=null;
                    SetValue(BlendProperty,previous==null?1d:0d);
                    if(previous!=null)
                    {
                        // The whole window is redrawn for every step of this fade, so it runs at a modest rate and a little slower, which looks smoother than a short 60 fps burst that stalls.
                        var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(500)){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
                        Timeline.SetDesiredFrameRate(fade,30);
                        // Once the new picture is fully in, the old one (hidden under it) is dropped: it was drawn across the window at every frame for nothing.
                        fade.Completed+=(s,e)=>{if(token==revision){previous=null;InvalidateVisual();}};
                        BeginAnimation(BlendProperty,fade);
                    }
                    InvalidateVisual();
                    };
                    // It waits for the other backdrops to be ready, and then they all begin to change together.
                    if(Sync!=null)Sync.Offer(forRound,apply);else apply();
                });
            }
            catch(TaskCanceledException){ } // Window closed before the palette completed.
        }
        internal static BitmapSource Create(Color color,double amount,bool image,int width,int height,BitmapSource cover=null)
        {
            var pixels=new byte[width*height*4];
            double r=color.R,g=color.G,b=color.B,max=Math.Max(r,Math.Max(g,b));
            if(max>0){double gain=210/Math.Max(80,max);r*=gain;g*=gain;b*=gain;}
            Color tone=Colors.Black;double lightness=0;var field=cover==null?null:ArtworkField(cover,width/(double)height,out tone,out lightness);
            // A pale cover gives a lighter window (as far as white text can still be read on it); a dark one stays dark.
            double level=amount/.85*(1+.2*Smooth((lightness-.4)/.4)),baseR=tone.R*level,baseG=tone.G*level,baseB=tone.B*level;
            var row=new double[FieldWidth*3];var columnStart=new int[width];var columnWeights=new double[width*4];var weight=new double[4];
            for(int x=0;x<width;x++)
            {
                columnStart[x]=Spline(x/(double)Math.Max(1,width-1),FieldWidth,weight);
                Array.Copy(weight,0,columnWeights,x*4,4);
            }
            for(int y=0;y<height;y++)
            {
                double ny=y/(double)Math.Max(1,height-1),wash=.34+.42*Math.Pow(1-ny,1.3);
                // The cover's glow fills the top and is gone by mid-window, leaving the flat dark tone.
                double glow=(.78+.22*Smooth(ny/.16))*(1-.7*Smooth((ny-.16)/.62));
                if(field!=null)
                {
                    int first=Spline(ny,FieldHeight,weight);
                    for(int j=0;j<row.Length;j++)
                    {
                        row[j]=0;
                        for(int k=0;k<4;k++)row[j]+=weight[k]*field[Clamp(first+k,FieldHeight)*FieldWidth*3+j];
                    }
                }
                for(int x=0;x<width;x++)
                {
                    double nx=x/(double)Math.Max(1,width-1),spot=Math.Exp(-2*((nx-.57)*(nx-.57)/.20+(ny+.10)*(ny+.10)/.30));
                    double tr=0,tg=0,tb=0,floor=7;
                    if(field!=null)
                    {
                        double fb=0,fg=0,fr=0;int start=columnStart[x];
                        for(int k=0;k<4;k++)
                        {
                            int cell=Clamp(start+k,FieldWidth)*3;
                            double w=columnWeights[x*4+k];
                            fb+=row[cell]*w;
                            fg+=row[cell+1]*w;
                            fr+=row[cell+2]*w;
                        }
                        Soften(fr*.76*level,fg*.76*level,fb*.76*level,out tr,out tg,out tb);
                        double reach=glow;
                        tr=baseR+(tr-baseR)*reach;tg=baseG+(tg-baseG)*reach;tb=baseB+(tb-baseB)*reach;floor=0;
                    }
                    else if(image)Soften(r*wash*amount,g*wash*amount,b*wash*amount,out tr,out tg,out tb);
                    else
                    {
                        // Nothing playing: not flat black, but a quiet deep-blue light at the top right and a faint teal glow low on the left.
                        double low=Math.Exp(-2*((nx-.1)*(nx-.1)/.24+(ny-1)*(ny-1)/.3));
                        tr=14*spot+3*low;tg=16*spot+16*low;tb=44*spot+20*low;floor=8;
                    }
                    // Well-mixed triangular dither: a weak hash leaves visible streaks.
                    uint hash=unchecked((uint)x*73856093u^(uint)y*19349663u);
                    hash=unchecked((hash^(hash>>15))*2246822519u);
                    hash=unchecked((hash^(hash>>13))*3266489917u);
                    hash^=hash>>16;
                    double noise=(((hash&65535)+(hash>>16))/65535d-1)*.75;
                    int i=(y*width+x)*4;
                    pixels[i]=(byte)Math.Max(0,Math.Min(255,Math.Round(floor+tb+noise)));
                    pixels[i+1]=(byte)Math.Max(0,Math.Min(255,Math.Round(floor+tg+noise)));
                    pixels[i+2]=(byte)Math.Max(0,Math.Min(255,Math.Round(floor+tr+noise)));
                    pixels[i+3]=255;
                }
            }
            var result=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,width*4);result.Freeze();return result;
        }
        static double Smooth(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        // Roll brightness off smoothly: saturated hues stay vivid, while pale
        // artwork can never reach behind-text contrast.
        static void Soften(double r,double g,double b,out double outR,out double outG,out double outB)
        {
            double luma=r*.2126+g*.7152+b*.0722,soft=luma>0?LumaLimit*(1-Math.Exp(-luma/LumaLimit))/luma:1;
            outR=r*soft;outG=g*soft;outB=b*soft;
        }
        static int Clamp(int index,int count){return index<0?0:index>=count?count-1:index;}
        // Cubic B-spline weights. Unlike bilinear sampling its slope is continuous
        // across field cells, so no lines appear where the cells meet.
        static int Spline(double position,int count,double[] weight)
        {
            double p=position*(count-1);int cell=Math.Min(count-2,(int)p);double f=p-cell,f2=f*f,f3=f2*f;
            weight[0]=(1-3*f+3*f2-f3)/6;weight[1]=(4-6*f2+3*f3)/6;weight[2]=(1+3*f+3*f2-3*f3)/6;weight[3]=f3/6;
            return cell-1;
        }
        // The backdrop is the cover itself, enlarged to the window's width and heavily
        // blurred, so every color keeps its place instead of being reduced to a palette.
        static double[] ArtworkField(BitmapSource cover,double aspect,out Color tone,out double lightness)
        {
            const int Detail=32;
            var small=new FormatConvertedBitmap(new TransformedBitmap(cover,new ScaleTransform(Detail/(double)cover.PixelWidth,Detail/(double)cover.PixelHeight)),PixelFormats.Bgra32,null,0);
            int w=small.PixelWidth,h=small.PixelHeight,stride=w*4;
            var bytes=new byte[stride*h];small.CopyPixels(bytes,stride,0);
            double sumR=0,sumG=0,sumB=0;
            for(int i=0;i<bytes.Length;i+=4){sumB+=bytes[i];sumG+=bytes[i+1];sumR+=bytes[i+2];}
            int count=w*h;tone=BaseTone(sumR/count,sumG/count,sumB/count);lightness=(sumR*.2126+sumG*.7152+sumB*.0722)/count/255;
            double visible=Math.Min(1,1/Math.Max(.01,aspect)),offset=(1-visible)/2;
            var field=new double[FieldWidth*FieldHeight*3];
            for(int y=0;y<FieldHeight;y++)
            {
                double sy=(offset+visible*y/(FieldHeight-1))*(h-1);int top=Math.Min(h-2,(int)sy);double down=sy-top;
                for(int x=0;x<FieldWidth;x++)
                {
                    int column=Math.Min(w-1,x*(w-1)/(FieldWidth-1)),j=(y*FieldWidth+x)*3;
                    for(int c=0;c<3;c++)field[j+c]=bytes[top*stride+column*4+c]*(1-down)+bytes[(top+1)*stride+column*4+c]*down;
                }
            }
            field=Blurred(field);
            // Blurring dulls colors; give back a little of what it took.
            for(int j=0;j<field.Length;j+=3)
            {
                double luma=field[j+2]*.2126+field[j+1]*.7152+field[j]*.0722;
                for(int c=0;c<3;c++)field[j+c]=Math.Max(0,Math.Min(255,luma+(field[j+c]-luma)*1.7));
            }
            return field;
        }
        static double[] Blurred(double[] field)
        {
            const int Reach=5;
            var weights=new double[Reach*2+1];double total=0;
            for(int k=-Reach;k<=Reach;k++){weights[k+Reach]=Math.Exp(-k*k/6d);total+=weights[k+Reach];}
            for(int k=0;k<weights.Length;k++)weights[k]/=total;
            var across=new double[field.Length];var result=new double[field.Length];
            for(int y=0;y<FieldHeight;y++)for(int x=0;x<FieldWidth;x++)for(int c=0;c<3;c++)for(int k=-Reach;k<=Reach;k++)
                across[(y*FieldWidth+x)*3+c]+=field[(y*FieldWidth+Clamp(x+k,FieldWidth))*3+c]*weights[k+Reach];
            for(int y=0;y<FieldHeight;y++)for(int x=0;x<FieldWidth;x++)for(int c=0;c<3;c++)for(int k=-Reach;k<=Reach;k++)
                result[(y*FieldWidth+x)*3+c]+=across[(Clamp(y+k,FieldHeight)*FieldWidth+x)*3+c]*weights[k+Reach];
            return result;
        }
        // The lower window: the cover's average color, dimmed. A colorful cover keeps
        // more of its strongest channel, so it stays rich where a gray one stays quiet.
        static Color BaseTone(double r,double g,double b)
        {
            const double Dim=.34,RichDim=.44;
            double top=Math.Max(r,Math.Max(g,b)),least=Math.Min(r,Math.Min(g,b));
            double colorfulness=(top-least)/255;
            double topDim=Dim+(RichDim-Dim)*Smooth((colorfulness-.1)/.18);
            return Color.FromRgb((byte)(r==top?r*topDim:r*Dim),(byte)(g==top?g*topDim:g*Dim),(byte)(b==top?b*topDim:b*Dim));
        }
        protected override void OnRender(DrawingContext dc)
        {
            if(current==null)return;var bounds=new Rect(0,0,ActualWidth,ActualHeight);
            if(previous!=null)dc.DrawImage(previous,bounds);
            dc.PushOpacity((double)GetValue(BlendProperty));dc.DrawImage(current,bounds);dc.Pop();
        }
    }
}
