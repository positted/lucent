using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Lucent
{
    // The big player's background: the album cover, shrunk until it is only color, then enlarged and left to drift.
    // Three copies turn and slide slowly in different directions, so the colors keep flowing into each other.
    internal sealed class FlowingBackdrop:Grid
    {
        // Each layer: how much bigger than the window it is, how strongly it shows, its turn in seconds, and how far it slides.
        static readonly double[] Scale={2.1,2.7,1.7},Strength={.85,.6,.7},Turn={140,200,110},Slide={.07,.1,.05},Pace={19,27,15};
        // The rich background (the main window): the first layer is the cover's smooth colors, the other two are pools of color and of light and shadow that move faster and cross each other.
        static readonly double[] RichScale={2.1,2.3,1.9},RichStrength={.9,.85,.95},RichTurn={150,80,60},RichSlide={.07,.08,.07},RichPace={19,14,11};
        bool rich;
        // How light the cover's smooth colors may get in the rich background: a pale cover gives a light grey up to this (0-255), as far as white text can still be read on it.
        internal double LightCap=140;
        internal bool Rich {get{return rich;}set{if(value==rich)return;rich=value;Fit();Rebuild();}}
        // The one background of every player (the big player, the one in its own window and the mini player; the app's own background is the rich one above):
        // the cover itself, softened but not reduced to a few colors, so its shapes and light drift behind the player; a second, turned copy crossing it, and gentle
        // pools of light and shadow so even a flat cover moves.
        static readonly double[] PlayerScale={2.0,2.3,1.8},PlayerStrength={.95,.5,.4},PlayerTurn={170,110,80},PlayerSlide={.06,.07,.06},PlayerPace={22,17,13};
        bool player;
        internal bool Player {get{return player;}set{if(value==player)return;player=value;Background=new SolidColorBrush(value?Color.FromRgb(5,5,6):Color.FromRgb(13,13,16));grain.Visibility=value?Visibility.Visible:Visibility.Collapsed;Fit();Rebuild();}}
        // A very fine grain over the finished picture: dark, slowly changing colors are drawn in 8-bit steps, and the steps show as faint blocks (a pixelated mist);
        // the grain breaks them up. It is only a level or two, so it does not read as texture.
        readonly Border grain=new Border {Background=MakeGrain(),IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        static Brush MakeGrain()
        {
            const int Side=128;
            var random=new Random(11);var pixels=new byte[Side*Side*4];
            for(int i=0;i<Side*Side;i++){byte v=(byte)(random.Next(2)*255);pixels[i*4]=pixels[i*4+1]=pixels[i*4+2]=v;pixels[i*4+3]=(byte)random.Next(1,3);}
            var tile=BitmapSource.Create(Side,Side,96,96,PixelFormats.Bgra32,null,pixels,Side*4);
            var brush=new ImageBrush(tile){TileMode=TileMode.Tile,Viewport=new Rect(0,0,Side,Side),ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.None};
            RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.NearestNeighbor);
            brush.Freeze();
            return brush;
        }
        double[] ScaleOf {get{return player?PlayerScale:rich?RichScale:Scale;}}
        double[] StrengthOf {get{return player?PlayerStrength:rich?RichStrength:Strength;}}
        double[] TurnOf {get{return player?PlayerTurn:rich?RichTurn:Turn;}}
        double[] SlideOf {get{return player?PlayerSlide:rich?RichSlide:Slide;}}
        double[] PaceOf {get{return player?PlayerPace:rich?RichPace:Pace;}}
        // Two sets of three layers: when the cover changes the new set fades in while the old one fades out, so the colors flow into the next song instead of dipping to black.
        readonly Image[] layers=new Image[6];
        readonly RotateTransform[] turns=new RotateTransform[3];
        readonly ScaleTransform[] breathe=new ScaleTransform[3];
        readonly TranslateTransform[] slides=new TranslateTransform[3];
        // A canvas lets the layers be far larger than the window without the layout cutting them down to its size,
        // which would show up as hard-edged tilted panels once they turn.
        readonly Canvas field=new Canvas();
        readonly Border shade=new Border {Background=new LinearGradientBrush(Color.FromArgb(70,0,0,0),Color.FromArgb(150,0,0,0),90),IsHitTestVisible=false};
        // How much of that shade is used: 1 is the full darkening, less lets the colors show brighter.
        internal double Shade {get{return shade.Opacity;}set{shade.Opacity=Math.Max(0,Math.Min(1,value));}}
        bool hasCover,running;

        public FlowingBackdrop()
        {
            Background=new SolidColorBrush(Color.FromRgb(13,13,16));
            ClipToBounds=true;
            IsHitTestVisible=false;
            for(int i=0;i<3;i++)
            {
                var group=new TransformGroup();
                turns[i]=new RotateTransform();
                slides[i]=new TranslateTransform();
                if(i==1)group.Children.Add(new ScaleTransform(-1,1));
                breathe[i]=new ScaleTransform(1,1);
                group.Children.Add(breathe[i]);
                group.Children.Add(turns[i]);
                group.Children.Add(slides[i]);
                // Both sets share the same motion.
                for(int set=0;set<2;set++)
                {
                    var layer=layers[set*3+i]=new Image {Stretch=Stretch.Fill,Opacity=0,RenderTransformOrigin=new Point(.5,.5),RenderTransform=group};
                    // A very small picture drawn large is a very smooth one; no blur effect has to run each frame.
                    RenderOptions.SetBitmapScalingMode(layer,BitmapScalingMode.Linear);
                    field.Children.Add(layer);
                }
            }
            Children.Add(field);
            // Darkens the colors so the text stays readable, a little more toward the bottom.
            Children.Add(shade);
            Children.Add(grain);
            SizeChanged+=(s,e)=>Fit();
            IsVisibleChanged+=(s,e)=>Run(IsVisible&&!holding&&active);
        }
        void Fit()
        {
            double side=Math.Max(ActualWidth,ActualHeight);
            for(int i=0;i<6;i++)
            {
                double size=side*ScaleOf[i%3];
                layers[i].Width=layers[i].Height=size;
                Canvas.SetLeft(layers[i],(ActualWidth-size)/2);
                Canvas.SetTop(layers[i],(ActualHeight-size)/2);
            }
            if(running&&clocks.Count==0)Build();
        }
        int shown=-1,coverToken;
        // Moves to a new cover: its colors are worked out off the app's own thread, then they fade in over the old ones while those fade out.
        // Set by the app, which wants this and the other backdrops to change together (see ColorSync).
        internal ColorSync Sync;
        // How colorful the colors are made (0: the usual amount of the players); the app asks for more.
        internal double Chroma;
        // True when new colors are being worked out (and the sync will hear from it).
        internal bool SetCover(BitmapSource cover)
        {
            hasCover=cover!=null;
            int token=++coverToken;
            if(cover==null)
            {
                foreach(var layer in layers)layer.BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(500)));
                shown=-1;
                return false;
            }
            int mode=player?2:rich?1:0;double cap=LightCap,chroma=Chroma;int forRound=Sync==null?0:Sync.Round;
            System.Threading.Tasks.Task.Run(()=>SmoothSet(cover,mode,cap,chroma)).ContinueWith(task=>
            {
                if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion)return;
                var small=task.Result;
                try
                {
                    Dispatcher.BeginInvoke(new Action(()=>
                    {
                        if(token!=coverToken)return;
                        Action start=()=>{if(token==coverToken)Fade(small);};
                        if(Sync!=null)Sync.Offer(forRound,start);else start();
                    }));
                }
                catch(Exception){}
            });
            return true;
        }
        void Fade(BitmapSource[] fields)
        {
            int next=shown<0?0:1-shown;
            var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};
            for(int i=0;i<3;i++)
            {
                var coming=layers[next*3+i];coming.Source=fields[i];
                coming.BeginAnimation(OpacityProperty,new DoubleAnimation(StrengthOf[i],TimeSpan.FromMilliseconds(600)){EasingFunction=ease});
                if(shown>=0)layers[shown*3+i].BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(600)){EasingFunction=ease});
            }
            shown=next;
        }
        // The cover's colors are ready (worked out) when this is true; tests wait for it.
        internal bool Ready {get{return shown>=0&&layers[shown*3].Source!=null;}}
        // The cover shrunk to a dozen pixels, then spread back out with a cubic B-spline, which has no corners and
        // no ringing: just soft color fields, however far the result is stretched.
        internal static BitmapSource Smooth(BitmapSource cover){return SmoothSet(cover)[0];}
        // Fine enough that stretching it across the window shows no faint diagonal bands (a small picture, enlarged, has them where its pixels meet).
        const int Fine=512;
        // Three different color fields from one cover, so the layers that cross each other do not all carry the same colors (which blends to one grey mist):
        // the cover's own colors, made more vivid; the same colors turned over, so they meet different ones; and soft glows of its
        // most colorful places.
        // The one background of every player: the same look, the same motion. (The mini player only changes how fast it moves, by its own setting.)
        internal static FlowingBackdrop ForPlayers(){return new FlowingBackdrop {Player=true,LightCap=185,Shade=.45,Lively=true,Speed=2.6,Travel=1.5,Rate=60};}
        internal static BitmapSource[] SmoothSet(BitmapSource cover){return SmoothSet(cover,0,140);}
        // What drifts behind the app while nothing is playing (or the music's colors are off): a made-up "cover" of soft deep blue, indigo and teal on near-black,
        // so the window moves gently instead of sitting flat and black.
        static BitmapSource idleCover;
        internal static BitmapSource IdleCover()
        {
            if(idleCover!=null)return idleCover;
            const int S=96;var px=new byte[S*S*4];
            // x, y, size, then red, green, blue of each glow
            double[][] glows={new[]{.72,.26,.34,24,38,118},new[]{.22,.72,.34,8,66,86},new[]{.38,.18,.26,50,28,104},new[]{.82,.82,.30,16,28,72}};
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                double r=6,g=7,b=10,nx=x/(double)(S-1),ny=y/(double)(S-1);
                foreach(var o in glows){double d=((nx-o[0])*(nx-o[0])+(ny-o[1])*(ny-o[1]))/(o[2]*o[2]),k=Math.Exp(-d*1.6);r+=o[3]*k;g+=o[4]*k;b+=o[5]*k;}
                int i=(y*S+x)*4;px[i]=(byte)Math.Min(255,b);px[i+1]=(byte)Math.Min(255,g);px[i+2]=(byte)Math.Min(255,r);px[i+3]=255;
            }
            var bmp=BitmapSource.Create(S,S,96,96,PixelFormats.Bgra32,null,px,S*4);bmp.Freeze();
            return idleCover=bmp;
        }
        // Mode 0 is the plain background, 1 the rich one (the app), 2 the one of the players.
        internal static BitmapSource[] SmoothSet(BitmapSource cover,int mode,double lightCap,double chroma=0)
        {
            bool rich=mode==1;
            int Coarse=mode==2?32:12;
            BitmapSource shrunk=new TransformedBitmap(cover,new ScaleTransform((double)Coarse/cover.PixelWidth,(double)Coarse/cover.PixelHeight));
            shrunk=new FormatConvertedBitmap(shrunk,PixelFormats.Bgra32,null,0);
            int width=shrunk.PixelWidth,height=shrunk.PixelHeight,stride=width*4;
            var source=new byte[stride*height];
            shrunk.CopyPixels(source,stride,0);
            // The players keep the cover's full range of light (deep blacks stay black, highlights stay bright) instead of an even grey.
            Vivid(source,mode==0?125:lightCap,mode==0?1.3:mode==1?1.2:(chroma>0?chroma:1.12),mode==2?1.75:1.15,mode==2?0:24,mode==2);
            var moved=new byte[source.Length];
            
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)Array.Copy(source,(height-1-y)*stride+x*4,moved,y*stride+x*4,4);
            if(mode==2)
            {
                // The same picture turned a quarter of the way, so the two copies show different parts of it where they cross.
                var turned=new byte[source.Length];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)Array.Copy(source,(y*width+x)*4,turned,(x*height+(height-1-y))*4,4);
                return new[]{Spread(source,width,height),Spread(turned,height,width),Blobs(source,width,height,1,true)};
            }
            if(rich)return new[]{Spread(source,width,height),Blobs(source,width,height,1,false),Blobs(source,width,height,0,false)};
            return new[]{Spread(source,width,height),Spread(moved,width,height),Glows(source,width,height)};
        }
        // More color and a wider range of light: a cover of soft colors would otherwise drift as one pale haze.
        static void Vivid(byte[] pixels,double cap,double chroma,double contrast,double floor,bool deep)
        {
            double mean=0;int count=pixels.Length/4;
            for(int i=0;i<pixels.Length;i+=4)mean+=pixels[i]*.0722+pixels[i+1]*.7152+pixels[i+2]*.2126;
            mean/=Math.Max(1,count);
            for(int i=0;i<pixels.Length;i+=4)
            {
                double b=pixels[i],g=pixels[i+1],r=pixels[i+2],luma=r*.2126+g*.7152+b*.0722;
                double target=Math.Max(floor,Math.Min(cap,mean+(luma-mean)*contrast));if(deep)target=.72*255*Math.Pow(target/255,1.3);
                // A pixel made much darker keeps its color in proportion (the same share of it), not the same amount: the same amount of color on a near-black
                // pixel is a saturated deep red or blue, which turned the brown of a face on a bright blue cover into big dark red areas.
                double share=deep&&luma>1?Math.Min(1,target/luma):1;
                pixels[i]=(byte)Math.Max(0,Math.Min(255,target+(b-luma)*chroma*share));
                pixels[i+1]=(byte)Math.Max(0,Math.Min(255,target+(g-luma)*chroma*share));
                pixels[i+2]=(byte)Math.Max(0,Math.Min(255,target+(r-luma)*chroma*share));
            }
        }
        // The rich background's pools. Kind 0: pools in the cover's most different colors (up to five). Kind 1: pools of light and of shadow, lighter and darker
        // versions of the cover's colors, so there is something to see moving on a white cover (shadows drift over it) and on a black one (light does). Each pool is
        // see-through at its edge, so the pools of different layers mix instead of cutting each other off.
        static BitmapSource Blobs(byte[] source,int width,int height,int kind,bool gentle)
        {
            int count=width*height;double avgR=0,avgG=0,avgB=0;
            for(int i=0;i<count;i++){avgB+=source[i*4];avgG+=source[i*4+1];avgR+=source[i*4+2];}
            avgR/=count;avgG/=count;avgB/=count;
            double avgLuma=avgR*.2126+avgG*.7152+avgB*.0722;
            var picks=new System.Collections.Generic.List<double[]>();
            for(int pick=0;pick<5;pick++)
            {
                double best=-1;double[] chosen=null;
                for(int p=0;p<count;p++)
                {
                    double b=source[p*4],g=source[p*4+1],r=source[p*4+2],top=Math.Max(r,Math.Max(g,b)),low=Math.Min(r,Math.Min(g,b)),far=double.MaxValue;
                    foreach(var c in picks){double dr=r-c[0],dg=g-c[1],db=b-c[2];far=Math.Min(far,Math.Sqrt(dr*dr+dg*dg+db*db));}
                    if(picks.Count>0&&far<30)continue;
                    double score=picks.Count==0?(top-low)+.3*top:far*(.6+(top-low)/255);
                    if(score>best){best=score;chosen=new[]{r,g,b};}
                }
                if(chosen==null)break;
                picks.Add(chosen);
            }
            if(picks.Count==0)picks.Add(new[]{avgR,avgG,avgB});
            var colors=new System.Collections.Generic.List<double[]>();var strengths=new System.Collections.Generic.List<double>();
            if(kind==0)
            {
                for(int k=0;k<picks.Count;k++){colors.Add(LimitLight(picks[k],140));strengths.Add(.9-.05*k);}
            }
            else
            {
                for(int k=0;k<3;k++)
                {
                    // The players only add shadows, and only over a light cover: grey pools on a dark cover were the grey mist.
                    if(gentle&&avgLuma<110)break;
                    // The players' shadows are shadows of the cover as a whole (its average color), not of its most colorful patch: shadows of a small warm patch
                    // (a face on a blue sky) were large dark red pools over the whole window.
                    var basis=gentle?new[]{avgR,avgG,avgB}:picks[(k+1)%picks.Count];
                    bool lighten=gentle?false:avgLuma<70?true:avgLuma>140?false:k%2==0;
                    var tone=new double[3];
                    for(int c=0;c<3;c++)tone[c]=lighten?basis[c]+(255-basis[c])*(gentle?.16:.4):basis[c]*(gentle?.45:.4);
                    colors.Add(LimitLight(tone,140));strengths.Add(gentle?(lighten?.4:.55):(lighten?.6:.7));
                }
            }
            int n=colors.Count;var cx=new double[n];var cy=new double[n];var sigma=new double[n];
            for(int k=0;k<n;k++)
            {
                double angle=k*2.399963+kind*1.1,radius=.10+.055*((k*3+kind)%5);
                cx[k]=(.5+radius*Math.Cos(angle))*Fine;cy[k]=(.5+radius*Math.Sin(angle))*Fine;sigma[k]=(.11+.02*((k+kind)%3))*Fine;
            }
            var output=new byte[Fine*Fine*4];
            for(int y=0;y<Fine;y++)for(int x=0;x<Fine;x++)
            {
                double red=0,green=0,blue=0,alpha=0;
                for(int k=0;k<n;k++)
                {
                    double w=Math.Exp(-((x-cx[k])*(x-cx[k])+(y-cy[k])*(y-cy[k]))/(2*sigma[k]*sigma[k]))*strengths[k];
                    red=red*(1-w)+colors[k][0]*w;green=green*(1-w)+colors[k][1]*w;blue=blue*(1-w)+colors[k][2]*w;alpha+=w*(1-alpha);
                }
                int to=(y*Fine+x)*4;
                output[to]=(byte)Math.Min(255,blue);output[to+1]=(byte)Math.Min(255,green);output[to+2]=(byte)Math.Min(255,red);output[to+3]=(byte)Math.Min(255,255*alpha);
            }
            var pools=BitmapSource.Create(Fine,Fine,96,96,PixelFormats.Pbgra32,null,output,Fine*4);
            pools.Freeze();
            return pools;
        }
        // No brighter than white text can sit on.
        static double[] LimitLight(double[] rgb,double limit)
        {
            double luma=rgb[0]*.2126+rgb[1]*.7152+rgb[2]*.0722,gain=luma>limit?limit/luma:1;
            return new[]{rgb[0]*gain,rgb[1]*gain,rgb[2]*gain};
        }
        // Up to three soft glows, one on each of the cover's most colorful places (kept apart), on a dark ground.
        static BitmapSource Glows(byte[] source,int width,int height)
        {
            int stride=width*4;
            var places=new System.Collections.Generic.List<int[]>();
            for(int pick=0;pick<3;pick++)
            {
                double best=0;int bestX=-1,bestY=-1;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int at=y*stride+x*4;double top=Math.Max(source[at],Math.Max(source[at+1],source[at+2])),low=Math.Min(source[at],Math.Min(source[at+1],source[at+2]));
                    double score=top<=0?0:(top-low)/top*top/255;
                    bool apart=true;
                    foreach(var place in places)if((x-place[0])*(x-place[0])+(y-place[1])*(y-place[1])<width*width/9)apart=false;
                    if(apart&&score>best){best=score;bestX=x;bestY=y;}
                }
                if(bestX<0)break;
                places.Add(new[]{bestX,bestY});
            }
            var output=new byte[Fine*Fine*4];
            double sigma=Fine*.22;
            var tones=new double[places.Count][];
            for(int k=0;k<places.Count;k++){int at=places[k][1]*stride+places[k][0]*4;tones[k]=new double[]{source[at+2],source[at+1],source[at]};}
            // A flat cover (every glow nearly the same hue) gets neighbors on the color wheel, a little lighter and darker, so there is always something to see moving.
            bool flat=places.Count>1;
            for(int k=1;k<places.Count&&flat;k++){double gap=Math.Abs(HueOf(tones[k])-HueOf(tones[0]));if(Math.Min(gap,360-gap)>28)flat=false;}
            if(flat)for(int k=1;k<places.Count;k++)tones[k]=Spin(tones[k],k%2==1?38:-38,k==1?1.2:.82);
            // The glows are pools of the cover's own light: a place that is dark in the cover (a faint patch on a black cover) gives only a faint pool,
            // and none is brighter than white text can sit on.
            var lit=new double[places.Count];
            for(int k=0;k<places.Count;k++)
            {
                double top=Math.Max(tones[k][0],Math.Max(tones[k][1],tones[k][2]));
                lit[k]=Math.Min(1,top/150);
                double luma=tones[k][0]*.2126+tones[k][1]*.7152+tones[k][2]*.0722;
                if(luma>125)for(int c=0;c<3;c++)tones[k][c]*=125/luma;
            }            for(int y=0;y<Fine;y++)for(int x=0;x<Fine;x++)
            {
                double total=0,red=0,green=0,blue=0;
                foreach(var place in places)
                {
                    double cx=(place[0]+.5)*Fine/width,cy=(place[1]+.5)*Fine/height,weight=Math.Exp(-((x-cx)*(x-cx)+(y-cy)*(y-cy))/(2*sigma*sigma))*lit[places.IndexOf(place)];
                    var tone=tones[places.IndexOf(place)];
                    total+=weight;blue+=weight*tone[2];green+=weight*tone[1];red+=weight*tone[0];
                }
                double mix=Math.Min(1,total);
                if(total>0){blue/=total;green/=total;red/=total;}
                int to=(y*Fine+x)*4;
                output[to]=(byte)(blue*mix);output[to+1]=(byte)(green*mix);output[to+2]=(byte)(red*mix);output[to+3]=(byte)(255*mix);
            }
            var glows=BitmapSource.Create(Fine,Fine,96,96,PixelFormats.Pbgra32,null,output,Fine*4);
            glows.Freeze();
            return glows;
        }
        static double HueOf(double[] rgb)
        {
            double r=rgb[0]/255,g=rgb[1]/255,b=rgb[2]/255,max=Math.Max(r,Math.Max(g,b)),d=max-Math.Min(r,Math.Min(g,b)),h=0;
            if(d>0){h=max==r?((g-b)/d)%6:max==g?(b-r)/d+2:(r-g)/d+4;h*=60;if(h<0)h+=360;}
            return h;
        }
        // The same color turned around the color wheel and made lighter or darker.
        static double[] Spin(double[] rgb,double degrees,double light)
        {
            double max=Math.Max(rgb[0],Math.Max(rgb[1],rgb[2]))/255,d=max-Math.Min(rgb[0],Math.Min(rgb[1],rgb[2]))/255,s=max<=0?0:d/max,v=Math.Min(1,max*light);
            double h=(HueOf(rgb)+degrees+360)%360,c=v*s,x=c*(1-Math.Abs((h/60)%2-1)),m=v-c,r=0,g=0,b=0;
            switch((int)(h/60)%6){case 0:r=c;g=x;break;case 1:r=x;g=c;break;case 2:g=c;b=x;break;case 3:g=x;b=c;break;case 4:r=x;b=c;break;default:r=c;b=x;break;}
            return new[]{(r+m)*255,(g+m)*255,(b+m)*255};
        }
        static BitmapSource Spread(byte[] source,int width,int height)
        {
            int stride=width*4;
            var output=new byte[Fine*Fine*4];
            var across=new double[Fine][];var acrossStart=new int[Fine];
            for(int x=0;x<Fine;x++){double u=(x+.5)*width/Fine-.5;int u0=(int)Math.Floor(u);across[x]=Spline(u-u0);acrossStart[x]=u0;}
            for(int y=0;y<Fine;y++)
            {
                double v=(y+.5)*height/Fine-.5;int v0=(int)Math.Floor(v);double[] wv=Spline(v-v0);
            for(int x=0;x<Fine;x++)
            {
                int u0=acrossStart[x];double[] wu=across[x];
                double blue=0,green=0,red=0;
                for(int j=0;j<4;j++)
                for(int i=0;i<4;i++)
                {
                    int sx=Math.Max(0,Math.Min(width-1,u0-1+i)),sy=Math.Max(0,Math.Min(height-1,v0-1+j));
                    double weight=wu[i]*wv[j];int at=sy*stride+sx*4;
                    blue+=source[at]*weight;green+=source[at+1]*weight;red+=source[at+2]*weight;
                }
                int to=(y*Fine+x)*4;
                output[to]=(byte)Math.Max(0,Math.Min(255,Math.Round(blue)));
                output[to+1]=(byte)Math.Max(0,Math.Min(255,Math.Round(green)));
                output[to+2]=(byte)Math.Max(0,Math.Min(255,Math.Round(red)));
                output[to+3]=255;
            }
            }
            var result=BitmapSource.Create(Fine,Fine,96,96,PixelFormats.Bgra32,null,output,Fine*4);
            result.Freeze();
            return result;
        }
        static double[] Spline(double t)
        {
            return new[]{(1-t)*(1-t)*(1-t)/6,(3*t*t*t-6*t*t+4)/6,(-3*t*t*t+3*t*t+3*t+1)/6,t*t*t/6};
        }
        internal const int FrameRate=30;
        // How many times a second the colors are redrawn. The main window keeps the modest default; the player asks for the full rate so its motion is smooth.
        int rate=FrameRate;
        internal int Rate
        {
            get{return rate;}
            set{value=Math.Max(10,Math.Min(60,value));if(value==rate)return;rate=value;Rebuild();}
        }
        double speed=1;
        // How fast the colors drift: 1 is the normal pace, 0 holds them still.
        internal double Speed
        {
            get{return speed;}
            set{value=Math.Max(0,Math.Min(6,value));if(Math.Abs(value-speed)<.001)return;speed=value;Rebuild();}
        }
        // How far the colors slide and swell compared with the usual: the main window, where the colors only show at the edges, asks for more.
        double travel=1;
        internal double Travel
        {
            get{return travel;}
            set{value=Math.Max(.5,Math.Min(3,value));if(Math.Abs(value-travel)<.001)return;travel=value;Rebuild();}
        }
        bool lively,holding;
        // While the window is being dragged larger or smaller the motion stops, so every frame goes to resizing.
        internal void Hold(bool on){holding=on;Run(IsVisible&&!on&&active);}
        // The motion also stops while nothing is playing.
        bool active=true;
        internal void SetActive(bool on){if(on==active)return;active=on;Run(IsVisible&&!holding&&active);}
        // A livelier drift for small windows: the colors also swell and shrink, and slide further.
        internal bool Lively
        {
            get{return lively;}
            set{if(value==lively)return;lively=value;Rebuild();}
        }
        internal bool Running {get{return running;}}

        // The motion is made of clocks that are paused and resumed, so pausing the music holds the colors where they are and playing carries on
        // from there. (Stopping the animations used to put every layer back at its start, and playing began them again from the start.)
        readonly System.Collections.Generic.List<Tuple<Animatable,DependencyProperty,AnimationClock>> clocks=new System.Collections.Generic.List<Tuple<Animatable,DependencyProperty,AnimationClock>>();
        void Start(Animatable target,DependencyProperty property,AnimationTimeline animation)
        {
            // The colors move slowly, so they are drawn at a modest rate: the window is redrawn that many times a second instead of sixty.
            Timeline.SetDesiredFrameRate(animation,rate);
            var clock=animation.CreateClock();
            target.ApplyAnimationClock(property,clock);
            clocks.Add(Tuple.Create(target,property,clock));
        }
        void Clear()
        {
            foreach(var entry in clocks)entry.Item1.ApplyAnimationClock(entry.Item2,null);
            clocks.Clear();
            for(int i=0;i<3;i++){turns[i].Angle=0;slides[i].X=slides[i].Y=0;breathe[i].ScaleX=breathe[i].ScaleY=1;}
        }
        void Rebuild()
        {
            Clear();
            if(running)Build();
        }
        void Build()
        {
            if(speed<=.001||ActualWidth<=0)return;
            for(int i=0;i<3;i++)
            {
                double direction=i==1?-1:1,reach=Math.Max(ActualWidth,ActualHeight)*SlideOf[i]*(lively?1.9*Travel:1);
                Start(turns[i],RotateTransform.AngleProperty,new DoubleAnimation(0,360*direction,TimeSpan.FromSeconds(TurnOf[i]/speed)){RepeatBehavior=RepeatBehavior.Forever});
                var ease=new SineEase {EasingMode=EasingMode.EaseInOut};
                Start(slides[i],TranslateTransform.XProperty,new DoubleAnimation(-reach,reach,TimeSpan.FromSeconds(PaceOf[i]/speed)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=ease});
                Start(slides[i],TranslateTransform.YProperty,new DoubleAnimation(reach,-reach,TimeSpan.FromSeconds(PaceOf[i]*1.3/speed)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=ease});
                if(lively)
                {
                    var swell=new DoubleAnimation(1,1+.24*Travel,TimeSpan.FromSeconds((7+i*3)/speed)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=ease};
                    Start(breathe[i],ScaleTransform.ScaleXProperty,swell);Start(breathe[i],ScaleTransform.ScaleYProperty,swell.Clone());
                }
            }
        }
        // The motion only runs while the player is on screen and something is playing. Pausing keeps the clock times and holds each
        // layer at its current value; resuming builds the clocks again and seeks them to those times, so the colors carry on from there.
        // (Pausing the clocks themselves left them drifting on some systems.)
        TimeSpan[] heldTimes;
        void Run(bool on)
        {
            if(on==running)return;
            running=on;
            if(on)
            {
                var times=heldTimes;heldTimes=null;
                if(clocks.Count>0)Clear();
                Build();
                if(times!=null&&times.Length==clocks.Count)
                    for(int i=0;i<clocks.Count;i++)clocks[i].Item3.Controller.Seek(times[i],System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
            }
            else if(clocks.Count>0)
            {
                heldTimes=new TimeSpan[clocks.Count];
                var values=new object[clocks.Count];
                for(int i=0;i<clocks.Count;i++){heldTimes[i]=clocks[i].Item3.CurrentTime??TimeSpan.Zero;values[i]=clocks[i].Item1.GetValue(clocks[i].Item2);}
                for(int i=0;i<clocks.Count;i++){var entry=clocks[i];entry.Item1.ApplyAnimationClock(entry.Item2,null);entry.Item1.SetValue(entry.Item2,values[i]);}
                clocks.Clear();
            }
        }
    }
}
