using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shell;
using Forms=System.Windows.Forms;
using ResolutionSwitcher;

namespace Lucent
{
    internal static class Entry
    {
        static void LogCrash(object error)
        {
            try
            {
                Directory.CreateDirectory(StateStore.Folder);
                File.AppendAllText(Path.Combine(StateStore.Folder,"crash.log"),DateTime.Now.ToString("s")+" "+Design.Version+Environment.NewLine+error+Environment.NewLine+Environment.NewLine);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
        [STAThread] public static void Main(string[] args)
        {
            if(args.Length==3&&args[0]=="--effects"){NativeEffects.Worker(args[1],int.Parse(args[2]));return;}
            if(args.Length>0&&args[0]=="--custom-resolution-trial"){CustomResolutionWorker.Run(args);return;}
            if(args.Length>0&&args[0]=="--refresh-only-trial"){RefreshRateWorker.Run(args);return;}
            bool capture = args.Contains("--capture")||args.Contains("--design-test");
            bool unique;using(var owner=new Mutex(true,capture ? "Local\\Lucent.Preview.Capture" : LegacyNames.SingleInstanceMutex,out unique))
            {
                if(!unique)
                {
                    // Opening the app again brings the running copy forward, even from the tray.
                    if(!capture&&!Shell.SignalRunningInstance())MessageBox.Show("Lucent or a previous Lucent instance is already running. Close it before opening this build.","Lucent");
                    return;
                }
                if(capture)StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data");
                Forms.Application.EnableVisualStyles();var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
                AppDomain.CurrentDomain.UnhandledException+=(s,e)=>LogCrash(e.ExceptionObject);
                app.DispatcherUnhandledException+=(s,e)=>
                {
                    if(args.Contains("--capture"))
                    {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"capture-error.txt"),e.Exception.ToString());
                        app.Shutdown(1);
                    }
                    else
                    {
                        LogCrash(e.Exception);
                        MessageBox.Show(e.Exception.Message,"Lucent",MessageBoxButton.OK,MessageBoxImage.Information);
                    }
                    e.Handled=true;
                };
                Shell window;
                try { window=new Shell(capture); }
                catch(Exception error)
                {
                    if(!capture)throw;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"capture-error.txt"),error.ToString());
                    Environment.ExitCode=1;return;
                }
                app.MainWindow=window;
                if(args.Contains("--capture"))
                {
                    window.TestMode=true;
                    window.Show();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>
                    {
                        await window.CapturePages();
                        window.Close();
                    }));
                    Environment.ExitCode=app.Run();
                }
                else{window.Show();if(args.Contains("--tray"))window.Hide();app.Run();}
            }
        }
    }
    internal sealed partial class Shell:Window
    {
        Preferences preferences;Look current;string activeProfile="Desktop";string page="Home";
        readonly Grid root=new Grid(),workspace=new Grid();
        readonly StackPanel nav=new StackPanel();
        // The holder of the page being shown. Each page keeps its own holder and scroller (see ShellMotion), and this follows the current one.
        ContentControl content;
        readonly DispatcherTimer statusTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};
        readonly TextBlock status=new TextBlock(),displayStatus=new TextBlock();
        readonly Dictionary<string,FrameworkElement> pages=new Dictionary<string,FrameworkElement>();
        readonly List<HotkeyWindow> hotkeys=new List<HotkeyWindow>();
        readonly BackgroundAppBoost boost=new BackgroundAppBoost();
        readonly Dictionary<CheckBox,BoostCandidate> candidates=new Dictionary<CheckBox,BoostCandidate>();
        readonly DispatcherTimer saveTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)},effectTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(65)},profileTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};
        readonly DispatcherTimer zoomHoldTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(16)};
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        readonly Dictionary<string,Slider> sliders=new Dictionary<string,Slider>();
        readonly List<CheckBox> autoChecks=new List<CheckBox>();
        readonly List<Button> navigationButtons=new List<Button>();
        Forms.NotifyIcon tray;
        TrayPopup trayPopup;
        EffectsClient effects;
        CrosshairOverlay crosshair;
        bool applyLook,comparing,zoomed,building,closing,scanning,profilePolling;
        AudioLimiter limiter;
        List<DisplayInfo> displays;
        DisplayInfo selected;
        StackPanel profilesList;
        CheckBox darkBoostToggle;
        public bool TestMode;
        AnimatedBackground animatedBackground, backgroundPreview;
        const double PreviewVisibility=.68;
        Slider backgroundVisibility;


        Grid previewPopup, previewStage;
        TextBlock previewTitle;
        Button applyPreview;
        string previewedBackground;
        bool previewingBackground {get{return previewedBackground!=null;}}
        readonly Dictionary<string,Button> backgroundButtons=new Dictionary<string,Button>();
        TextBlock backgroundName;
        bool customBackgroundLoaded;
        public Shell(bool testMode=false)
        {
            TestMode=testMode;Design.EnableButtonMotion();
            preferences=StateStore.Load();
            current=preferences.Desktop.Copy();
            current.DarkEnabled=false;
            Title="Lucent";
            Width=1200;
            Height=780;
            MinWidth=960;
            MinHeight=680;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            AvoidTooSmallScreen();
            // The window is drawn by Lucent itself (see-through, clipped to a smooth rounded shape), with the same corners Windows gives its own windows.
            AllowsTransparency=true;
            Background=Brushes.Transparent;
            Foreground=Brushes.White;
            FontFamily=Design.Font;
            FontSize=14;
            FontWeight=FontWeights.Medium;
            SnapsToDevicePixels=true;
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.CanResize;
            Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Lucent.ico")));
            InitializeMutablePalette();
            Resources=Design.Resources(Styles);
            Resources["AdaptiveControlBrush"]=optionSurface;
            Resources["AdaptiveSelectionBrush"]=selectionSurface;
            Resources["AdaptiveTrackBrush"]=trackSurface;
            Resources["AdaptiveSettingBrush"]=settingsGroupSurface;
            Resources["AdaptiveTooltipBrush"]=tooltipSurface;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
            TextOptions.SetTextHintingMode(this,TextHintingMode.Auto);
            new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(5),CornerRadius=new CornerRadius(0),GlassFrameThickness=new Thickness(0)}.Set(this);
            UseLayoutRounding=true;root.Background=Brushes.Black;Content=windowSurface;windowSurface.Children.Add(root);
            windowSurface.SizeChanged+=(sender,args)=>ShapeWindow();
            StateChanged+=(sender,args)=>ShapeWindow();
            animatedBackground=new AnimatedBackground {IsHitTestVisible=false,Visibility=Visibility.Collapsed};
            root.Children.Add(animatedBackground);
            backgroundPreview=new AnimatedBackground {Visibility=Visibility.Collapsed,Opacity=.55};

            Panel.SetZIndex(workspace,3);
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(70)});
            workspace.ColumnDefinitions.Add(new ColumnDefinition());
            workspace.RenderTransformOrigin=new Point(.07,.96);workspace.RenderTransform=appFold;root.Children.Add(workspace);
            BuildNav();BuildBody();BuildPages();ShowPage("Home");
            BuildPreviewPopup();InitializeAdaptiveTheme();BuildFloatingSettings();
            // Notices are a card at the top: it drops in, waits, and slides out.
            BuildNotice();
            statusTimer.Tick+=(sender,args)=>HideStatus();
            saveTimer.Tick+=(s,e)=>{saveTimer.Stop();SavePreferences();};effectTimer.Tick+=(s,e)=>{effectTimer.Stop();UpdateEffects();};
            profileTimer.Tick+=(s,e)=>{PollProfiles();var ignored=RefreshPlayStates();var sampled=RefreshSystemStats();};
            memoryTimer.Tick+=(s,e)=>{if(IsVisible && WindowState!=WindowState.Minimized)RefreshMemoryDashboard();};
            zoomHoldTimer.Tick+=(s,e)=>PollZoomBind();
            Loaded+=async (s,e)=>
            {
                if(!TestMode)
                {
                    StartUpdateChecks();
                    RecoverColorsQuietly();
                    Count(c=>c.Launches++);StartStatsClock();
                    SetupTray();
                    RegisterKeys();
                    profileTimer.Start();
                    ListenForDisplayChanges();
                    ListenForActivation();
                    StartZoomPolling();
                    if(StateStore.LoadProblem!=null)SetStatus(StateStore.LoadProblem);
                    if(!string.IsNullOrEmpty(preferences.BackgroundFile))await LoadBackground(preferences.BackgroundFile);
                }
                ApplyAppearance();
                AttachPages();
            };
            StateChanged+=(s,e)=>
            {
                ApplyAppearance();
            };
            IsVisibleChanged+=(s,e)=>ApplyAppearance();
            Activated+=(s,e)=>ApplyAppearance();Deactivated+=(s,e)=>ApplyAppearance();
            Closing+=OnClosing;
        }
        void BuildNav(){BuildModernNav();}
        void BuildBody(){BuildModernBody();}
        void BuildPages()
        {
            building=true;displays=DisplayService.GetDisplays();selected=displays.FirstOrDefault(x=>x.IsPrimary)??displays.FirstOrDefault();
            pages["Home"]=HomePage();
            pages["Video"]=VideoPage();
            pages["Resolution"]=ResolutionPage();
            pages["Profiles"]=ProfilesPage();
            pages["Crosshair"]=CrosshairPage();
            pages["Game Boost"]=BoostPage();
            pages["Visual tools"]=DarkPage();
            pages["Settings"]=SettingsPage();
            building=false;
        }
        void ShowPage(string title)
        {
            // Backgrounds live in Settings now; the old name opens that tab.
            if(title=="Backgrounds"){SetSettingsOpen(true);SelectSettingsCategory("Backgrounds");return;}
            if(title=="Settings")
            {
                SetSettingsOpen(!settingsOpen);
                return;
            }
            if(settingsOpen)SetSettingsOpen(false);
            bool fromPlayer=bigPlayerOpen;
            SetBigPlayerOpen(false);
            if(!pages.ContainsKey(title))return;
            // Clicking the tab you are already on does nothing, so spamming it cannot replay the opening motion.
            if(title==page&&content!=null&&ReferenceEquals(content.Content,pages[title]))return;
            HideStatus();
            page=title;
            ChangePage(title,!fromPlayer);
            UpdateRailSelection();
            if(title=="Home"){UpdateHome();var sampling=RefreshSystemStats();}
            if(title=="Profiles")ScanGames(false);
            memoryTimer.Stop();
            if(title=="Game Boost"){RefreshMemoryDashboard();if(!TestMode)memoryTimer.Start();}
        }
        StackPanel Page(string title,string subtitle)
        {
            var stack=new StackPanel {Margin=new Thickness(0,16,0,5)};
            stack.Children.Add(Text(title,32,true));
            var sub=Text(subtitle,14);
            sub.Foreground=Muted;
            sub.Margin=new Thickness(0,6,0,26);
            stack.Children.Add(sub);
            return stack;
        }
        FrameworkElement SettingsPage(){return PolishedSettingsPage();}
        static Grid ResponsiveColumns(FrameworkElement left,FrameworkElement right,double leftWeight=1)
        {
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(leftWeight,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            grid.Children.Add(left);grid.Children.Add(right);
            Action arrange=()=>
            {
                bool wide=grid.ActualWidth>=880;
                grid.ColumnDefinitions[1].Width=wide?new GridLength(1,GridUnitType.Star):new GridLength(0);
                grid.ColumnDefinitions[0].Width=wide?new GridLength(leftWeight,GridUnitType.Star):new GridLength(1,GridUnitType.Star);
                left.Margin=wide?new Thickness(0,0,16,0):new Thickness(0);
                Grid.SetColumn(right,wide?1:0);Grid.SetRow(right,wide?0:1);
            };
            grid.SizeChanged+=(s,e)=>arrange();arrange();return grid;
        }
        // The whole app folds down toward the player as it opens (the player grows out of the same place, the bar's cover), and unfolds as it closes.
        readonly ScaleTransform appFold=new ScaleTransform(1,1);
        const double FoldedScale=.6;
        DispatcherTimer foldSettle;
        void FoldApp(bool into)
        {
            // The app is moved as one picture while it folds; drawing all of it again at every step made opening and closing the player lag.
            workspace.CacheMode=new BitmapCache();
            if(foldSettle==null){foldSettle=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(520)};foldSettle.Tick+=(s,e)=>{foldSettle.Stop();workspace.CacheMode=null;};}
            foldSettle.Stop();foldSettle.Start();
            double from=appFold.ScaleX;
            var scale=new DoubleAnimation(from,into?FoldedScale:1,TimeSpan.FromMilliseconds(into?400:340)){EasingFunction=new CubicEase{EasingMode=into?EasingMode.EaseIn:EasingMode.EaseOut}};
            if(!into)scale.Completed+=(s,e)=>{if(!bigPlayerOpen){appFold.BeginAnimation(ScaleTransform.ScaleXProperty,null);appFold.BeginAnimation(ScaleTransform.ScaleYProperty,null);appFold.ScaleX=appFold.ScaleY=1;}};
            appFold.BeginAnimation(ScaleTransform.ScaleXProperty,scale);appFold.BeginAnimation(ScaleTransform.ScaleYProperty,scale.Clone());
            Design.Animate(workspace,OpacityProperty,workspace.Opacity,into?0:1,into?330:300);
        }
        // A player in a window of its own: the app gives a small nod toward it, as if it had been tucked into the player.
        void NodApp()
        {
            var nod=new DoubleAnimationUsingKeyFrames();
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(.955,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)),new CubicEase{EasingMode=EasingMode.EaseOut}));
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)),new CubicEase{EasingMode=EasingMode.EaseInOut}));
            nod.Completed+=(s,e)=>{if(!bigPlayerOpen){appFold.BeginAnimation(ScaleTransform.ScaleXProperty,null);appFold.BeginAnimation(ScaleTransform.ScaleYProperty,null);appFold.ScaleX=appFold.ScaleY=1;}};
            appFold.BeginAnimation(ScaleTransform.ScaleXProperty,nod);appFold.BeginAnimation(ScaleTransform.ScaleYProperty,nod.Clone());
        }
        // A copy of what the app last read, for a player to start from (a player changes the track it is given).
        static MediaTrack CopyTrack(MediaTrack t)
        {
            return new MediaTrack {Title=t.Title,Artist=t.Artist,Source=t.Source,Playing=t.Playing,CanPlay=t.CanPlay,CanPause=t.CanPause,CanPrevious=t.CanPrevious,CanNext=t.CanNext,CanSeek=t.CanSeek,CanShuffle=t.CanShuffle,CanRepeat=t.CanRepeat,Shuffling=t.Shuffling,Repeat=t.Repeat,Position=t.Position,Duration=t.Duration,Artwork=t.Artwork};
        }
        MediaPlayerWindow mediaPlayer;
        BigPlayerWindow bigPlayerWindow;
        void ToggleBigPlayer()
        {
            if(bigPlayerWindow!=null){bigPlayerWindow.Close();return;}
            SetBigPlayerOpen(!bigPlayerOpen);
        }
        // The big player can live in the app or in a window of its own, never both.
        void PopOutBigPlayer()
        {
            CloseOtherPlayers("window");
            bigPlayerWindow=new BigPlayerWindow(this,TestMode,DockBigPlayer);
            bigPlayerWindow.Closed+=(sender,args)=>{bigPlayerWindow=null;ShowPlayerOpen(bigPlayerButton,bigPlayerOpen);ApplyPlayerBar();};
            if(ambientTrack!=null)bigPlayerWindow.View.Core.SetTrack(CopyTrack(ambientTrack));
            bigPlayerWindow.Show();
            ShowPlayerOpen(bigPlayerButton,true);
            ApplyPlayerBar();
        }
        void DockBigPlayer()
        {
            if(bigPlayerWindow!=null)bigPlayerWindow.Close();
            // The main app comes back too, even from the tray or the taskbar: docking is the way back into it. (Closing the player window only closes it.)
            if(!TestMode)BringToFront();
            SetBigPlayerOpen(true);
        }
        void MediaPopup()
        {
            if(mediaPlayer!=null){mediaPlayer.Close();return;}
            CloseOtherPlayers("mini");
            mediaPlayer=new MediaPlayerWindow(this,preferences,TestMode){Changed=SaveSoon,ShowAppSettings=()=>{Open(page);SetSettingsOpen(true);SelectSettingsCategory("Mini Player");}};
            mediaPlayer.Closed+=(sender,args)=>{mediaPlayer=null;ShowPlayerOpen(playerDockButton,false);ApplyPlayerBar();};
            // The song is known from the start (the app already reads it), so the player does not say "Nothing playing" until its own first read comes back.
            if(ambientTrack!=null)mediaPlayer.SetTrack(CopyTrack(ambientTrack));
            mediaPlayer.Show();
            ShowPlayerOpen(playerDockButton,true);
            ApplyPlayerBar();
        }
        void SetBigPlayerOpen(bool open)
        {
            if(open==bigPlayerOpen)return;
            if(open)CloseOtherPlayers("big");
            bigPlayerOpen=open;
            ShowPlayerOpen(bigPlayerButton,open||bigPlayerWindow!=null);
            if(open&&ambientTrack!=null&&bigPlayerView.Core.Track==null)bigPlayerView.Core.SetTrack(CopyTrack(ambientTrack));
            bigPlayerView.SetOpen(open);
            FoldApp(open);
            ApplyPlayerBar();
        }        static void ShowPlayerOpen(Button button,bool open)
        {
            if(button==null)return;
            button.BeginAnimation(OpacityProperty,null);
            button.Opacity=open?1:.65;
        }
        // The window really goes away: from the tray menu, or because Windows is signing out or shutting down.
        bool exiting;
        // Closing the window hides it in the tray only when that is wanted, there is a tray icon, and Lucent is not really quitting.
        internal static bool ShouldHideOnClose(bool exiting,bool testMode,bool closeToTray,bool hasTray){return !exiting&&!testMode&&closeToTray&&hasTray;}
        void ExitApp(){exiting=true;Close();}
        void OnSessionEnding(object sender,Microsoft.Win32.SessionEndingEventArgs e){exiting=true;}
        void HideToTray()
        {
            // The window shrinks toward its corner and fades, and then it is hidden.
            AnimateAway(()=>Hide());
            if(preferences.TrayHintShown||tray==null)return;
            preferences.TrayHintShown=true;SaveSoon();
            try{tray.ShowBalloonTip(5000,"Lucent is still running","It is in the system tray. Click the icon to open it, or right-click it to exit.",Forms.ToolTipIcon.Info);}catch(Exception){}
        }
        void Open(string title){BringToFront();ShowPage(title);}
        // From the tray the window is made invisible before it is shown, so it does not appear whole for a moment before it starts to grow.
        void BringToFront(){if(!TestMode&&!preferences.PerformanceMode&&!leaving&&(!IsVisible||WindowState==WindowState.Minimized)){animateRestore=true;Opacity=0;EnsureVisibleSoon();}Show();WindowState=WindowState.Normal;Activate();}
        internal static string ActivationSignal="Local\\Lucent.Activate";
        EventWaitHandle activationSignal;RegisteredWaitHandle activationWait;
        void ListenForActivation()
        {
            activationSignal=new EventWaitHandle(false,EventResetMode.AutoReset,ActivationSignal);
            activationWait=ThreadPool.RegisterWaitForSingleObject(activationSignal,(state,timedOut)=>Dispatcher.BeginInvoke(new Action(BringToFront)),null,Timeout.Infinite,false);
        }
        internal static bool SignalRunningInstance()
        {
            try{using(var signal=EventWaitHandle.OpenExisting(ActivationSignal))return signal.Set();}
            catch(WaitHandleCannotBeOpenedException){return false;}
            catch(UnauthorizedAccessException){return false;}
        }
        void SavePreferences()
        {
            try{StateStore.Save(preferences);}
            catch(IOException error){SetStatus("Settings could not be saved: "+error.Message);}
            catch(UnauthorizedAccessException error){SetStatus("Settings could not be saved: "+error.Message);}
        }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        // The round corners of the window, as round as Windows' own. A maximized window fills the screen, so it has none.
        internal const double WindowRadius=8;
        internal double CurrentWindowRadius {get{return WindowState==WindowState.Normal?WindowRadius:0;}}
        void ShapeWindow()
        {
            double radius=CurrentWindowRadius;
            if(radius==0||windowSurface.ActualWidth<=0||windowSurface.ActualHeight<=0){windowSurface.Clip=null;return;}
            var shape=new RectangleGeometry(new Rect(0,0,windowSurface.ActualWidth,windowSurface.ActualHeight),radius,radius);
            shape.Freeze();windowSurface.Clip=shape;
        }
        readonly SolidColorBrush sidebarSurface=new SolidColorBrush(Color.FromArgb(224,8,8,8));
        readonly SolidColorBrush selectedDisplaySurface=new SolidColorBrush(Color.FromArgb(220,39,39,39));
        readonly SolidColorBrush displaySurface=new SolidColorBrush(Color.FromArgb(188,16,16,16));
        readonly LinearGradientBrush cardSurface = new LinearGradientBrush(
            Color.FromArgb(205,18,18,18),Color.FromArgb(205,18,18,18),70);
        string CurrentModeText()
        {
            if(selected==null)return "No active display";
            var m=DisplayService.GetCurrentMode(selected.DeviceName);
            return m==null?"Display unavailable":m.ResolutionText+" · "+m.Frequency+" Hz";
        }
        // ---- Notices: a card that drops in from the top like a phone notification, waits, and slides back out.
        readonly Border statusCard=new Border();
        readonly TextBlock statusTitle=new TextBlock();
        readonly TranslateTransform statusSlide=new TranslateTransform(0,-110);
        bool statusShown;
        void BuildNotice()
        {
            statusTitle.FontSize=15;statusTitle.FontWeight=FontWeights.Bold;statusTitle.FontFamily=Design.DisplayFont;statusTitle.Foreground=Brushes.White;statusTitle.Visibility=Visibility.Collapsed;statusTitle.Margin=new Thickness(0,0,0,2);
            status.FontSize=14;status.FontWeight=FontWeights.Medium;status.FontFamily=Design.Font;status.Foreground=Brushes.White;status.TextWrapping=TextWrapping.Wrap;status.MaxWidth=470;status.TextAlignment=TextAlignment.Left;
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            words.Children.Add(statusTitle);words.Children.Add(status);
            var mark=Art.Logo(28);mark.Margin=new Thickness(0,0,15,0);mark.VerticalAlignment=VerticalAlignment.Center;
            var row=new DockPanel();DockPanel.SetDock(mark,Dock.Left);row.Children.Add(mark);row.Children.Add(words);
            statusCard.Child=row;
            statusCard.Background=new SolidColorBrush(Color.FromArgb(178,30,30,32));
            statusCard.BorderThickness=new Thickness(0); // no border: the notice is a see-through card
            statusCard.CornerRadius=new CornerRadius(22);statusCard.Padding=new Thickness(18,14,24,14);
            statusCard.Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=30,ShadowDepth=8,Opacity=.55,Color=Colors.Black};
            statusCard.HorizontalAlignment=HorizontalAlignment.Center;statusCard.VerticalAlignment=VerticalAlignment.Top;statusCard.Margin=new Thickness(190,16,190,0);statusCard.MaxWidth=620;
            statusCard.RenderTransform=statusSlide;statusCard.Visibility=Visibility.Collapsed;statusCard.IsHitTestVisible=false;
            Panel.SetZIndex(statusCard,10);root.Children.Add(statusCard);
        }
        // The card takes the colors of what is playing, the same way the rest of the window does, and stays plain dark when nothing is.
        static Color Mix(Color from,Color to,double amount){return Color.FromRgb((byte)(from.R+(to.R-from.R)*amount),(byte)(from.G+(to.G-from.G)*amount),(byte)(from.B+(to.B-from.B)*amount));}
        void TintNotice()
        {
            var neutral=Color.FromRgb(46,46,48);
            Color accent=Adaptive?mediaAccent:neutral;
            double strength=Adaptive?Math.Max(0,Math.Min(1,preferences.ThemeIntensity)):0;
            double lift=accent==neutral?0:.20+.45*strength;
            var dark=Color.FromRgb(26,26,28);
            var tinted=Mix(dark,Mix(Colors.Black,accent,.78),lift);
            statusCard.Background=new SolidColorBrush(Color.FromArgb(178,tinted.R,tinted.G,tinted.B));
            var edge=Mix(Color.FromRgb(120,120,124),accent,accent==neutral?0:.7);
            statusCard.BorderBrush=null;
        }
        void HideStatus()
        {
            statusTimer.Stop();
            if(!statusShown)return;
            statusShown=false;
            if(TestMode){statusCard.Visibility=Visibility.Collapsed;return;}
            var leave=new DoubleAnimation(-110,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
            leave.Completed+=(s,e)=>{if(!statusShown)statusCard.Visibility=Visibility.Collapsed;};
            statusSlide.BeginAnimation(TranslateTransform.YProperty,leave);
            statusCard.BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(240)));
        }
        void SetStatus(string value){ShowNotice(null,value,3);}
        // title is an optional bold first line; seconds is how long it stays.
        internal void ShowNotice(string title,string body,int seconds)
        {
            statusTimer.Stop();
            statusTitle.Text=title??"";statusTitle.Visibility=string.IsNullOrEmpty(title)?Visibility.Collapsed:Visibility.Visible;
            status.Text=body;status.Foreground=string.IsNullOrEmpty(title)?Brushes.White:new SolidColorBrush(Color.FromArgb(205,255,255,255));
            TintNotice();
            bool already=statusShown;statusShown=true;
            statusCard.Visibility=Visibility.Visible;
            if(TestMode){statusSlide.BeginAnimation(TranslateTransform.YProperty,null);statusSlide.Y=0;statusCard.BeginAnimation(OpacityProperty,null);statusCard.Opacity=1;}
            else
            {
                statusSlide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(already?statusSlide.Y:-110,0,TimeSpan.FromMilliseconds(520)){EasingFunction=new BackEase {EasingMode=EasingMode.EaseOut,Amplitude=.55}});
                statusCard.BeginAnimation(OpacityProperty,new DoubleAnimation(already?statusCard.Opacity:0,1,TimeSpan.FromMilliseconds(220)));
            }
            statusTimer.Interval=TimeSpan.FromSeconds(seconds);statusTimer.Start();
        }
        // Tests must never wait on a dialog nobody can see, so there the message goes to the status line instead.
        void Message(string value){if(TestMode){SetStatus(value);return;}MessageBox.Show(this,value,"Lucent",MessageBoxButton.OK,MessageBoxImage.Information);}
        void OnClosing(object sender,System.ComponentModel.CancelEventArgs args)
        {
            if(closing)return;
            // Closing the window goes to the tray instead of quitting, unless this is a real exit (the tray menu, signing out or shutting down).
            if(ShouldHideOnClose(exiting,TestMode,preferences.CloseToTray,tray!=null))
            {
                args.Cancel=true;
                HideToTray();
                return;
            }
            closing=true;
            Microsoft.Win32.SystemEvents.SessionEnding-=OnSessionEnding;
            foreach(var timer in new[]{paletteTimer,statusTimer,profileTimer,memoryTimer,zoomHoldTimer,saveTimer,effectTimer})timer.Stop();
            if(zoomEdgeTimer!=null){zoomEdgeTimer.Dispose();zoomEdgeTimer=null;}
            UpdateZoomWheel(false);
            DisposeDarkHold();
            if(systemStats!=null){systemStats.Dispose();systemStats=null;}
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=OnDisplaySettingsChanged;
            if(activationWait!=null)activationWait.Unregister(null);
            if(activationSignal!=null)activationSignal.Dispose();
            if(mediaPlayer!=null)mediaPlayer.Close();
            if(bigPlayerWindow!=null)bigPlayerWindow.Close();
            bigPlayerView.Core.Stop();
            playerBar.Core.Stop();
            DisableEffects();
            if(limiter!=null)limiter.Dispose();
            if(!TestMode)SavePreferences();
            if(crosshair!=null)crosshair.Dispose();
            foreach(var item in bindings.Values)item.Dispose();
            if(trayPopup!=null)trayPopup.Close();
            if(tray!=null){tray.Visible=false;tray.Dispose();}
            backgroundPreview.Dispose();
            animatedBackground.Dispose();
        }
        public async Task CapturePages()
        {
            await CaptureModernPages();
        }
        void CaptureFrame(string name)
        {
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);
            image.Render(windowSurface);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews",name+".png")))encoder.Save(file);
        }
        static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(188,188,188));
        static TextBlock Text(string text,double size=13,bool bold=false)
        {
            var label=new TextBlock {Text=text,FontSize=Math.Max(12,size),FontWeight=bold?FontWeights.Bold:FontWeights.Medium,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,8)};
            if(bold&&size>=18)label.FontFamily=Design.DisplayFont;
            return label;
        }
        // Settings keep their own neutral look, whatever is playing: their sections are a plain veil with no outline (the page behind keeps its colors while they are open).
        static readonly Brush plainSectionSurface=FrozenBrush(Color.FromArgb(11,255,255,255));
        static Brush FrozenBrush(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
        SectionPanel Section(string title,string subtitle,bool neutral=false)
        {
            var panel=new StackPanel {Margin=new Thickness(22,19,22,19)};
            panel.Children.Add(Text(title,20,true));
            if(!string.IsNullOrEmpty(subtitle)){var hint=Text(subtitle,13);hint.Foreground=Muted;panel.Children.Add(hint);}
            var border=new Border
            {
                Background=neutral?plainSectionSurface:cardSurface,
                BorderBrush=null,
                BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,0,14),Child=neutral?(UIElement)panel:Design.Glass(panel,16)
            };
            var holder=new StackPanel();holder.Children.Add(border);return new SectionPanel(panel,holder);
        }
        // SectionPanel forwards added children into its bordered content.
        sealed class SectionPanel:StackPanel
        {
            public SectionPanel(StackPanel content,StackPanel holder)
            {
                Content=content;
                base.Children.Add(holder);
            }
            public StackPanel Content;
            public new UIElementCollection Children
            {
                get
                {
                    return Content.Children;
                }
            }
        }
        static Color WarmSwatch(double strength)
        {
            var m = ColorMath.Build(new Look { Red = strength });
            Func<int, byte> channel = j => (byte)Math.Round(255 * Math.Max(0, Math.Min(1, m[j] + m[5 + j] + m[10 + j] + m[20 + j])));
            return Color.FromRgb(channel(0), channel(1), channel(2));
        }
        void AddSlider(StackPanel section, string label, double min, double max, double value, Action<double> change, string suffix)
        {
            var parent = section is SectionPanel ? ((SectionPanel)section).Content : section;
            bool hero = label == "Vibrance";
            bool eyeCare = label == "Red Filter" || label == "Screen Dim";
            string format = suffix == "×" ? "0.0" : "0";
            var number = Text(value.ToString(format) + suffix, hero ? 44 : 18, true);
            number.VerticalAlignment = VerticalAlignment.Center;
            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = value,
                Height = hero ? 58 : 48,
                Margin = eyeCare ? new Thickness(8, 2, 12, 2) : new Thickness(0, 5, 0, 8),
                SmallChange = suffix == "×" ? .1 : 1, ToolTip = label, IsMoveToPointEnabled=true
            };
            sliders[label] = slider;
            if (eyeCare)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(122) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
                var caption = IconLabel(label=="Red Filter"?"Sun":"Dim",label,14);
                caption.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(caption);
                Grid.SetColumn(slider, 1);
                row.Children.Add(slider);
                Grid.SetColumn(number, 2);
                number.HorizontalAlignment = HorizontalAlignment.Right;
                row.Children.Add(number);
                if (label == "Red Filter")
                {
                    // What white looks like through the filter at this strength, so the yellow, orange and red can be seen without turning it on.
                    var swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(WarmSwatch(value)), ToolTip = "White through this filter" };
                    Grid.SetColumn(swatch, 2);
                    row.Children.Add(swatch);
                    slider.ValueChanged += (s, e) => swatch.Background = new SolidColorBrush(WarmSwatch(slider.Value));
                }
                parent.Children.Add(row);
            }
            else
            {
                var row = new DockPanel();
                DockPanel.SetDock(number, Dock.Right);
                row.Children.Add(number);
                if (hero)
                {
                    parent.Children.RemoveAt(0);
                    var heading = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    heading.Children.Add(Art.Icon("Vibrance", 28));
                    heading.Children.Add(Text(label, 24, true));
                    row.Children.Add(heading);
                    parent.Children.Insert(0, row);
                }
                else
                {
                    row.Children.Add(Text(label, 15));
                    parent.Children.Add(row);
                }
                parent.Children.Add(slider);
            }
            var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.White, ShadowDepth = 0 };
            slider.Effect = null;
            Action updateGlow = () =>
            {
                double power = (slider.Value - min) / (max - min);
                glow.BlurRadius = 8 + power * 20;
                glow.Opacity = .14 + power * .43;
            };
            updateGlow();
            slider.ValueChanged += (sender, args) =>
            {
                number.Text = slider.Value.ToString(format) + suffix;
                change(slider.Value);
                updateGlow();
                Changed();
            };
        }
        Button Action(string text,Action action)
        {
            var button=new Button {Content=text,Padding=new Thickness(14,10,14,10),Margin=new Thickness(0,4,7,4),HorizontalAlignment=HorizontalAlignment.Left};
            button.Click+=(s,e)=>
            {
                try
                {
                    action();
                }
                catch(Exception error)
                {
                    Message(error.Message);
                }
            };
            return button;
        }
        CheckBox Toggle(string text,bool value,Action<bool> change)
        {
            var check=new CheckBox {Content=text,IsChecked=value,Margin=new Thickness(0,11,0,11),Style=(Style)Resources["ToggleStyle"]};
            check.Click+=(s,e)=>
            {
                try
                {
                    change(check.IsChecked==true);
                }
                catch(Exception error)
                {
                    check.IsChecked=!check.IsChecked;
                    Message(error.Message);
                }
            };
            return check;
        }
        CheckBox AutoToggle(string title)
        {
            var check=Toggle(title,preferences.AutoProfiles,value=>
            {
                preferences.AutoProfiles=value;
                foreach(var other in autoChecks)other.IsChecked=value;
                lastAutoProfile=null;
                if(!value&&activeProfile!="Desktop")LoadLook(preferences.Desktop,"Desktop");
                SaveSoon();
            });
            autoChecks.Add(check);
            return check;
        }
        const string Styles=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
        <Style TargetType='ScrollViewer'><Setter Property='FocusVisualStyle' Value='{x:Null}'/></Style><Style TargetType='ContentControl'><Setter Property='FocusVisualStyle' Value='{x:Null}'/></Style><Style TargetType='Button'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Background' Value='#85232323'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='MinHeight' Value='38'/><Setter Property='FontSize' Value='14'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#99555555'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='Background' Value='#AA666666'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Background' Value='#AA555555'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='PrimaryAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
            <Setter Property='Foreground' Value='Black'/><Setter Property='Background' Value='#EEEEEE'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
                <Border x:Name='Surface' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'>
                    <ContentPresenter VerticalAlignment='Center' HorizontalAlignment='Center'/>
                </Border><ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='White'/></Trigger>
                    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='Background' Value='White'/></Trigger>
                    <Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Background' Value='#CCCCCC'/></Trigger>
                    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style x:Key='TrashAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
            <Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#BBBBBB'/>
            <Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='#FF6262'/></Trigger></Style.Triggers>
        </Style>
        <Style x:Key='InlineExpander' TargetType='Expander'>
            <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Expander'><StackPanel>
                <ToggleButton FocusVisualStyle='{x:Null}' IsChecked='{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent}}'>
                    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>
                        <Border x:Name='Surface' Background='#402D2D2D' CornerRadius='8' Padding='14,12'>
                            <DockPanel><ContentPresenter x:Name='Arrow' Content='›' TextElement.FontSize='22' TextElement.Foreground='White' DockPanel.Dock='Right'/><ContentPresenter Content='Outline' TextElement.Foreground='White' TextElement.FontSize='16' VerticalAlignment='Center'/></DockPanel>
                        </Border><ControlTemplate.Triggers>
                            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='Content' Value='⌄'/></Trigger>
                            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#70555555'/></Trigger>
                            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='Background' Value='#70555555'/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter x:Name='Body' Visibility='Collapsed'/>
            </StackPanel><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='Body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style TargetType='ProgressBar'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ProgressBar'><Grid x:Name='PART_Track' Background='Transparent'><Border Background='{TemplateBinding Background}' CornerRadius='6'/><Border x:Name='PART_Indicator' Background='{TemplateBinding Foreground}' CornerRadius='6' HorizontalAlignment='Left'/></Grid></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='TextAction' TargetType='Button'>
            <Setter Property='Foreground' Value='White'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='FontSize' Value='14'/>
            <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
                <Border Background='Transparent' Padding='{TemplateBinding Padding}'>
                    <TextBlock x:Name='Label' Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}'/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Label' Property='TextDecorations' Value='Underline'/></Trigger>
                    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Label' Property='TextDecorations' Value='Underline'/></Trigger>
                    <Trigger Property='IsPressed' Value='True'><Setter TargetName='Label' Property='Opacity' Value='.65'/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style TargetType='TextBox'><Setter Property='Background' Value='#242424'/><Setter Property='Foreground' Value='White'/><Setter Property='CaretBrush' Value='White'/><Setter Property='BorderBrush' Value='#AA555555'/><Setter Property='Padding' Value='10'/></Style>
        <Style TargetType='ComboBox'><Setter Property='Foreground' Value='White'/><Setter Property='Background' Value='{DynamicResource AdaptiveControlBrush}'/><Setter Property='Padding' Value='10'/><Setter Property='Margin' Value='0,5,0,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><Border Background='{TemplateBinding Background}' BorderBrush='#2EFFFFFF' BorderThickness='1' CornerRadius='10'/><Border x:Name='Glow' Background='#14FFFFFF' CornerRadius='10' Opacity='0' IsHitTestVisible='False'/><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='12,8,35,8' IsHitTestVisible='False' VerticalAlignment='Center'/><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'><ContentPresenter Content='⌄' TextElement.Foreground='#D0D0D0' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,2'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' PopupAnimation='Fade'><Border Background='{DynamicResource AdaptiveTooltipBrush}' BorderBrush='#505050' BorderThickness='1' CornerRadius='7' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='360'><ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Hidden'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><EventTrigger RoutedEvent='MouseEnter'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Glow' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:.1'/></Storyboard></BeginStoryboard></EventTrigger><EventTrigger RoutedEvent='MouseLeave'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Glow' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:.16'/></Storyboard></BeginStoryboard></EventTrigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='White'/><Setter Property='Padding' Value='12,9'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='B' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource AdaptiveSelectionBrush}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource AdaptiveSelectionBrush}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='CheckBox'><Setter Property='Foreground' Value='White'/></Style>
        <Style x:Key='ToggleStyle' TargetType='CheckBox'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Foreground' Value='White'/><Setter Property='Cursor' Value='Hand'/><Setter Property='MinHeight' Value='38'/><Setter Property='FontSize' Value='14'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><DockPanel LastChildFill='True'><Border x:Name='Track' DockPanel.Dock='Right' Width='34' Height='19' CornerRadius='10' Margin='16,0,0,0'><Border.Background><SolidColorBrush Color='#3C3C3C'/></Border.Background><Ellipse x:Name='Knob' Width='13' Height='13' HorizontalAlignment='Left' Margin='3,0,3,0'><Ellipse.Fill><SolidColorBrush Color='#C0C0C0'/></Ellipse.Fill><Ellipse.RenderTransform><TranslateTransform/></Ellipse.RenderTransform></Ellipse></Border><ContentPresenter VerticalAlignment='Center'/></DockPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='15' Duration='0:0:.17'><DoubleAnimation.EasingFunction><BackEase EasingMode='EaseOut' Amplitude='.5'/></DoubleAnimation.EasingFunction></DoubleAnimation><ColorAnimation Storyboard.TargetName='Track' Storyboard.TargetProperty='(Border.Background).(SolidColorBrush.Color)' To='White' Duration='0:0:.14'/><ColorAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(Shape.Fill).(SolidColorBrush.Color)' To='#171717' Duration='0:0:.14'/></Storyboard></BeginStoryboard></Trigger.EnterActions><Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='0' Duration='0:0:.17'><DoubleAnimation.EasingFunction><BackEase EasingMode='EaseOut' Amplitude='.5'/></DoubleAnimation.EasingFunction></DoubleAnimation><ColorAnimation Storyboard.TargetName='Track' Storyboard.TargetProperty='(Border.Background).(SolidColorBrush.Color)' To='#3C3C3C' Duration='0:0:.14'/><ColorAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(Shape.Fill).(SolidColorBrush.Color)' To='#C0C0C0' Duration='0:0:.14'/></Storyboard></BeginStoryboard></Trigger.ExitActions></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='ScrollBar'><Setter Property='Background' Value='#101010'/><Setter Property='Width' Value='9'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='{TemplateBinding Background}'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Opacity='0' Command='ScrollBar.PageUpCommand'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Margin='2' Background='#555555' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Opacity='0' Command='ScrollBar.PageDownCommand'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='Slider'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Height' Value='48'/><Setter Property='IsMoveToPointEnabled' Value='True'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid VerticalAlignment='Center' Height='40'><Border Height='12' Background='#353535' CornerRadius='6'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' BorderThickness='0' Height='12' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border x:Name='Fill' CornerRadius='6' Margin='0,0,-12,0'><Border.Background><LinearGradientBrush StartPoint='0,0' EndPoint='1,0'><GradientStop Color='#686868' Offset='0'/><GradientStop Color='White' Offset='1'/></LinearGradientBrush></Border.Background></Border><ControlTemplate.Triggers><Trigger Property='ActualWidth' Value='0'><Setter TargetName='Fill' Property='Visibility' Value='Hidden'/></Trigger></ControlTemplate.Triggers></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='30' Height='40'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse x:Name='Knob' Width='27' Height='27' Fill='White'/>
<ControlTemplate.Triggers>
    <DataTrigger Binding='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource AncestorType=Slider}}' Value='True'>
        <Setter TargetName='Knob' Property='Effect'>
            <Setter.Value><DropShadowEffect Color='White' ShadowDepth='0' BlurRadius='10' Opacity='.45'/></Setter.Value>
        </Setter>
    </DataTrigger>
</ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
        </ResourceDictionary>";
    }
    internal static class ChromeExtensions
    {
        public static void Set(this WindowChrome chrome,Window window)
        {
            WindowChrome.SetWindowChrome(window,chrome);
        }
    }
}








































