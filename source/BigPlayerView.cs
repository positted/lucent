using System;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;

namespace Lucent
{
    // The big player: a full-window view with the cover, the track, a seekable progress bar, the controls and the volume,
    // over a background of slowly drifting cover colors. In the app it covers the sidebar too; the same view fills the pop-out window.
    internal sealed class BigPlayerView:Grid
    {
        internal readonly PlayerCore Core;
        // The same lively drift as the mini player (which is just right), a little further and faster since the window is bigger.
        internal readonly FlowingBackdrop Backdrop=FlowingBackdrop.ForPlayers();
        readonly ArtistBadge artistPicture=new ArtistBadge(22);
        internal ArtistBadge ArtistPicture {get{return artistPicture;}}
        internal void RefreshArtistPicture(){var track=Core.Track;artistPicture.Show(track==null?null:track.Artist,!offline&&ArtistPictures.Enabled());}
        internal readonly VolumeControl Volume;
        readonly Border coverFrame=new Border {CornerRadius=new CornerRadius(16),Background=Brushes.Black,Effect=new DropShadowEffect {BlurRadius=40,ShadowDepth=12,Opacity=.5,Color=Colors.Black}};
        readonly Grid cover=new Grid {Background=new SolidColorBrush(Color.FromRgb(38,38,41))};
        readonly StackPanel column=new StackPanel {HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
        readonly LyricsView lyricsView=new LyricsView {HorizontalAlignment=HorizontalAlignment.Right,Visibility=Visibility.Collapsed,Opacity=0};
        // Whether lyrics are wanted, and whether to look them up (never in tests). Set by the app so the choice is remembered.
        internal static Func<bool> LyricsEnabled=()=>false;
        internal static Action<bool> LyricsChanged=on=>{};
        // Performance mode: the drifting colors stay still.
        internal static Func<bool> PerformanceMode=()=>false;
        internal void RefreshPerformance(){Backdrop.SetActive(!PerformanceMode());}
        readonly bool offline;
        bool lyricsOn,lyricsVisible;
        Button lyricsButton;
        Grid lyricsFace;
        string lyricsSong;
        int lyricsToken;
        internal LyricsView Lyrics {get{return lyricsView;}}
        internal bool LyricsOn {get{return lyricsOn;}}
        internal bool LyricsShown {get{return lyricsVisible;}}
        readonly StackPanel labels=new StackPanel {Margin=new Thickness(0,18,0,0),RenderTransform=new TranslateTransform()};
        readonly SeekBar seek=new SeekBar {Margin=new Thickness(0,14,0,2)};
        readonly TextBlock elapsed=new TextBlock {FontSize=12,FontWeight=FontWeights.Medium,Text="0:00"};
        readonly TextBlock remaining=new TextBlock {FontSize=12,FontWeight=FontWeights.Medium,Text="0:00"};
        readonly TranslateTransform slide=new TranslateTransform();
        readonly ScaleTransform grow=new ScaleTransform(1,1);
        int shownElapsed=-1,shownLength=-1;
        bool open;

        // leading and trailing are the buttons in the top corners. inApp is true for the view that covers the main window,
        // where the top edge also drags the window.
        public BigPlayerView(Window owner,bool testMode,FrameworkElement leading,FrameworkElement trailing,bool inApp)
        {
            offline=testMode;
            Design.SetStill(this,true);
            Visibility=Visibility.Collapsed;
            Opacity=0;
            // It grows out of the lower left, where the cover of the player bar is, as it fades in.
            RenderTransform=new TransformGroup {Children=new TransformCollection {grow,slide}};
            RenderTransformOrigin=new Point(.07,.96);
            // Without a background, clicks on its empty parts would fall through to the page underneath.
            Background=new SolidColorBrush(Color.FromRgb(13,13,16));
            Core=new PlayerCore(this,testMode) {artworkPixels=800,showProgress=ShowProgress,showNote=note=>Core.title.ToolTip=note,trackChanged=TrackChanged,artworkChanged=ArtworkChanged};
            Volume=new VolumeControl(testMode,18,150);
            Volume.Source=()=>Core.Source;
            owner.StateChanged+=(s,e)=>Core.UpdateProgressFrames();
            seek.Preview=fraction=>elapsed.Text=Time((int)(fraction*Core.progressLength));
            seek.Commit=async fraction=>{shownElapsed=-1;await Core.Seek(fraction*Core.progressLength);};
            Children.Add(Backdrop);
            if(inApp)
            {
                var strip=new Border {Height=52,VerticalAlignment=VerticalAlignment.Top,Background=Brushes.Transparent};
                strip.MouseLeftButtonDown+=(s,e)=>
                {
                    if(e.ClickCount==2)return;
                    else owner.DragMove();
                };
                Children.Add(strip);
            }

            cover.Children.Add(Design.Icon("Music",96));
            cover.Children.Add(Core.artwork);
            coverFrame.Child=cover;
            column.Children.Add(coverFrame);
            Core.title.FontSize=21;
            Core.title.FontWeight=FontWeights.Bold;
            Core.title.FontFamily=Design.DisplayFont;
            Core.artist.FontSize=13;
            Core.artist.FontWeight=FontWeights.Medium;
            Core.artist.Foreground=new SolidColorBrush(Color.FromArgb(190,255,255,255));
            // The artist's picture sits before the name when pictures are switched on and one is found.
            var artistRow=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,0)};
            Core.artist.VerticalAlignment=VerticalAlignment.Center;
            artistRow.Children.Add(artistPicture);artistRow.Children.Add(Core.artist);
            labels.Children.Add(Core.title);
            labels.Children.Add(artistRow);
            column.Children.Add(labels);
            column.Children.Add(seek);
            var times=new DockPanel();
            remaining.Foreground=elapsed.Foreground=new SolidColorBrush(Color.FromArgb(170,255,255,255));
            DockPanel.SetDock(remaining,Dock.Right);
            times.Children.Add(remaining);
            times.Children.Add(elapsed);
            column.Children.Add(times);

