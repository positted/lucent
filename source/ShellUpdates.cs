using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Lucent
{
    // Telling you when a newer Lucent is out: a quiet check a little after start-up and twice a day, a notice in About and on Home, and
    // one tray notification per version. Nothing is downloaded or installed by Lucent itself; the notice opens the release page.
    internal sealed partial class Shell
    {
        UpdateInfo updateInfo;
        readonly DispatcherTimer updateTimer=new DispatcherTimer {Interval=TimeSpan.FromHours(12)};
        TextBlock updateStatus,updateNotes;
        Button updateOpen,updateCheck;
        string UpdateSource{get{return Updates.Source;}}
        internal UpdateInfo UpdateAvailable {get{return updateInfo;}}
        Grid updatePopup;bool updatePopupPending;
        internal bool UpdatePopupOpen {get{return updatePopup!=null;}}

        // Setup (when it updates Lucent) asks a running Lucent to close by raising this signal; Lucent then exits normally, like Close Lucent in the tray.
        void ListenForQuit()
        {
            try
            {
                var signal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\Lucent.Quit");
                var thread=new System.Threading.Thread(()=>
                {
                    while(signal.WaitOne())try{Dispatcher.BeginInvoke(new Action(ExitApp));}catch(Exception){return;}
                }){IsBackground=true,Name="Lucent quit signal"};
                thread.Start();
            }
            catch(Exception){}
        }
        // The first check waits, so starting Lucent is never slowed by the network.
        void StartUpdateChecks()
        {
            if(TestMode)return;
            ListenForQuit();
            Activated+=(s,e)=>ShowPendingUpdatePopup();
            var first=new DispatcherTimer {Interval=TimeSpan.FromSeconds(25)};
            first.Tick+=async(s,e)=>{first.Stop();await CheckForUpdates(false);};
            first.Start();
            updateTimer.Tick+=async(s,e)=>await CheckForUpdates(false);
            updateTimer.Start();
        }
        // manual is a click on "Check for updates": it always answers, in words. The quiet checks only speak up for a newer version.
        internal async Task CheckForUpdates(bool manual)
        {
            if(closing)return;
            if(!manual&&!preferences.CheckUpdates)return;
            if(string.IsNullOrWhiteSpace(UpdateSource))
            {
                ShowUpdateText(manual?"Updates are not set up yet.":null);
                return;
            }
            if(manual)ShowUpdateText("Checking…");
            UpdateInfo found=null;string problem=null;
            try{found=await Task.Run(()=>Updates.Check(UpdateSource));}
            catch(Exception error){problem=error.Message;}
            if(closing)return;
            if(problem!=null)
            {
                if(manual)ShowUpdateText("Could not check for updates: "+problem);
                return;
            }
            if(Updates.IsNewer(found,Updates.Current))
            {
                updateInfo=found;
                ShowUpdateText("Lucent "+Updates.Show(found.Latest)+" is available. You have "+Design.Version+".");
                UpdateHome();
                // A big window asks whether to update: once for each new version, and always after a click on "Check for updates".
                if(manual||preferences.PopupVersion!=found.Tag){preferences.PopupVersion=found.Tag;SaveSoon();ShowUpdatePopup();}
                // Tell the user once per version, wherever Lucent is: the status line and, from the tray, a notification.
                if(manual||preferences.NotifiedVersion!=found.Tag)
                {
                    preferences.NotifiedVersion=found.Tag;SaveSoon();
                    SetStatus("A new version of Lucent is out: "+Updates.Show(found.Latest));
                    if(!TestMode&&tray!=null)
                        try{tray.ShowBalloonTip(7000,"Lucent update available","Version "+Updates.Show(found.Latest)+" is out. Open Lucent, then Settings and About, to get it.",System.Windows.Forms.ToolTipIcon.Info);}catch(Exception){}
                }
            }
            else
            {
                updateInfo=null;
                ShowUpdateText(manual?"You have the latest version ("+Design.Version+").":null);
                UpdateHome();
            }
        }
        // ---- The update window: a small panel at the top right, like the Recently played one, that asks whether to update now. The app stays usable around it.
        internal void ShowUpdatePopup()
        {
            if(updateInfo==null||closing||updatePopup!=null)return;
            // Hidden in the tray or minimized: it is shown the next time the window is in front.
            if(!IsVisible||WindowState==WindowState.Minimized){updatePopupPending=true;return;}
            updatePopupPending=false;
            var info=updateInfo;
            var layer=new Grid {Background=null,Focusable=true,Opacity=0};
            var stack=new StackPanel();
            var head=new DockPanel {Margin=new Thickness(0,0,0,12)};
            var mark=Art.Logo(34);mark.Margin=new Thickness(0,0,13,0);mark.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(mark,Dock.Left);head.Children.Add(mark);
            var titles=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            titles.Children.Add(new TextBlock {Text="Update available",FontSize=18,FontWeight=FontWeights.Bold,Foreground=Brushes.White});
            titles.Children.Add(new TextBlock {Text="Lucent "+Updates.Show(info.Latest)+" is ready. You have "+Design.Version+".",FontSize=13,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0)});
            head.Children.Add(titles);stack.Children.Add(head);
            if(!string.IsNullOrWhiteSpace(info.Notes))
                stack.Children.Add(new Border {Background=new SolidColorBrush(Color.FromRgb(36,36,38)),CornerRadius=new CornerRadius(12),Padding=new Thickness(14,10,14,10),Margin=new Thickness(0,0,0,12),MaxHeight=110,
                    Child=new TextBlock {Text=info.Notes,FontSize=13,Foreground=Muted,TextWrapping=TextWrapping.Wrap}});
            var status=new TextBlock {FontSize=13,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10),Visibility=Visibility.Collapsed};
            var bar=new ProgressBar {Height=8,Minimum=0,Maximum=100,Margin=new Thickness(0,0,0,14),Visibility=Visibility.Collapsed};
            stack.Children.Add(status);stack.Children.Add(bar);
            stack.Children.Add(new TextBlock {Text="Lucent will close, then its setup opens. Your settings are kept.",FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var later=Action("Later",()=>HideUpdatePopup());
            var update=new Button {Content="Update now",Padding=new Thickness(14,10,14,10),Style=(Style)Resources["PrimaryAction"]};bool failed=false;
            later.Margin=new Thickness(0,0,10,0);update.Margin=new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(later,"Later");System.Windows.Automation.AutomationProperties.SetName(update,"Update now");
            buttons.Children.Add(later);buttons.Children.Add(update);stack.Children.Add(buttons);
            update.Click+=async(s,e)=>
            {
                if(failed){OpenUpdatePage();HideUpdatePopup();return;}
                update.IsEnabled=false;later.IsEnabled=false;status.Visibility=Visibility.Visible;
                if(!Updates.CanDownload(info.AssetUrl)){status.Text="Opening the download page…";OpenUpdatePage();HideUpdatePopup();return;}
                bar.Visibility=Visibility.Visible;status.Text="Downloading…";
                string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Lucent-Setup-"+Updates.Show(info.Latest)+".exe");
                string problem=null;
                try
                {
                    await Task.Run(()=>Updates.Save(info,path,(done,total)=>Dispatcher.BeginInvoke(new Action(()=>
                    {
                        if(total>0){bar.Value=Math.Min(100,done*100.0/total);status.Text="Downloading… "+Math.Min(100,(int)(done*100/total))+"%";}
                    }))));
                }
                catch(Exception error){problem=error.Message;}
                if(updatePopup==null)return;
                if(problem!=null)
                {
                    status.Text="The download did not work: "+problem;bar.Visibility=Visibility.Collapsed;
                    failed=true;update.Content="Open download page";update.IsEnabled=true;later.IsEnabled=true;
                    return;
                }
                status.Text="Opening setup…";
                if(TestMode){lastLaunch=path;HideUpdatePopup();return;}
                try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}
                catch(Exception error){status.Text="Setup could not be opened: "+error.Message;update.IsEnabled=true;later.IsEnabled=true;return;}
                ExitApp();
            };
            var card=new Border {Width=380,Background=new SolidColorBrush(Color.FromRgb(28,28,30)),BorderBrush=new SolidColorBrush(Color.FromArgb(46,255,255,255)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(20),
                HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,72,16,0),Child=stack,RenderTransformOrigin=new Point(1,0),RenderTransform=new ScaleTransform(.94,.94),
                Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=30,ShadowDepth=6,Opacity=.55,Color=Colors.Black}};
            layer.Children.Add(card);
            layer.KeyDown+=(s,e)=>{if(e.Key==Key.Escape&&later.IsEnabled){HideUpdatePopup();e.Handled=true;}};
            layer.MouseDown+=(s,e)=>e.Handled=true;
            Panel.SetZIndex(layer,40);
            windowSurface.Children.Add(layer);
            updatePopup=layer;
            Design.Animate(layer,OpacityProperty,0,1,180);
            var scale=(ScaleTransform)card.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.94,1,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.94,1,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            layer.Focus();
        }
        internal void HideUpdatePopup()
        {
            var layer=updatePopup;if(layer==null)return;
            updatePopup=null;
            windowSurface.Children.Remove(layer);
        }
        // A popup that had to wait (the window was in the tray) appears as soon as the window is back.
        void ShowPendingUpdatePopup(){if(updatePopupPending)ShowUpdatePopup();}        void ShowUpdateText(string text)
        {
            if(updateStatus==null)return;
            if(text!=null)updateStatus.Text=text;
            else if(updateInfo==null)updateStatus.Text=string.IsNullOrWhiteSpace(UpdateSource)?"Updates are not set up yet.":"Lucent checks for updates by itself.";
            bool newer=updateInfo!=null;
            updateOpen.Visibility=newer&&Updates.CanOpenRelease(updateInfo.Url)?Visibility.Visible:Visibility.Collapsed;
            if(newer)updateOpen.Content="Get "+Updates.Show(updateInfo.Latest);
            updateNotes.Text=newer?updateInfo.Notes:"";
            updateNotes.Visibility=newer&&updateInfo.Notes.Length>0?Visibility.Visible:Visibility.Collapsed;
        }
        void OpenUpdatePage()
        {
            if(updateInfo==null||!Updates.CanOpenRelease(updateInfo.Url))return;
            if(!TestMode)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(updateInfo.Url){UseShellExecute=true});
            else lastLaunch=updateInfo.Url;
        }
        // The Updates block of the About card.
        FrameworkElement UpdatesBlock()
        {
            var block=new StackPanel {Margin=new Thickness(0,0,0,22)};
            var title=new TextBlock {Text="Updates",FontSize=15,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,6)};
            block.Children.Add(title);
            updateStatus=new TextBlock {FontSize=13,TextWrapping=TextWrapping.Wrap,Foreground=Muted};
            block.Children.Add(updateStatus);
            updateNotes=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Muted,Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed};
            block.Children.Add(updateNotes);
            var buttons=new WrapPanel {Margin=new Thickness(0,10,0,0)};
            updateCheck=Action("Check for updates",async()=>await CheckForUpdates(true));
            updateOpen=Action("Get update",OpenUpdatePage);
            updateOpen.Style=(Style)Resources["PrimaryAction"];updateOpen.Visibility=Visibility.Collapsed;
            buttons.Children.Add(updateCheck);buttons.Children.Add(updateOpen);
            block.Children.Add(buttons);
            block.Children.Add(Toggle("Check for updates automatically",preferences.CheckUpdates,v=>{preferences.CheckUpdates=v;SaveSoon();}));
            ShowUpdateText(null);
            return block;
        }
    }
}
