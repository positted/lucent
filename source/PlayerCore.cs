using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lucent
{
    // Predict between provider samples without snapping backwards on quantized
    // timestamps. Large jumps and track changes reset immediately.
    internal sealed class MediaProgressClock
    {
        bool initialized,playing;
        string track;
        double position,origin,correction;
        public void Reset(){initialized=false;}
        public double Position(double now)
        {
            double elapsed=Math.Max(0,now-origin);
            return Math.Max(0,position+(playing?elapsed+correction*Math.Min(elapsed/1.5,1):0));
        }
        public void Synchronize(string key,double reported,bool running,double now)
        {
            double projected=Position(now),error=reported-projected;
            bool snap=!initialized||key!=track||running!=playing||!running||Math.Abs(error)>1.5;
            position=snap?reported:projected;
            correction=snap?0:error;
            origin=now;
            track=key;
            playing=running;
            initialized=true;
        }
    }
    // Everything the players share: reading media, the transport buttons and their motion,
    // seeking, shuffle and repeat, and progress. Each host only supplies a layout.
    internal sealed class PlayerCore
    {
        // The click shows at once; media apps can take a moment to publish the new state.
        internal const double IdleInk=.66;
        const double OptimisticHold=1.5;
        readonly MediaSession session=new MediaSession();
        readonly DispatcherTimer refresh=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
        readonly MediaProgressClock progressClock=new MediaProgressClock();
        readonly FrameworkElement host;
        readonly bool testMode;
        internal readonly TextBlock title=new TextBlock {Text="Nothing playing",TextTrimming=TextTrimming.CharacterEllipsis};
        internal readonly TextBlock artist=new TextBlock {Text="Open a music app",TextTrimming=TextTrimming.CharacterEllipsis};
        internal readonly Image artwork=new Image {Stretch=Stretch.UniformToFill};
        internal readonly Button previous=new Button(),play=new Button(),next=new Button(),shuffle=new Button(),repeat=new Button();
        internal bool? displayedPlaying;
        internal bool canSeek,canShuffle,canRepeat,shuffling;
        internal int repeating;
        internal double progressValue,progressLength=1;
        internal int artworkPixels=96;
        internal Action showProgress=()=>{},trackChanged=()=>{};
        internal Action<string> showNote=note=>{};
        internal Action<BitmapSource> artworkChanged=image=>{};
        MediaTrack current;
        // The id of the app whose track this is, so the volume control can follow that app alone.
        internal string Source {get{return current==null?null:current.Source;}}
        internal MediaTrack Track {get{return current;}}
        string trackKey;
        byte[] displayedArtwork;
        double optimisticUntil,seekUntil,flagsUntil,playGlyph=22;
        bool optimisticPlaying,flagShuffle,reading,commandRunning,stopped,progressFrames;
        int flagRepeat;
        Grid shuffleFace,repeatFace;

        public PlayerCore(FrameworkElement host,bool testMode)
        {
            this.host=host;
            this.testMode=testMode;
            refresh.Tick+=async(s,e)=>await Read();
            host.IsVisibleChanged+=(s,e)=>UpdateProgressFrames();
        }
        internal async Task Start()
        {
            stopped=false;
            if(testMode)return;
            refresh.Start();
            UpdateProgressFrames();
            await Read();
        }
        internal void Stop()
        {
            stopped=true;
            refresh.Stop();
            UpdateProgressFrames();
        }
        // Builds the transport buttons at this layout's sizes. Shuffle and repeat only appear
        // while the media app says it supports them.
        const double PlayRoom=6;
        internal void ConfigureTransport(double stepWidth,double stepIcon,double playSize,double playIcon)
        {
            playGlyph=playIcon;
            Configure(previous,"Previous","Previous track",()=>{Nudge(previous,-3);return Command("previous");},stepWidth,stepIcon);
            Configure(play,"Play","Play / pause",()=>Command("play"),playSize,playIcon);
            Configure(next,"Next","Next track",()=>{Nudge(next,3);return Command("next");},stepWidth,stepIcon);
            shuffleFace=ConfigureMode(shuffle,new[]{"Shuffle"},"Shuffle",ToggleShuffle,stepWidth,stepIcon-2);
            repeatFace=ConfigureMode(repeat,new[]{"Repeat","RepeatOne"},"Repeat",ToggleRepeat,stepWidth,stepIcon-2);
            // The disc grows on hover, so its slot is larger than the disc and the growth never reaches the edge of the slot.
            // Negative margins keep the layout exactly where it was.
            play.Width=play.Height=playSize+2*PlayRoom;
            play.Margin=new Thickness(1-PlayRoom,-PlayRoom,1-PlayRoom,-PlayRoom);
            play.Background=Brushes.White;
            play.Foreground=Brushes.Black;
            play.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Grid RenderTransformOrigin='.5,.5'><Grid.RenderTransform><ScaleTransform/></Grid.RenderTransform><Ellipse x:Name='Feather' Fill='White' Opacity='.32' Margin='5.1' SnapsToDevicePixels='False' UseLayoutRounding='False'/><Ellipse x:Name='Disc' Fill='White' Margin='5.9' SnapsToDevicePixels='False' UseLayoutRounding='False'/><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Disc' Property='Fill' Value='#DDDDDD'/><Setter TargetName='Feather' Property='Fill' Value='#DDDDDD'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Disc' Property='Fill' Value='#AAAAAA'/><Setter TargetName='Feather' Property='Fill' Value='#AAAAAA'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            play.Content=PlayIcon("Play");
            Motion(play,1.1,.88);
            previous.IsEnabled=play.IsEnabled=next.IsEnabled=false;
            ShowModes();
        }
        internal static void Frame(Button button,double width)
        {
            button.Width=width;
            button.Height=30;
            button.MinHeight=0;
            button.Padding=new Thickness(0);
            button.Margin=new Thickness(1,0,1,0);
            button.Background=new SolidColorBrush(Color.FromArgb(0,255,255,255));
            button.HorizontalContentAlignment=HorizontalAlignment.Center;
        }
        static void Configure(Button button,string symbol,string name,Func<Task> action,double width,double iconSize)
        {
            Frame(button,width);
            var icon=Design.Icon(symbol,iconSize);
            icon.Opacity=IdleInk;
            button.Content=icon;
            Motion(button,1.14,.86);
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            button.Click+=async(s,e)=>await action();
        }
        // A toggle: its icon, with a small dot underneath while it is on.
        static Grid ConfigureMode(Button button,string[] symbols,string name,Func<Task> action,double width,double iconSize)
        {
            Frame(button,width);
            var face=new Grid {Opacity=IdleInk};
            foreach(string symbol in symbols){var icon=Design.Icon(symbol,iconSize);icon.Visibility=face.Children.Count==0?Visibility.Visible:Visibility.Collapsed;face.Children.Add(icon);}
            face.Children.Add(new System.Windows.Shapes.Ellipse {Width=4,Height=4,Fill=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,0),Opacity=0,RenderTransform=new TranslateTransform(0,6)});   // below the icon, not on it
            button.Content=face;
            Motion(button,1.14,.86);
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            button.Click+=async(s,e)=>await action();
            return face;
        }
        Viewbox PlayIcon(string name)
        {
            var icon=Design.Icon(name,playGlyph);
            ((System.Windows.Shapes.Path)icon.Child).Fill=Brushes.Black;
            return icon;
        }
        // A control in a player only lights up while the pointer is over it: a soft round highlight behind its icon, at once, and nothing moves or eases.
        // (The sizes the callers pass are kept for them, but no longer used.)
        internal static void Motion(Button button,double hover,double press)
        {
            // A button that starts with no surface lights up a little under the pointer, and must always un-light again. The play disc gets its white
            // surface after this is set up, so it is told apart by which brush it has now, never by how bright the brush is at that moment.
            var pill=button.Background as SolidColorBrush;
            bool glyphLight=pill!=null&&pill.Color.A==0&&!pill.IsFrozen;
            Action<bool> light=on=>
            {
                if(!glyphLight||!ReferenceEquals(button.Background,pill))return;
                pill.BeginAnimation(SolidColorBrush.ColorProperty,null);
                pill.Color=Color.FromArgb(on?(byte)34:(byte)0,255,255,255);
                var ink=button.Content as UIElement;
                if(ink!=null){ink.BeginAnimation(UIElement.OpacityProperty,null);ink.Opacity=on?1:IdleInk;}
            };
            button.MouseEnter+=(s,e)=>light(true);
            button.MouseLeave+=(s,e)=>light(false);
        }        // A new glyph or track eases in instead of swapping abruptly.
        internal static void Pop(UIElement element,double from,int milliseconds)
        {
            var scale=new ScaleTransform(from,from);
            element.RenderTransform=scale;
            element.RenderTransformOrigin=new Point(.5,.5);
            var motion=new DoubleAnimation(from,1,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.8}};
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,motion);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,motion);
        }
        static void Nudge(Button button,double distance)
        {
            var ink=button.Content as UIElement;
            if(ink==null)return;
            var slide=new TranslateTransform();
            ink.RenderTransform=slide;
            slide.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(distance,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.6}});
        }
        void ShowPlaying(bool playing)
        {
            if(displayedPlaying==playing)return;
            bool first=displayedPlaying==null;
            displayedPlaying=playing;
            var icon=PlayIcon(playing?"Pause":"Play");
            play.Content=icon;
            if(first)return;
            Pop(icon,.62,170);
            icon.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.2,1,TimeSpan.FromMilliseconds(90)));
        }
        // Shuffle and repeat only show while the media app supports them, with a dot while they are on.
        void ShowModes()
        {
            shuffle.Visibility=canShuffle?Visibility.Visible:Visibility.Collapsed;
            repeat.Visibility=canRepeat?Visibility.Visible:Visibility.Collapsed;
            if(shuffleFace==null)return;
            shuffleFace.Children[shuffleFace.Children.Count-1].Opacity=shuffling?1:0;
            repeatFace.Children[repeatFace.Children.Count-1].Opacity=repeating>0?1:0;
            repeatFace.Children[0].Visibility=repeating==1?Visibility.Collapsed:Visibility.Visible;
            repeatFace.Children[1].Visibility=repeating==1?Visibility.Visible:Visibility.Collapsed;
        }
        void ApplyOptimisticPlay()
        {
            if(current==null)return;
            double now=ClockTime();
            bool playing=!current.Playing;
            optimisticPlaying=playing;
            optimisticUntil=now+OptimisticHold;
            current.Playing=playing;
            progressClock.Synchronize(trackKey,progressClock.Position(now),playing,now);
            ShowPlaying(playing);
            UpdateProgressFrames();
        }
        // Set by the app for the bar: a new song is shown (cover, title) at the moment its colors start to change, not before them. It gets the song and what
        // shows it, and calls that when the time comes.
        internal Action<MediaTrack,Action> hold;
        MediaTrack heldTrack;string heldKey;bool showingHeld;
        internal void SetTrack(MediaTrack track)
        {
            PlayHistory.Add(track);
            string newKey=track==null?null:track.Source+"|"+track.Title+"|"+track.Artist;
            if(hold!=null&&!showingHeld)
            {
                if(track==null||newKey==trackKey)heldTrack=null;
                else if(heldTrack!=null){heldTrack=track;heldKey=newKey;return;}
                else if(trackKey!=null)
                {
                    heldTrack=track;heldKey=newKey;
                    hold(track,()=>{var late=heldTrack;heldTrack=null;if(late==null)return;showingHeld=true;try{SetTrack(late);}finally{showingHeld=false;}});
                    return;
                }
            }
            bool held=false;
            if(track!=null&&ClockTime()<optimisticUntil)
            {
                // A sample from before the click must not undo the icon the user just saw change.
                if(track.Playing==optimisticPlaying)optimisticUntil=0;
                else
                {
                    held=true;
                    track.Playing=optimisticPlaying;
                    track.Position=progressClock.Position(ClockTime());
                }
            }
            if(track!=null&&ClockTime()<flagsUntil)
            {
                if(track.Shuffling==flagShuffle&&track.Repeat==flagRepeat)flagsUntil=0;
                else{track.Shuffling=flagShuffle;track.Repeat=flagRepeat;}
            }
            if(track!=null&&newKey==trackKey&&ClockTime()<seekUntil)
            {
                // Samples taken before the media app caught up with a seek must not pull the bar back.
                double predicted=progressClock.Position(ClockTime());
                if(Math.Abs(track.Position-predicted)<1.5)seekUntil=0;
                else track.Position=predicted;
            }
            string previousKey=trackKey;
            current=track;
            UpdateProgressFrames();
            trackKey=newKey;
            if(track==null)
            {
                progressClock.Reset();
                title.Text="Nothing playing";
                artist.Text="Open a music app";
                ShowArtwork(null);
                previous.IsEnabled=play.IsEnabled=next.IsEnabled=false;
                canSeek=canShuffle=canRepeat=shuffling=false;repeating=0;
                ShowModes();
                SetProgress(0);
                return;
            }
            title.Text=string.IsNullOrWhiteSpace(track.Title)?"Media playback":track.Title;
            artist.Text=track.Artist;
            if(previousKey!=null&&previousKey!=trackKey)trackChanged();
            previous.IsEnabled=track.CanPrevious;
            next.IsEnabled=track.CanNext;
            play.IsEnabled=held||(track.Playing?track.CanPause:track.CanPlay);
            canSeek=track.CanSeek;
            canShuffle=track.CanShuffle;canRepeat=track.CanRepeat;shuffling=track.Shuffling;repeating=track.Repeat;
            ShowModes();
            ShowPlaying(track.Playing);
            progressLength=Math.Max(1,track.Duration);
            double now=ClockTime();
            progressClock.Synchronize(trackKey,Math.Min(track.Position,progressLength),track.Playing,now);
            SetProgress(testMode?track.Position:progressClock.Position(now));
            ShowArtwork(track.Artwork);
        }
        void ShowArtwork(byte[] bytes)
        {
            if(MediaSession.SameArtwork(displayedArtwork,bytes))return;
            displayedArtwork=bytes;
            BitmapImage image=null;
            if(bytes!=null)using(var stream=new MemoryStream(bytes))
            {
                image=new BitmapImage();
                image.BeginInit();
                image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=artworkPixels;
                image.StreamSource=stream;
                image.EndInit();
                image.Freeze();
            }
            artwork.Source=image;
            artworkChanged(image);
        }
        void SetProgress(double seconds)
        {
            progressValue=Math.Max(0,Math.Min(progressLength,seconds));
            showProgress();
        }
        static double ClockTime(){return System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;}
        bool OnScreen()
        {
            if(!host.IsVisible)return false;
            var window=Window.GetWindow(host);
            return window==null||window.WindowState!=WindowState.Minimized;
        }
        // A Rendering subscription forces WPF to compose continuously, so it only
        // exists while there is movement to show.
        internal void UpdateProgressFrames()
        {
            bool wanted=!testMode&&!stopped&&current!=null&&current.Playing&&OnScreen();
            if(wanted==progressFrames)return;
            progressFrames=wanted;
            if(wanted)CompositionTarget.Rendering+=OnProgressFrame;
            else CompositionTarget.Rendering-=OnProgressFrame;
        }
        // The progress moves a few pixels a minute, so it is brought up to date fifteen times a second rather than on every one of the sixty frames:
        // every frame the window is redrawn costs the processor, and measured, redrawing it that often for a moving bar took most of a core.
        const double ProgressStep=1d/15;
        double lastProgressFrame;
        void OnProgressFrame(object sender,EventArgs e)
        {
            double now=ClockTime();
            if(now-lastProgressFrame<ProgressStep)return;
            lastProgressFrame=now;
            SetProgress(progressClock.Position(now));
        }
        async Task Read()
        {
            if(reading||stopped||commandRunning||!OnScreen())return;
            reading=true;
            try
            {
                var track=await session.Read();
                if(!stopped)SetTrack(track);
            }
            catch
            {
                showNote("Media information unavailable");
            }
            finally
            {
                reading=false;
            }
        }
        // Sends one request to the media app, then picks up its new state sooner than the regular refresh.
        async Task Send(Func<Task<bool>> request)
        {
            if(commandRunning||stopped||testMode)return;
            commandRunning=true;
            bool accepted=false;
            try
            {
                accepted=await request();
                if(!accepted)showNote("The media app declined that action");
            }
            catch
            {
                showNote("The media app did not respond");
            }
            finally
            {
                commandRunning=false;
            }
            if(!accepted){optimisticUntil=0;seekUntil=0;flagsUntil=0;}
            await Read();
            if(!accepted)return;
            await Task.Delay(120);
            await Read();
            await Task.Delay(180);
            await Read();
        }
        async Task Command(string command)
        {
            if(commandRunning||stopped)return;
            if(command=="play")ApplyOptimisticPlay();
            await Send(()=>session.Command(command));
        }
        // The bar moves to the new position at once and ignores older samples until the media app agrees.
        internal async Task Seek(double seconds)
        {
            if(current==null||!canSeek||stopped)return;
            seconds=Math.Max(0,Math.Min(progressLength,seconds));
            double now=ClockTime();
            seekUntil=now+OptimisticHold;
            progressClock.Synchronize(trackKey,seconds,current.Playing,now);
            current.Position=seconds;
            SetProgress(seconds);
            await Send(()=>session.Seek(seconds));
        }
        void HoldModes(bool shuffleOn,int repeatMode)
        {
            shuffling=flagShuffle=shuffleOn;
            repeating=flagRepeat=repeatMode;
            flagsUntil=ClockTime()+OptimisticHold;
            ShowModes();
        }
        async Task ToggleShuffle()
        {
            if(current==null||!canShuffle||stopped)return;
            bool value=!shuffling;
            HoldModes(value,repeating);
            await Send(()=>session.SetShuffle(value));
        }
        // Off, then repeat all, then repeat this track.
        async Task ToggleRepeat()
        {
            if(current==null||!canRepeat||stopped)return;
            int value=repeating==0?2:repeating==2?1:0;
            HoldModes(shuffling,value);
            await Send(()=>session.SetRepeat(value));
        }
    }
}
