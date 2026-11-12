using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Lucent
{
    // The mini player: a small floating card over the colors of the cover. It has two views, the player alone and the player with lyrics, and each
    // view remembers the size it was given. Everything inside scales with the width of the window, so it can be as small or as large as wanted.
    // No Owner: an owned window would disappear whenever the main window is minimized or sent to the tray.
    internal sealed class MediaPlayerWindow:Window
    {
        // Sizes are given for a window 400 wide; a wider or narrower window scales everything by the same factor.
        internal const double BaseWidth=400,CompactHeight=176,LyricsHeight=580,MinScale=.75,MaxScale=3;
        internal readonly PlayerCore Core;
        internal readonly FlowingBackdrop Backdrop=FlowingBackdrop.ForPlayers();
        internal readonly VolumeControl Volume;
        // Closed, the lyrics are hidden in a row of no height but stay laid out at the window's width, so opening them only has to draw them.
        internal readonly LyricsView Lyrics=new LyricsView {Visibility=Visibility.Hidden};
        readonly Preferences preferences;
        readonly bool testMode;
        readonly ScaleTransform scale=new ScaleTransform(1,1);
        readonly Grid face=new Grid();
        readonly Grid page=new Grid();
        readonly RowDefinition lyricsRow=new RowDefinition {Height=new GridLength(0)};
        readonly Grid art=new Grid {Width=64,Height=64,Background=new SolidColorBrush(Color.FromRgb(38,38,41)),RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(1,1)};
        readonly StackPanel labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,RenderTransform=new TranslateTransform()};
        readonly SeekBar seek=new SeekBar();
        readonly TextBlock elapsed=new TextBlock {FontSize=12,FontWeight=FontWeights.Medium,Text="0:00",VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock remaining=new TextBlock {FontSize=12,FontWeight=FontWeights.Medium,Text="0:00",TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
        readonly Grid seekRow=new Grid();
        readonly Grid controls=new Grid();
        readonly StackPanel windowButtons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,8,10,0)};
        readonly DispatcherTimer idleTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(400)};
        Button lyricsButton;
        Grid lyricsFace;
        string lyricsSong;
        bool lyricsPending;Action lyricsArrived;
        int lyricsToken,shownElapsed=-1,shownLength=-1;
        bool lyricsOn,animating,faded;
        bool sizingByHand;
        DateTime lastActive=DateTime.UtcNow;
        // Set by the app so a change is saved.
        internal Action Changed=()=>{};
        // Tests put the window somewhere else than where the user's setting would.
        internal Point? StartAt;
        // Opens the mini player settings in Lucent. Set by the app.
        internal Action ShowAppSettings=()=>{};
        // 0 is everything showing, 1 is only the cover and the title: the progress bar and the buttons have left and the window has shrunk by their height.
        const double TuckRows=88;
        public static readonly DependencyProperty TuckProperty=DependencyProperty.Register("Tuck",typeof(double),typeof(MediaPlayerWindow),new PropertyMetadata(0d,(d,e)=>((MediaPlayerWindow)d).ApplyTuck()));
        double Tuck {get{return (double)GetValue(TuckProperty);}set{SetValue(TuckProperty,value);}}
        double tuckBaseHeight;
        bool tucking;
        internal double TuckAmount {get{return Tuck;}}
        void ApplyTuck()
        {
            double f=Math.Max(0,Math.Min(1,Tuck));
            page.RowDefinitions[2].Height=new GridLength(30*(1-f));
            page.RowDefinitions[3].Height=new GridLength(58*(1-f));
            // The controls arrive a little after the window has made room for them, and leave a little before it closes.
            double fade=Math.Max(0,Math.Min(1,(1-f-.15)/.85));
            seekRow.Opacity=controls.Opacity=windowButtons.Opacity=fade;
            seekRow.IsHitTestVisible=controls.IsHitTestVisible=windowButtons.IsHitTestVisible=f<.5;
            if(animating||!IsInitialized||ActualWidth<=0)return;
            double s=Scale;
            double height=lyricsOn?(tuckBaseHeight>0?tuckBaseHeight:ActualHeight)-TuckRows*f*s:(CompactHeight-TuckRows*f)*s;
            if(Math.Abs(ActualHeight-height)<.5)return;
            tucking=true;Height=height;tucking=false;
        }

        internal double Scale {get{return scale.ScaleX;}}
        internal bool LyricsMode {get{return lyricsOn;}}
        internal bool ControlsFaded {get{return faded;}}
        internal bool Resizing {get{return animating;}}
        internal SeekBar Seek {get{return seek;}}

        public MediaPlayerWindow(Window owner,Preferences preferences,bool testMode)
        {
            this.preferences=preferences??new Preferences();
            this.testMode=testMode;
            Core=new PlayerCore(this,testMode) {artworkPixels=256,showProgress=ShowProgress,showNote=note=>labels.ToolTip=note,trackChanged=TrackChanged,artworkChanged=ArtworkChanged};
            Volume=new VolumeControl(testMode,18,64);
            Volume.Source=()=>Core.Source;
            Title="Lucent · Mini player";
            if(testMode){WindowStartupLocation=WindowStartupLocation.Manual;Left=-20000;Top=-20000;ShowActivated=false;}
            // The mini player always opens without lyrics (they are switched on from its own button).
            lyricsOn=false;
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.CanResize;
            ShowInTaskbar=false;
            Topmost=preferences==null||preferences.PlayerAlwaysOnTop;
            Foreground=Brushes.White;
            FontFamily=Design.Font;
            Resources=owner.Resources;
            UseLayoutRounding=true;
            Design.SetStill(this,true);
            SmoothText.SetCrisp(this,true);
            Background=new SolidColorBrush(Color.FromRgb(13,13,16));
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
            TextOptions.SetTextHintingMode(this,TextHintingMode.Auto);
            WindowChrome.SetWindowChrome(this,new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0)});
            SourceInitialized+=(s,e)=>
            {
                WindowEdges.Round(this);
                var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if(source!=null)source.AddHook(KeepShape);
            };
            Build();
            ApplyStyle();
            if(lyricsOn){lyricsRow.Height=new GridLength(1,GridUnitType.Star);Lyrics.Visibility=Visibility.Visible;}
            SetSize(DefaultSize(lyricsOn));
            ApplyLimits();
            SizeChanged+=(s,e)=>OnSized();
            // Buttons consume their own presses, so the empty parts of the player drag it. The lyrics are left alone so a line can be clicked.
            MouseLeftButtonDown+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&!IsInside(e.OriginalSource as DependencyObject,Lyrics))DragMove();};
            MouseRightButtonUp+=(s,e)=>{e.Handled=true;ShowNativeMenu(e.GetPosition(this));};
            PreviewMouseMove+=(s,e)=>Wake();
            PreviewMouseDown+=(s,e)=>Wake();
            PreviewKeyDown+=(s,e)=>
            {
                Wake();
                if(e.Key!=Key.Escape)return;
                e.Handled=true;
                Close();
            };
            idleTimer.Tick+=(s,e)=>TickIdle(DateTime.UtcNow);
            Loaded+=async(s,e)=>
            {
                if(testMode)return;
                PlaceAtPreferredPosition();
                Design.Animate((UIElement)Content,OpacityProperty,0,1,170);
                idleTimer.Start();
                await Core.Start();
            };
            StateChanged+=(s,e)=>Core.UpdateProgressFrames();
            Closed+=(s,e)=>{idleTimer.Stop();Core.Stop();};
        }

        // Opening: the card grows out of the corner nearest the app, as if the app had been tucked into it.
        static void Grow(UIElement content)
        {
            var grow=new ScaleTransform(1,1);
            content.RenderTransformOrigin=new Point(.5,.5);content.RenderTransform=grow;
            var swell=new DoubleAnimation(.94,1,TimeSpan.FromMilliseconds(280)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
            swell.Completed+=(s,e)=>{grow.BeginAnimation(ScaleTransform.ScaleXProperty,null);grow.BeginAnimation(ScaleTransform.ScaleYProperty,null);content.RenderTransform=Transform.Identity;};
            grow.BeginAnimation(ScaleTransform.ScaleXProperty,swell);grow.BeginAnimation(ScaleTransform.ScaleYProperty,swell.Clone());
        }
        // ---- Building the window
        void Build()
        {
            var root=new Grid {Background=Background,ClipToBounds=true};
            root.Children.Add(Backdrop);
            face.LayoutTransform=scale;
            root.Children.Add(face);
            Content=root;
            Backdrop.Lively=true;Backdrop.Speed=preferences.MiniBackdropSpeed*2;   // the small player drifts livelier than the big one

            page.RowDefinitions.Add(new RowDefinition {Height=new GridLength(88)});
            page.RowDefinitions.Add(lyricsRow);
            page.RowDefinitions.Add(new RowDefinition {Height=new GridLength(30)});
            page.RowDefinitions.Add(new RowDefinition {Height=new GridLength(58)});
            face.Children.Add(page);

            // The cover, with the title and the artist centered beside it.
            // The same room on the left, top and bottom of the cover, so it sits in the middle of its row, also when everything below has hidden.
            var header=new Grid {Margin=new Thickness(12,12,12,12),Background=Brushes.Transparent};
            header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            header.ColumnDefinitions.Add(new ColumnDefinition());
            art.Children.Add(Design.Icon("Music",26));
            art.Children.Add(Core.artwork);
            art.Clip=new RectangleGeometry(new Rect(0,0,64,64),12,12);
            header.Children.Add(art);
            Core.title.FontSize=16;Core.title.FontWeight=FontWeights.Bold;Core.title.FontFamily=Design.DisplayFont;Core.title.TextAlignment=TextAlignment.Center;
            Core.artist.FontSize=13;Core.artist.FontWeight=FontWeights.Medium;Core.artist.TextAlignment=TextAlignment.Center;
            Core.artist.Foreground=new SolidColorBrush(Color.FromArgb(190,255,255,255));Core.artist.Margin=new Thickness(0,3,0,0);
            labels.Children.Add(Core.title);labels.Children.Add(Core.artist);
            // Equal room on both sides, so the text is centered in the window and clear of the buttons in the corner.
            labels.Margin=new Thickness(8,0,72,0);
            Grid.SetColumn(labels,1);header.Children.Add(labels);
            Grid.SetRow(header,0);page.Children.Add(header);

            Lyrics.Margin=new Thickness(14,2,14,0);
            Lyrics.Seek=seconds=>{shownElapsed=-1;var ignored=Core.Seek(seconds);};
            Grid.SetRow(Lyrics,1);page.Children.Add(Lyrics);

            seek.Margin=new Thickness(8,0,8,0);
            seek.Preview=fraction=>elapsed.Text=Time((int)(fraction*Core.progressLength));
            seek.Commit=async fraction=>{shownElapsed=-1;await Core.Seek(fraction*Core.progressLength);};
            elapsed.Foreground=remaining.Foreground=new SolidColorBrush(Color.FromArgb(180,255,255,255));
            seekRow.Margin=new Thickness(20,0,20,0);
            seekRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});
            seekRow.ColumnDefinitions.Add(new ColumnDefinition());
            seekRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(48)});
            seekRow.Children.Add(elapsed);
            Grid.SetColumn(seek,1);seekRow.Children.Add(seek);
            Grid.SetColumn(remaining,2);seekRow.Children.Add(remaining);
            Grid.SetRow(seekRow,2);page.Children.Add(seekRow);

            // Volume on the left, the transport in the middle, the lyrics and settings on the right.
            Core.ConfigureTransport(40,20,42,24);
            var transport=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            Core.previous.Margin=Core.next.Margin=new Thickness(10,0,10,0);
            transport.Children.Add(Core.previous);transport.Children.Add(Core.play);transport.Children.Add(Core.next);
            controls.Margin=new Thickness(14,0,14,6);
            controls.ColumnDefinitions.Add(new ColumnDefinition());
            controls.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            controls.ColumnDefinitions.Add(new ColumnDefinition());
            Volume.HorizontalAlignment=HorizontalAlignment.Left;
            controls.Children.Add(Volume);
            Grid.SetColumn(transport,1);controls.Children.Add(transport);
            var right=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            lyricsButton=new Button();
            PlayerCore.Frame(lyricsButton,32);
            lyricsFace=new Grid {Opacity=PlayerCore.IdleInk};
            var lyricsIcon=Design.Icon("Lyrics",18);
            lyricsFace.Children.Add(lyricsIcon);
            lyricsButton.Content=lyricsFace;
            PlayerCore.Motion(lyricsButton,1.14,.86);
            lyricsButton.MouseLeave+=(s,e)=>{if(lyricsOn)Design.Animate(lyricsFace,OpacityProperty,lyricsFace.Opacity,1,170);};
            System.Windows.Automation.AutomationProperties.SetName(lyricsButton,"Lyrics");
            lyricsButton.Click+=(s,e)=>ToggleLyrics();
            var more=IconButton("Settings","Settings",17,32);
            more.Click+=(s,e)=>ShowAppSettings();
            right.Children.Add(lyricsButton);right.Children.Add(more);
            Grid.SetColumn(right,2);controls.Children.Add(right);
            Grid.SetRow(controls,3);page.Children.Add(controls);
            ShowLyricsButton();

            // Close. Keeping the player on top is a setting of Lucent (Settings, General), on by default.
            // The same close button as the Lucent window, with the same hover.
            var close=Design.WindowButton((Style)Resources["ChromeAction"],"Close",Close);
            close.MouseEnter+=(s,e)=>PrepareClosePicture();   // the picture for the closing motion is taken when the pointer arrives, so pressing the button has nothing to wait for
            close.ToolTip=null;
            windowButtons.Children.Add(close);
            face.Children.Add(windowButtons);
            ApplyTuck();
        }        static Button IconButton(string symbol,string name,double size,double width)
        {
            var button=new Button {Content=Design.Icon(symbol,size)};
            PlayerCore.Frame(button,width);
            ((UIElement)button.Content).Opacity=PlayerCore.IdleInk;
            PlayerCore.Motion(button,1.14,.86);
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            return button;
        }
        static bool IsInside(DependencyObject element,DependencyObject ancestor)
        {
            while(element!=null)
            {
                if(ReferenceEquals(element,ancestor))return true;
                element=element is Visual||element is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        // ---- Keeping the shape: the player alone keeps its proportions while it is dragged larger or smaller.
        [StructLayout(LayoutKind.Sequential)]struct Box{public int Left,Top,Right,Bottom;}
        IntPtr KeepShape(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message==0x0231)sizingByHand=true;
            else if(message==0x0232){sizingByHand=false;Backdrop.Hold(false);OnSized();}
            if(message!=0x0214||lyricsOn||animating||Tuck>.001)return IntPtr.Zero;
            Backdrop.Hold(true);
            var box=(Box)Marshal.PtrToStructure(lParam,typeof(Box));
            double ratio=BaseWidth/CompactHeight;
            int edge=wParam.ToInt32(),width=box.Right-box.Left,height=box.Bottom-box.Top;
            if(edge==3||edge==6)box.Right=box.Left+(int)Math.Round(height*ratio);
            else
            {
                int fitted=(int)Math.Round(width/ratio);
                if(edge==4||edge==5)box.Top=box.Bottom-fitted;else box.Bottom=box.Top+fitted;
            }
            Marshal.StructureToPtr(box,lParam,false);
            handled=true;
            return new IntPtr(1);
        }
        static double ScaleFor(double width){return Math.Max(MinScale,Math.Min(MaxScale,width/BaseWidth));}
        internal Size DefaultSize(bool lyrics)
        {
            if(lyrics)
            {
                double width=preferences.MiniLyricsWidth>0?preferences.MiniLyricsWidth:BaseWidth;
                return new Size(width,preferences.MiniLyricsHeight>0?preferences.MiniLyricsHeight:Math.Round(width*LyricsHeight/BaseWidth));
            }
            double compact=preferences.MiniCompactWidth>0?preferences.MiniCompactWidth:BaseWidth;
            return new Size(compact,Math.Round(CompactHeight*ScaleFor(compact)));
        }
        void SetSize(Size size){Width=size.Width;Height=size.Height;}
        // The lyrics open (and close) at the width the window has now, and only the height changes, so a window that was made big just grows downward instead of
        // turning into a different, narrower window (that change of width and height together was what lagged so much at big sizes).
        Size LyricsSizeFor(double width)
        {
            double ratio=preferences.MiniLyricsWidth>0&&preferences.MiniLyricsHeight>0?preferences.MiniLyricsHeight/preferences.MiniLyricsWidth:LyricsHeight/BaseWidth;
            double scale=ScaleFor(width),height=Math.Round(width*ratio);
            var area=SystemParameters.WorkArea;
            height=Math.Max(Math.Round(420*scale),Math.Min(height,area.Height-60));
            return new Size(Math.Round(width),height);
        }
        // The limits of the view that is showing. While the window is moving between views they are opened wide.
        void ApplyLimits()
        {
            MinWidth=BaseWidth*MinScale;MaxWidth=BaseWidth*MaxScale;
            if(lyricsOn){MinHeight=420*MinScale-TuckRows;MaxHeight=2400;}
            else{MinHeight=(CompactHeight-TuckRows)*MinScale;MaxHeight=CompactHeight*MaxScale;}
        }
        void OnSized()
        {
            double factor=ScaleFor(ActualWidth);
            scale.ScaleX=scale.ScaleY=factor;
            if(animating)return;
            if(tucking)return;
            double wanted=lyricsOn?Math.Max(ActualHeight,(420-TuckRows*Tuck)*factor):(CompactHeight-TuckRows*Tuck)*factor;
            // While it is dragged by hand, Windows is told the shape (above); it is settled when the drag ends.
            if(!sizingByHand&&Math.Abs(ActualHeight-wanted)>1){animating=true;Height=Math.Round(wanted);animating=false;}
            if(!IsLoaded||Tuck>.001||sizingByHand)return;
            // Each view remembers the size it was given. A size that is already the default is not a choice, so it is not stored.
            var normal=DefaultSize(lyricsOn);
            if(Math.Abs(ActualWidth-normal.Width)<1.5&&Math.Abs(ActualHeight-normal.Height)<1.5)return;
            if(lyricsOn){preferences.MiniLyricsWidth=Math.Round(ActualWidth);preferences.MiniLyricsHeight=Math.Round(ActualHeight);}
            else preferences.MiniCompactWidth=Math.Round(ActualWidth);
            Changed();
        }
        // Moves to a new size, keeping the top left corner where it is unless the bottom would leave the screen.
        int resizeMotion;
        void ResizeTo(Size target,bool animate,Action after=null)
        {
            resizeMotion++;
            var area=SystemParameters.WorkArea;
            double top=testMode?Top:Math.Max(area.Top,Math.Min(Top,area.Bottom-target.Height));
            animating=true;
            MinWidth=100;MaxWidth=4000;MinHeight=60;MaxHeight=4000;
            Action finish=()=>
            {
                BeginAnimation(WidthProperty,null);BeginAnimation(HeightProperty,null);BeginAnimation(TopProperty,null);
                Lyrics.CacheMode=null;Backdrop.Hold(false);
                if(lyricsOn)lyricsRow.Height=new GridLength(1,GridUnitType.Star);
                Width=target.Width;Height=target.Height;if(!testMode)Top=top;
                ApplyLimits();
                animating=false;
                OnSized();
                Lyrics.Thaw();
                // Lyrics that arrived while the window was moving are put in now, so laying them out did not hold up a step of the glide.
                var arrived=lyricsArrived;lyricsArrived=null;if(arrived!=null)arrived();
                if(after!=null)after();
            };
            if(!animate||!IsVisible||testMode){finish();return;}
            // The lyrics keep their layout and are drawn once as a picture while the window changes size, so only the window moves.
            // (No picture of the lyrics is made: with their row already at its final size they are not laid out or redrawn at each step, and a cached picture of them was drawn again at every step.)
            Lyrics.Frozen=true;Backdrop.Hold(true);
            var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
            var grow=new DoubleAnimation(ActualHeight,target.Height,TimeSpan.FromMilliseconds(220)){EasingFunction=ease};
            grow.Completed+=(s,e)=>finish();
            // The width changes at once and only the height glides, like a drawer: resizing in both directions at every step lagged, most of all when the window
            // had been made bigger first and had to change its width to open the lyrics.
            BeginAnimation(WidthProperty,null);Width=target.Width;
            // The glide starts at once, on the click: nothing heavy happens first (the lyrics themselves only come in when the window has arrived).
            BeginAnimation(TopProperty,new DoubleAnimation(Top,top,TimeSpan.FromMilliseconds(220)){EasingFunction=ease});
            BeginAnimation(HeightProperty,grow);
        }

        // ---- Closing: the window closes at once, and a picture of it takes its place, shrinks a little toward its middle (to about 90%) and drops a few pixels while it
        // fades away, quickly. (The picture lives in a window of its own, so nothing of the real window has to be redrawn or resized at every step, which is what keeps it smooth.)
        bool closeMoved,closeStarted;
        // Closing: a picture of the window is put on screen first (in a window of its own), and only when that has been drawn does the real window close, so there is
        // never a gap or a flash between them; then the picture shrinks a little and fades.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if(e.Cancel||closeMoved||testMode||!IsVisible||WindowState!=WindowState.Normal||Dispatcher.HasShutdownStarted)return;
            e.Cancel=true;
            if(closeStarted)return;
            closeStarted=true;
            Action closeForReal=()=>{if(closeMoved)return;closeMoved=true;Close();};
            try{HopAway(closeForReal);}catch(Exception){closeForReal();}
        }
        RenderTargetBitmap closePicture;DateTime closePictureAt;
        RenderTargetBitmap TakePicture()
        {
            double w=ActualWidth,h=ActualHeight;
            var dpi=VisualTreeHelper.GetDpi(this);
            var picture=new RenderTargetBitmap((int)Math.Ceiling(w*dpi.DpiScaleX),(int)Math.Ceiling(h*dpi.DpiScaleY),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
            picture.Render((Visual)Content);picture.Freeze();
            return picture;
        }
        void PrepareClosePicture()
        {
            if(testMode||!IsVisible||ActualWidth<20||ActualHeight<20||closePicture!=null&&DateTime.UtcNow-closePictureAt<TimeSpan.FromSeconds(2))return;
            Dispatcher.BeginInvoke(new Action(()=>{try{closePicture=TakePicture();closePictureAt=DateTime.UtcNow;}catch(Exception){closePicture=null;}}),DispatcherPriority.Background);
        }
        void HopAway(Action closeReal)
        {
            double w=ActualWidth,h=ActualHeight;
            if(w<20||h<20){closeReal();return;}
            var picture=closePicture!=null&&DateTime.UtcNow-closePictureAt<TimeSpan.FromSeconds(3)?closePicture:TakePicture();
            closePicture=null;
            var shrink=new ScaleTransform(1,1);
            var image=new Image {Source=picture,Stretch=Stretch.Fill,IsHitTestVisible=false,Clip=new RectangleGeometry(new Rect(0,0,w,h),9,9),RenderTransformOrigin=new Point(.5,.5),RenderTransform=shrink};
            var ghost=new Window
            {
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=Topmost,Focusable=false,
                WindowStartupLocation=WindowStartupLocation.Manual,Left=Left,Top=Top,Width=w,Height=h,IsHitTestVisible=false,Content=image
            };
            bool started=false;
            Action start=()=>
            {
                if(started)return;started=true;
                closeReal();
                var quick=TimeSpan.FromMilliseconds(220);
                var scale=new DoubleAnimation(1,.95,quick){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
                shrink.BeginAnimation(ScaleTransform.ScaleXProperty,scale);
                shrink.BeginAnimation(ScaleTransform.ScaleYProperty,scale.Clone());
                // The fade runs the whole time, softly at both ends (an ease-in left it almost whole and then took it away in the last few frames).
                var fade=new DoubleAnimation(1,0,quick){EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut}};
                fade.Completed+=(s,a)=>ghost.Close();
                ghost.BeginAnimation(OpacityProperty,fade);
            };
            ghost.ContentRendered+=(s,a)=>start();
            // If it were never drawn, the window still closes.
            var guard=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
            guard.Tick+=(s,a)=>{guard.Stop();start();};
            guard.Start();
            ghost.Show();
        }        // ---- Lyrics
        void ShowLyricsButton()
        {
            // On, the icon is fully lit and glows; off, it is dim.
            lyricsFace.Opacity=lyricsOn?1:PlayerCore.IdleInk;
            lyricsFace.Effect=lyricsOn?new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.White,BlurRadius=12,ShadowDepth=0,Opacity=.8}:null;
        }
        internal void ToggleLyrics()
        {
            lyricsOn=!lyricsOn;preferences.ShowLyrics=lyricsOn;Changed();
            ShowLyricsButton();
            // The lyrics of the song are kept while they are closed, so opening them again for the same song builds nothing. Only a search still on its way is dropped.
            if(lyricsOn){ShowLyricsView(true);LoadLyrics();Lyrics.Update(Core.progressValue);}
            else{if(lyricsPending){lyricsToken++;lyricsSong=null;lyricsPending=false;lyricsArrived=null;}HideLyricsView(true);}
        }
        // The lyrics take the room between the title and the progress bar; the window grows to the size this view last had.
        // The lyrics open and close without resizing the window a step at a time (every step re-laid-out and redrew the whole window, which is what stuttered):
        // the window takes its new size at once, and only the lyrics move: they fade in and rise a little, or fade out and sink a little before the window
        // takes its small size.
        readonly TranslateTransform lyricsRise=new TranslateTransform();
        int lyricsMotion;
        void ShowLyricsView(bool animate)
        {
            int motion=++lyricsMotion;
            // The window grows downward from its small size (the lyrics expand out of it, as a drawer, instead of a new window opening), and the lyrics fade in as it grows.
            bool glide=animate&&IsVisible&&!testMode;
            var target=LyricsSizeFor(ActualWidth>0?ActualWidth:Width);
            // While the window grows the lyrics already have the size they will end with (their row is as tall as it will be and the controls wait below it), so nothing
            // inside is laid out or redrawn at every step: the window only uncovers it. A row that followed the window's height had the whole list drawn again at every step.
            lyricsRow.Height=glide?new GridLength(Math.Max(100,target.Height/ScaleFor(target.Width)-CompactHeight)):new GridLength(1,GridUnitType.Star);
            Lyrics.RenderTransform=lyricsRise;
            if(glide)
            {
                // The window starts to open on the click itself, empty: laying out and drawing the lyrics is the slow part (it used to hold the click up for a moment),
                // so they are only brought in once the window has arrived, and fade in there. The glide itself then draws no text at all.
                Lyrics.Visibility=Visibility.Hidden;
                ResizeTo(target,true,()=>
                {
                    if(motion!=lyricsMotion||!lyricsOn)return;
                    Lyrics.Opacity=0;Lyrics.Visibility=Visibility.Visible;
                    Lyrics.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(170)){BeginTime=TimeSpan.FromMilliseconds(50),EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
                    lyricsRise.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(10,0,TimeSpan.FromMilliseconds(200)){BeginTime=TimeSpan.FromMilliseconds(50),EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
                });
            }
            else
            {
                Lyrics.Visibility=Visibility.Visible;
                ResizeTo(target,false);
                Lyrics.BeginAnimation(OpacityProperty,null);Lyrics.Opacity=1;lyricsRise.BeginAnimation(TranslateTransform.YProperty,null);lyricsRise.Y=0;
            }
        }
        void HideLyricsView(bool animate)
        {
            int motion=++lyricsMotion;
            Action close=()=>
            {
                if(motion!=lyricsMotion||lyricsOn)return;
                lyricsRow.Height=new GridLength(0);
                Lyrics.Visibility=Visibility.Hidden;
                // With the lyrics already faded away, the window folds back up to its small size.
                double keep=ActualWidth>0?ActualWidth:Width;
                ResizeTo(new Size(Math.Round(keep),Math.Round(CompactHeight*ScaleFor(keep))),animate&&IsVisible&&!testMode);
                Lyrics.BeginAnimation(OpacityProperty,null);Lyrics.Opacity=1;lyricsRise.BeginAnimation(TranslateTransform.YProperty,null);lyricsRise.Y=0;
            };
            // The lyrics go at once and the window starts to fold on the click itself (fading them out first made the click feel late).
            close();
        }        // A see-through window of its own that shows a picture of this window as it is now, in the same place.
        Window MakeGhost()
        {
            double w=ActualWidth,h=ActualHeight;
            var dpi=VisualTreeHelper.GetDpi(this);
            var picture=new RenderTargetBitmap((int)Math.Ceiling(w*dpi.DpiScaleX),(int)Math.Ceiling(h*dpi.DpiScaleY),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
            picture.Render((Visual)Content);picture.Freeze();
            return new Window
            {
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=Topmost,Focusable=false,
                WindowStartupLocation=WindowStartupLocation.Manual,Left=Left,Top=Top,Width=w,Height=h,IsHitTestVisible=false,
                Content=new Border {CornerRadius=new CornerRadius(9),ClipToBounds=true,IsHitTestVisible=false,Child=new Image {Source=picture,Width=w,Height=h,Stretch=Stretch.None,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false}}
            };
        }        // Looks the playing song up once per song, and only while lyrics are switched on. Tests never go online.
        void LoadLyrics()
        {
            if(!lyricsOn)return;
            var track=Core.Track;
            if(track==null||string.IsNullOrEmpty(track.Title))
            {
                if(lyricsSong==null){Lyrics.ShowMessage("Nothing playing");return;}
                lyricsSong=null;lyricsToken++;
                Lyrics.ShowMessage("Nothing playing");
                return;
            }
            string song=track.Source+"|"+track.Title+"|"+track.Artist;
            if(song==lyricsSong)return;
            lyricsSong=song;
            int token=++lyricsToken;lyricsPending=true;lyricsArrived=null;
            Lyrics.ShowMessage("Looking for lyrics…");
            if(testMode)return;
            string title=track.Title,artist=track.Artist;double length=track.Duration;
            Task.Run(()=>LyricsService.Find(title,artist,length)).ContinueWith(task=>
            {
                var result=task.Status==TaskStatus.RanToCompletion?task.Result:null;
                Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(token!=lyricsToken||!lyricsOn)return;
                    lyricsPending=false;
                    Action put=()=>{if(token!=lyricsToken)return;Lyrics.SetLyrics(result);Lyrics.Update(Core.progressValue);};
                    if(animating)lyricsArrived=put;else put();
                }));
            });
        }
        void ApplyStyle(){Lyrics.SetStyle(preferences.MiniLyricsSize,preferences.MiniCenterLyrics);}

        // ---- Settings are in Lucent (Settings, Mini Player); these two are what they call.
        internal void ResetSizes()
        {
            preferences.MiniCompactWidth=preferences.MiniLyricsWidth=preferences.MiniLyricsHeight=0;
            Changed();
            ResizeTo(DefaultSize(lyricsOn),true);
        }
        // The settings changed while the player is open.
        internal void RefreshFromPreferences()
        {
            Backdrop.Speed=preferences.MiniBackdropSpeed*2;
            Backdrop.SetActive(Core.Track!=null&&!preferences.PerformanceMode);
            ApplyStyle();
            Wake();
        }
        // ---- Hiding the controls while idle
        internal void Wake(){lastActive=DateTime.UtcNow;if(faded)ApplyChromeFade(false);}
        bool HideWanted{get{return lyricsOn?preferences.MiniHideLyrics:preferences.MiniHideCompact;}}
        // The timer calls this with the time now; tests call it with a later time.
        internal void TickIdle(DateTime now)
        {
            bool idle=HideWanted&&!seek.Dragging&&now-lastActive>TimeSpan.FromSeconds(3);
            if(idle!=faded)ApplyChromeFade(idle);
        }
        void ApplyChromeFade(bool hide)
        {
            faded=hide;
            if(hide&&Tuck<.001)tuckBaseHeight=ActualHeight;
            if(testMode||!IsVisible){BeginAnimation(TuckProperty,null);Tuck=hide?1:0;return;}
            // Growing back is gentler than leaving, and the drifting colors rest while the window changes size, so every frame goes to the resize.
            var motion=new DoubleAnimation(Tuck,hide?1:0,TimeSpan.FromMilliseconds(hide?420:380)){EasingFunction=new QuinticEase {EasingMode=hide?EasingMode.EaseInOut:EasingMode.EaseOut},FillBehavior=FillBehavior.HoldEnd};
            Backdrop.Hold(true);
            motion.Completed+=(s,e)=>Backdrop.Hold(false);
            BeginAnimation(TuckProperty,motion);
        }        internal void SetOnTop(bool on){Topmost=on;}

        internal void SetTrack(MediaTrack track){Core.SetTrack(track);}
        internal void PlaceAtPreferredPosition()
        {
            if(StartAt.HasValue){Left=StartAt.Value.X;Top=StartAt.Value.Y;return;}
            var area=SystemParameters.WorkArea;
            string position=preferences.PlayerPosition;
            Left=position=="Left"?area.Left+20:position=="Right"?area.Right-Width-20:area.Left+(area.Width-Width)/2;
            Top=area.Top+20;
        }
        void ShowProgress()
        {
            if(lyricsOn)
            {
                var playing=Core.Track;
                if(playing==null?lyricsSong!=null:(playing.Source+"|"+playing.Title+"|"+playing.Artist)!=lyricsSong)LoadLyrics();
                Lyrics.Update(Core.progressValue);
            }
            // The colors drift only while something is playing.
            Backdrop.SetActive(Core.Track!=null&&!preferences.PerformanceMode);
            seek.Seekable=Core.canSeek;
            seek.SetFraction(Core.progressValue/Core.progressLength);
            if(seek.Dragging)return;
            // This runs every frame while playing, so only redraw text when a second actually changes.
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
            Design.Animate(labels,OpacityProperty,.15,1,200);
            Design.Animate((TranslateTransform)labels.RenderTransform,TranslateTransform.YProperty,5,0,220);
        }
        void ArtworkChanged(BitmapSource image)
        {
            Backdrop.SetCover(image);
            if(image==null)return;
            Design.Animate(art,OpacityProperty,.3,1,240);
            PlayerCore.Pop(art,.94,260);
        }
        void ShowNativeMenu(Point location)
        {
            var source=PresentationSource.FromVisual(this);
            var point=PointToScreen(location);
            if(source!=null)point=source.CompositionTarget.TransformFromDevice.Transform(point);
            SystemCommands.ShowSystemMenu(this,point);
        }
    }
}