            Core.ConfigureTransport(38,20,52,30);
            Core.previous.Height=Core.next.Height=Core.shuffle.Height=Core.repeat.Height=38;
            Core.previous.Margin=Core.next.Margin=new Thickness(12,0,12,0);
            Core.shuffle.Margin=Core.repeat.Margin=new Thickness(4,0,4,0);
            var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,14,0,0)};
            controls.Children.Add(Core.shuffle);
            controls.Children.Add(Core.previous);
            controls.Children.Add(Core.play);
            controls.Children.Add(Core.next);
            controls.Children.Add(Core.repeat);
            // The five transport buttons are wider than a small cover. In a row that is too wide for its column, layout clips everything to the
            // column, which cut off the outer buttons' highlights and the play disc. The row shrinks to fit instead.
            column.Children.Add(new Viewbox {Child=controls,StretchDirection=StretchDirection.DownOnly,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center});
            // The volume, and a switch for the lyrics beside it.
            lyricsButton=new Button();
            PlayerCore.Frame(lyricsButton,30);
            lyricsButton.Margin=new Thickness(12,0,0,0);
            lyricsFace=new Grid {Opacity=PlayerCore.IdleInk};
            var lyricsIcon=Design.Icon("Lyrics",18);
            lyricsFace.Children.Add(lyricsIcon);
            lyricsButton.Content=lyricsFace;
            PlayerCore.Motion(lyricsButton,1.14,.86);
            lyricsButton.MouseLeave+=(s,e)=>{if(lyricsOn)Design.Animate(lyricsFace,OpacityProperty,lyricsFace.Opacity,1,170);};
            System.Windows.Automation.AutomationProperties.SetName(lyricsButton,"Lyrics");
            lyricsButton.Click+=(s,e)=>ToggleLyrics();
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,14,0,0)};
            Volume.Margin=new Thickness(0);
            bottom.Children.Add(Volume);
            bottom.Children.Add(lyricsButton);
            column.Children.Add(bottom);
            Children.Add(column);
            lyricsView.Seek=seconds=>{shownElapsed=-1;var ignored=Core.Seek(seconds);};
            Children.Add(lyricsView);
            lyricsOn=LyricsEnabled();
            ShowLyricsButton();

            if(leading!=null)
            {
                leading.HorizontalAlignment=HorizontalAlignment.Left;
                leading.VerticalAlignment=VerticalAlignment.Top;
                leading.Margin=new Thickness(14,12,0,0);
                Children.Add(leading);
            }
            if(trailing!=null)
            {
                trailing.HorizontalAlignment=HorizontalAlignment.Right;
                trailing.VerticalAlignment=VerticalAlignment.Top;
                Children.Add(trailing);
            }
            SizeChanged+=(s,e)=>ApplyLayout(false);
        }
        void ShowLyricsButton()
        {
            // On, the icon is fully lit and glows; off, it is dim.
            lyricsFace.Opacity=lyricsOn?1:PlayerCore.IdleInk;
            lyricsFace.Effect=lyricsOn?new DropShadowEffect {Color=Colors.White,BlurRadius=12,ShadowDepth=0,Opacity=.8}:null;
        }
        internal void ToggleLyrics()
        {
            lyricsOn=!lyricsOn;
            LyricsChanged(lyricsOn);
            ShowLyricsButton();
            lyricsSong=null;
            if(lyricsOn)LoadLyrics();
            else{lyricsToken++;lyricsView.Clear();}
            ApplyLayout(true);
        }
        // With lyrics on, the player takes the left part of the window and the lyrics the rest. A narrow window keeps just the player.
        System.Windows.Threading.DispatcherTimer layoutSettle;
        void ApplyLayout(bool animate)
        {
            if(ActualWidth<=0||ActualHeight<=0)return;
            bool show=lyricsOn&&ActualWidth>=900;
            double area=show?ActualWidth*.44:ActualWidth;
            FitCover(new Size(area,ActualHeight));
            var margin=new Thickness(Math.Max(0,(area-column.Width)/2),0,0,0);
            lyricsView.Width=Math.Max(0,ActualWidth-area-40);
            lyricsView.Margin=new Thickness(0,64,40,36);
            if(animate&&IsVisible)
            {
                // Moved as pictures while it slides (the cover's soft shadow and the lyrics are costly to draw again at every step).
                column.CacheMode=new BitmapCache();lyricsView.CacheMode=new BitmapCache();
                if(layoutSettle==null){layoutSettle=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(420)};layoutSettle.Tick+=(s,e)=>{layoutSettle.Stop();column.CacheMode=null;lyricsView.CacheMode=null;};}
                layoutSettle.Stop();layoutSettle.Start();
            }
            if(animate&&IsVisible)column.BeginAnimation(MarginProperty,new ThicknessAnimation(margin,TimeSpan.FromMilliseconds(320)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            else{column.BeginAnimation(MarginProperty,null);column.Margin=margin;}
            if(show==lyricsVisible)return;
            lyricsVisible=show;
            if(show)
            {
                lyricsView.Visibility=Visibility.Visible;
                if(animate&&IsVisible)Design.Animate(lyricsView,OpacityProperty,lyricsView.Opacity,1,320);else{lyricsView.BeginAnimation(OpacityProperty,null);lyricsView.Opacity=1;}
                return;
            }
            var fade=new DoubleAnimation(lyricsView.Opacity,0,TimeSpan.FromMilliseconds(animate&&IsVisible?160:1));
            fade.Completed+=(s,e)=>{if(!lyricsVisible)lyricsView.Visibility=Visibility.Collapsed;};
            lyricsView.BeginAnimation(OpacityProperty,fade);
        }
        // Looks the playing song up once per song, and only while lyrics are switched on.
        void LoadLyrics()
        {
            if(!lyricsOn)return;
            var track=Core.Track;
            if(track==null||string.IsNullOrEmpty(track.Title))
            {
                if(lyricsSong==null)return;
                lyricsSong=null;lyricsToken++;
                lyricsView.ShowMessage("Nothing playing");
                return;
            }
            string song=track.Source+"|"+track.Title+"|"+track.Artist;
            if(song==lyricsSong)return;
            lyricsSong=song;
            int token=++lyricsToken;
            lyricsView.ShowMessage("Looking for lyrics\u2026");
            if(offline)return;
            string title=track.Title,artist=track.Artist;double length=track.Duration;
            Task.Run(()=>LyricsService.Find(title,artist,length)).ContinueWith(task=>
            {
                var result=task.Status==TaskStatus.RanToCompletion?task.Result:null;
                Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(token!=lyricsToken||!lyricsOn)return;
                    lyricsView.SetLyrics(result);
                    lyricsView.Update(Core.progressValue);
                }));
            });
        }
        // The cover takes the room the rest of the column leaves, up to a size that still looks sharp.
        void FitCover(Size room)
        {
            double side=Math.Max(220,Math.Min(370,Math.Min(room.Width-96,room.Height-300)));
            column.Width=coverFrame.Width=coverFrame.Height=side;
            cover.Clip=new RectangleGeometry(new Rect(0,0,side,side),16,16);
        }
        System.Windows.Threading.DispatcherTimer transitionSettle;
        // Lays the player out once without showing it (hidden things are laid out but not drawn), so its first real opening has nothing left to do for the first time.
        internal void Warm()
        {
            if(open||Visibility!=Visibility.Collapsed)return;
            Visibility=Visibility.Hidden;
            try{RefreshPerformance();ApplyLayout(false);UpdateLayout();}
            finally{if(!open)Visibility=Visibility.Collapsed;}
        }
        // Where the cover of the player bar is, in this view's own coordinates (null when there is no bar to go to). The player grows out of that cover and,
        // closing, shrinks down into it.
        internal Func<Rect?> CoverTarget;
        Rect? AimAtCover()
        {
            Rect? target=CoverTarget==null?(Rect?)null:CoverTarget();
            if(target==null||ActualWidth<=0||ActualHeight<=0){RenderTransformOrigin=new Point(.07,.96);return null;}
            var cover=target.Value;
            RenderTransformOrigin=new Point((cover.X+cover.Width/2)/ActualWidth,(cover.Y+cover.Height/2)/ActualHeight);
            return target;
        }
        internal async void SetOpen(bool show)
        {
            if(show==open)return;
            open=show;
            // While it opens or closes it is moved as one picture, and its drifting colors rest: drawing everything again at every step (the cover's soft shadow,
            // the lyrics, the colors) made it lag.
            if(!show){CacheMode=new BitmapCache();Backdrop.Hold(true);}else{CacheMode=null;Backdrop.Hold(false);}
            if(show)
            {
                Visibility=Visibility.Visible;
                RefreshPerformance();
                ApplyLayout(false);
                AimAtCover();
                Design.Animate(this,OpacityProperty,Opacity,1,150);
                slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(22,0,TimeSpan.FromMilliseconds(320)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
                var swell=new DoubleAnimation(.16,1,TimeSpan.FromMilliseconds(520)){EasingFunction=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.16}};
                grow.BeginAnimation(ScaleTransform.ScaleXProperty,swell);grow.BeginAnimation(ScaleTransform.ScaleYProperty,swell);
                if(transitionSettle==null){transitionSettle=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(600)};transitionSettle.Tick+=(s,e)=>{transitionSettle.Stop();if(open){CacheMode=null;Backdrop.Hold(false);}};}
                transitionSettle.Stop();transitionSettle.Start();
                await Core.Start();
                return;
            }
            Core.Stop();
            var aim=AimAtCover();
            if(aim!=null)
            {
                // The whole player shrinks down into the cover on the player bar: it ends as small as that cover, right on it, and only fades out as it gets there.
                double small=Math.Max(.03,Math.Min(.2,aim.Value.Width/ActualWidth));
                var fadeInto=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(150)){BeginTime=TimeSpan.FromMilliseconds(210)};
                fadeInto.Completed+=(s,e)=>{if(!open){Visibility=Visibility.Collapsed;CacheMode=null;}};
                BeginAnimation(OpacityProperty,fadeInto);
                slide.BeginAnimation(TranslateTransform.YProperty,null);slide.Y=0;
                var shrinkInto=new DoubleAnimation(grow.ScaleX,small,TimeSpan.FromMilliseconds(360)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}};
                grow.BeginAnimation(ScaleTransform.ScaleXProperty,shrinkInto);grow.BeginAnimation(ScaleTransform.ScaleYProperty,shrinkInto);
                return;
            }
            var fade=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(260)){BeginTime=TimeSpan.FromMilliseconds(60)};
            fade.Completed+=(s,e)=>{if(!open){Visibility=Visibility.Collapsed;CacheMode=null;}};
            BeginAnimation(OpacityProperty,fade);
            slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,14,TimeSpan.FromMilliseconds(170)));
            var shrink=new DoubleAnimation(grow.ScaleX,.16,TimeSpan.FromMilliseconds(320)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};
            grow.BeginAnimation(ScaleTransform.ScaleXProperty,shrink);grow.BeginAnimation(ScaleTransform.ScaleYProperty,shrink);
        }
        void ShowProgress()
        {
            if(lyricsOn)
            {
                var playing=Core.Track;
                if(playing==null?lyricsSong!=null:(playing.Source+"|"+playing.Title+"|"+playing.Artist)!=lyricsSong)LoadLyrics();
                if(lyricsVisible)lyricsView.Update(Core.progressValue);
            }
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
            RefreshArtistPicture();
            Design.Animate(labels,OpacityProperty,.15,1,200);
            Design.Animate((TranslateTransform)labels.RenderTransform,TranslateTransform.YProperty,5,0,220);
        }
        void ArtworkChanged(BitmapSource image)
        {
            Backdrop.SetCover(image);
            if(image==null)return;
            Design.Animate(cover,OpacityProperty,.3,1,240);
            PlayerCore.Pop(cover,.97,260);
        }
    }
}
