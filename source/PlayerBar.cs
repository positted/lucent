using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Lucent
{
    // The now-playing bar along the bottom of the main window: cover and track on the left, the controls and a
    // seekable progress bar in the middle, the volume, and room on the right for the player buttons, which stay where they are.
    internal sealed class PlayerBar:Border
    {
        // The bar is centered on the player buttons: 9 pixels below the page area's bottom edge, with 10 pixels between it and the page.
        // BarBottom: the bar floats this far above the window's bottom edge, so its whole rim shows (it used to sit 7 px past the edge, which cut the bottom of the glass off).
        internal const double BarHeight=68,BarBottom=6,SideRoom=172,VolumeRoom=206,PageInset=BarHeight+BarBottom+8;
        // The bar lines up with the sections of the page on both sides: they begin 18 in from the page area and end 10 before its right edge (where the page scrolls).
        internal const double BarLeft=18,BarRight=10;
        internal readonly SolidColorBrush Surface;
        internal readonly BarBackdrop Backdrop=new BarBackdrop();
        readonly ArtistBadge artistPicture=new ArtistBadge(16);
        internal ArtistBadge ArtistPicture {get{return artistPicture;}}
        internal void RefreshArtistPicture(){var track=Core.Track;artistPicture.Show(track==null?null:track.Artist,!testing&&ArtistPictures.Enabled());}
        readonly bool testing;
        // The bar's tint. With the colored band behind it (see BarBackdrop) it is only a light dark veil over the band; without the band
        // (performance mode) it is the whole bar: strength 0 is plain dark, more of it lets the cover's color through.
        internal static Color SurfaceColor(Color accent,double strength,bool band=true,bool glassy=true)
        {
            var dark=Color.FromRgb(18,18,20);
            if(band)return Color.FromArgb(glassy?(byte)48:(byte)62,(byte)(12+accent.R*.1),(byte)(12+accent.G*.1),(byte)(12+accent.B*.1));
            bool glass=false;
            if(strength<=0)return Color.FromArgb(glass?(byte)168:(byte)236,dark.R,dark.G,dark.B);
            double lift=Math.Min(.8,.25+.35*Math.Max(0,Math.Min(1,strength)));
            Func<byte,byte,byte> mix=(from,to)=>(byte)(from+(to-from)*lift);
            // Darker than the page around it, so it reads as a card sitting on it.
            double through=.32;
            return Color.FromArgb(glass?(byte)176:(byte)238,mix(dark.R,(byte)(accent.R*through)),mix(dark.G,(byte)(accent.G*through)),mix(dark.B,(byte)(accent.B*through)));
        }
        internal readonly PlayerCore Core;
        internal readonly VolumeControl Volume;
        readonly SeekBar seek=new SeekBar {Margin=new Thickness(10,0,10,0),VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock elapsed=new TextBlock {FontSize=11,FontWeight=FontWeights.Medium,Text="0:00",Width=38,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock remaining=new TextBlock {FontSize=11,FontWeight=FontWeights.Medium,Text="0:00",Width=38,VerticalAlignment=VerticalAlignment.Center};
        readonly StackPanel labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),RenderTransform=new TranslateTransform()};
        readonly TranslateTransform slide=new TranslateTransform();
        int shownElapsed=-1,shownLength=-1;
        bool open;

        // Opens the big player; set by the app.
        internal Action OpenBig=()=>{};
        public PlayerBar(Window owner,bool testMode)
        {
            testing=testMode;
            Design.SetStill(this,true);
            Visibility=Visibility.Collapsed;
            Opacity=0;
            Height=BarHeight;
            VerticalAlignment=VerticalAlignment.Bottom;
            Margin=new Thickness(BarLeft,0,BarRight,BarBottom);
            CornerRadius=new CornerRadius(14);

            // The bar takes a dark version of the cover's color, like a card of its own; plain dark with nothing to take it from.
            Surface=new SolidColorBrush(SurfaceColor(Color.FromRgb(46,46,48),0));
            Surface.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Surface.Color,Surface.Color,TimeSpan.Zero));
            // The bar carries the cover's colors as a band across it.
            RenderTransform=slide;
            Core=new PlayerCore(this,testMode) {artworkPixels=128,showProgress=ShowProgress,showNote=note=>Core.title.ToolTip=note,trackChanged=TrackChanged};
            Volume=new VolumeControl(testMode,17,66);
            Volume.Source=()=>Core.Source;
            owner.StateChanged+=(s,e)=>Core.UpdateProgressFrames();
            seek.Preview=fraction=>elapsed.Text=Time((int)(fraction*Core.progressLength));
            seek.Commit=async fraction=>{shownElapsed=-1;await Core.Seek(fraction*Core.progressLength);};

            var layout=new Grid {Margin=new Thickness(14,0,0,0)};
            layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(250)});
            layout.ColumnDefinitions.Add(new ColumnDefinition());
            layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(VolumeRoom)});
            layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(SideRoom)});
            // The page runs on underneath the bar, so the bar must catch the pointer itself (a see-through background still counts): without it the cards behind the glass lit up and showed their tooltips.
            var stack=new Grid {Background=Brushes.Transparent};
            // What is behind the bar (the page, which runs on under it) is seen a little blurred: a copy of that strip of the page, blurred. It is larger than the bar
            // so the blur has no faded edge inside it (the bar clips it to its own shape), and it is off in performance mode.
            blur=new Grid {Margin=new Thickness(-BlurBleed),IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.BlurEffect {Radius=20,RenderingBias=System.Windows.Media.Effects.RenderingBias.Performance,KernelType=System.Windows.Media.Effects.KernelType.Gaussian}};
            Backdrop.Toggled=on=>ShowGlassParts();
            // The layers of the glass are cut to the bar's rounded shape with a mask, not a clip: a clip's edge is not smoothed, so the corners were stair-stepped.
            var glassLayers=new Grid {IsHitTestVisible=false};
            glassLayers.Children.Add(blur);
            glassLayers.Children.Add(Backdrop);
            glassLayers.Children.Add(new Border {Background=Surface,CornerRadius=new CornerRadius(14),IsHitTestVisible=false});
            stack.Children.Add(glassLayers);
            // The glass: a soft sheen from the top left, and a thin lit edge that is brightest along the top, like the rim of a pane of glass.
            var sheen=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(18,255,255,255),0),new GradientStop(Color.FromArgb(5,255,255,255),.5),new GradientStop(Color.FromArgb(0,255,255,255),1)},new Point(0,0),new Point(.7,1));
                        sheen.Freeze();
            var rimLayer=new Grid {IsHitTestVisible=false};rimLayer.Children.Add(new Border {Background=sheen,CornerRadius=new CornerRadius(14),IsHitTestVisible=false});rimLayer.Children.Add(Design.GlassEdge(14));glassRim=rimLayer;
            stack.Children.Add(glassRim);
            stack.Children.Add(layout);
            stack.SizeChanged+=(s,e)=>{if(stack.ActualWidth>0&&stack.ActualHeight>0)glassLayers.OpacityMask=RoundedMask(stack.ActualWidth,stack.ActualHeight,14);};
            Child=stack;

            var track=new Grid {VerticalAlignment=VerticalAlignment.Center};
            track.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});
            track.ColumnDefinitions.Add(new ColumnDefinition());
            var cover=new Grid {Width=40,Height=40,Background=new SolidColorBrush(Color.FromRgb(48,48,51)),OpacityMask=RoundedMask(40,40,10)};
            cover.Children.Add(Design.Icon("Music",20));
            cover.Children.Add(Core.artwork);
            // Clicking the cover opens the big player.
            cover.Cursor=Cursors.Hand;
            cover.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;OpenBig();};
            System.Windows.Automation.AutomationProperties.SetName(cover,"Open the player");
            track.Children.Add(cover);
            Core.title.FontSize=14;
            Core.title.FontWeight=FontWeights.Bold;
            Core.artist.FontSize=12;
            Core.artist.Foreground=new SolidColorBrush(Color.FromRgb(196,196,202));
            var artistRow=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,2,0,0)};
            Core.artist.VerticalAlignment=VerticalAlignment.Center;
            artistRow.Children.Add(artistPicture);artistRow.Children.Add(Core.artist);
            labels.Children.Add(Core.title);
            labels.Children.Add(artistRow);
            Grid.SetColumn(labels,1);
            track.Children.Add(labels);
            layout.Children.Add(track);

            Core.ConfigureTransport(34,18,36,22);
            Core.shuffle.Margin=Core.repeat.Margin=new Thickness(6,0,6,0);
            Core.previous.Margin=Core.next.Margin=new Thickness(6,0,6,0);
            var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Height=36};
            controls.Children.Add(Core.previous);
            controls.Children.Add(Core.play);
            controls.Children.Add(Core.next);
            var progress=new Grid {Height=18,MaxWidth=560};
            progress.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            progress.ColumnDefinitions.Add(new ColumnDefinition());
            progress.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            remaining.Foreground=elapsed.Foreground=new SolidColorBrush(Color.FromArgb(170,255,255,255));
            // Like the bar this is modeled on there are no times, only a slim line to seek on.
            remaining.Visibility=elapsed.Visibility=Visibility.Collapsed;
            progress.Children.Add(elapsed);
            Grid.SetColumn(seek,1);
            progress.Children.Add(seek);
            Grid.SetColumn(remaining,2);
            progress.Children.Add(remaining);
            var center=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,1,0,0)};
            center.Children.Add(controls);
            center.Children.Add(progress);
            Grid.SetColumn(center,1);
            layout.Children.Add(center);
            // Shuffle and repeat sit with the volume on the right, and only show for apps that support them.
            Volume.HorizontalAlignment=HorizontalAlignment.Center;
            var right=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            right.Children.Add(Core.shuffle);right.Children.Add(Core.repeat);right.Children.Add(Volume);
            Grid.SetColumn(right,2);
            layout.Children.Add(right);
        }
        // A rounded rectangle as a mask (its edge is smoothed, unlike a clip's): what it covers is drawn, the rest is not.
        internal static Brush RoundedMask(double width,double height,double radius)
        {
            var drawing=new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(0,0,width,height),radius,radius));
            // Mapped in the element's own units (not to the size of what it draws): the blur around the bar reaches past its edges, and a mask that followed that size was stretched with it.
            var brush=new DrawingBrush(drawing){Stretch=Stretch.Fill,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,width,height),ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,width,height),TileMode=TileMode.None};
            brush.Freeze();return brush;
        }
        const double BlurBleed=36;
        Grid blur;
        FrameworkElement glassRim;
        bool glass=true;
        internal bool IsGlass {get{return glass;}}
        // Glass is the blur of the page behind the bar, the sheen and the lit rim, and the thinner band; without it the bar is the older, more solid one.
        internal void SetGlass(bool on)
        {
            glass=on;Backdrop.Glass=on;ShowGlassParts();
        }
        void ShowGlassParts()
        {
            if(blur==null||glassRim==null)return;
            blur.Visibility=glass&&Backdrop.On?Visibility.Visible:Visibility.Collapsed;
            glassRim.Visibility=glass?Visibility.Visible:Visibility.Collapsed;
        }
        // What the blur shows is a copy of the strip behind the bar: the window's colors, the moving colors and the page (each a copy of its own, so that together they cover
        // what is really behind and the sharp page does not show through the soft one). The copies are blurred together.
        VisualBrush[] blurBrushes;Visual[] blurSources;
        internal void SetBlurSources(params Visual[] sources)
        {
            blurSources=sources;blurBrushes=new VisualBrush[sources.Length];
            blur.Children.Clear();
            for(int i=0;i<sources.Length;i++)
            {
                blurBrushes[i]=new VisualBrush(sources[i]) {ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};
                blur.Children.Add(new System.Windows.Shapes.Rectangle {Fill=blurBrushes[i],IsHitTestVisible=false});
            }
        }
        // Points each copy at the place the bar is over, in that source's own coordinates (called when the window or the bar changes size).
        internal void FitBlur()
        {
            if(blurSources==null||ActualWidth<=0||ActualHeight<=0)return;
            for(int i=0;i<blurSources.Length;i++)
            {
                try
                {
                    var area=TransformToVisual(blurSources[i]).TransformBounds(new Rect(-BlurBleed,-BlurBleed,ActualWidth+2*BlurBleed,ActualHeight+2*BlurBleed));
                    if(area.Width>0&&area.Height>0&&blurBrushes[i].Viewbox!=area)blurBrushes[i].Viewbox=area;
                }
                catch(InvalidOperationException){}
            }
        }        internal void SetOpen(bool show)
        {
            if(show==open)return;
            open=show;
            var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
            if(show)
            {
                Visibility=Visibility.Visible;
                Design.Animate(this,OpacityProperty,Opacity,1,240);
                slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(BarHeight*.5,0,TimeSpan.FromMilliseconds(280)){EasingFunction=ease});
                var start=Core.Start();
                return;
            }
            Core.Stop();
            var fade=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(170));
            fade.Completed+=(s,e)=>{if(!open)Visibility=Visibility.Collapsed;};
            BeginAnimation(OpacityProperty,fade);
            slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(slide.Y,BarHeight*.35,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});
        }
        void ShowProgress()
        {
            seek.Seekable=Core.canSeek;
            seek.SetFraction(Core.progressValue/Core.progressLength);
            if(seek.Dragging)return;
            int now=(int)Core.progressValue,length=(int)Core.progressLength;
            if(now!=shownElapsed){shownElapsed=now;elapsed.Text=Time(now);}
            if(length!=shownLength){shownLength=length;remaining.Text=Time(length);}
        }
        static string Time(int seconds)
        {
            var span=TimeSpan.FromSeconds(seconds);
            return (int)span.TotalMinutes+":"+span.Seconds.ToString("00");
        }
        void TrackChanged()
        {
            RefreshArtistPicture();
            Design.Animate(labels,OpacityProperty,.15,1,200);
            Design.Animate((TranslateTransform)labels.RenderTransform,TranslateTransform.YProperty,5,0,220);
        }
    }
}
