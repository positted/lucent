using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lucent
{
    internal sealed partial class Shell
    {
        readonly Grid windowSurface=new Grid();
        readonly AmbientBackdrop ambientBackdrop=new AmbientBackdrop();
        Border gifShade;
        readonly DispatcherTimer paletteTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        readonly MediaSession paletteSession=new MediaSession();
        bool readingPalette;
        string paletteTrack;
        byte[] paletteArtwork;
        BitmapSource paletteBitmap;
        Color mediaAccent=Color.FromRgb(46,46,48);
        Button settingsRailButton;
        Grid settingsLayer;
        Border settingsCard;
        Border settingsShade;
        Button settingsOverlayGear,playerDockButton,bigPlayerButton;
        Border settingsPill;
        ScrollViewer contentScroll,settingsScroll;
        // A blurred copy of the page at exactly its own size, over black so the blur's soft edges never show the sharp page.
        readonly System.Windows.Shapes.Rectangle settingsBackdrop=new System.Windows.Shapes.Rectangle{IsHitTestVisible=false,Margin=new Thickness(-24),Effect=new BlurEffect{Radius=12,RenderingBias=RenderingBias.Performance},CacheMode=new BitmapCache()};
        readonly Grid settingsBackdropLayer=new Grid{IsHitTestVisible=false,Visibility=Visibility.Collapsed,Opacity=0,Background=Brushes.Black,ClipToBounds=true};
        // One value fades every settings surface together; nothing moves or resizes, and
        // an interrupted transition simply reverses from where it is.
        public static readonly DependencyProperty SettingsMotionProperty=DependencyProperty.Register("SettingsMotion",typeof(double),typeof(Shell),new PropertyMetadata(0d,(d,e)=>((Shell)d).ApplySettingsMotion((double)e.NewValue)));
        internal static double SpringOut(double t)
        {
            double u=t-1;
            return 1+2.9*u*u*u+1.9*u*u;
        }
        void ApplySettingsMotion(double p)
        {
            if(settingsCard==null)return;
            settingsCard.Opacity=p;settingsShade.Opacity=p;settingsBackdropLayer.Opacity=p;
            // The window itself eases in and out: a little smaller and lower at first, settling into place. The gear is a separate element and never moves.
            if(settingsCard.RenderTransform is TransformGroup)
            {
                var group=(TransformGroup)settingsCard.RenderTransform;
                var scale=(ScaleTransform)group.Children[0];var lift=(TranslateTransform)group.Children[1];
                // The window grows out of the settings gear and shrinks back into it (its origin is aimed at the gear before each motion).
                double eased=settingsOpen?SpringOut(p):p;
                scale.ScaleX=scale.ScaleY=Math.Max(.05,.05+.95*eased);
                lift.Y=0;
            }
        }
        readonly Dictionary<string,SectionPanel> settingsSections=new Dictionary<string,SectionPanel>();
        readonly Dictionary<string,Button> settingsCategoryButtons=new Dictionary<string,Button>();
        ContentControl settingsContent;
        TextBlock settingsHeading,settingsDescription;
        string settingsCategory="Appearance";
        bool settingsOpen;
        int settingsTransition;
        public bool SettingsOpen {get{return settingsOpen;}}
        // The names people see. The keys stay stable, since saved state, shortcuts and the tray refer to them.
        internal static string TabTitle(string key)
        {
            switch(key)
            {
                case "Video":return "Color";
                case "Resolution":return "Display";
                case "Profiles":return "Profiles";
                case "Game Boost":return "Boost";
                case "Visual tools":return "Vision";
                default:return key;
            }
        }
        void BuildModernNav()
        {
            // The pages are a slim strip of tabs along the top: the logo, one tab per page, and the settings gear at the end.
            var items=new StackPanel {Orientation=Orientation.Horizontal};
            var logo=new Border {Width=34,Height=28,Margin=new Thickness(2,0,4,0),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=Art.Logo(22),ToolTip="Lucent"};
            logo.MouseLeftButtonUp+=(s,e)=>ShowPage("Home");
            items.Children.Add(logo);
            foreach(string name in new[]{"Home","Video","Resolution","Crosshair","Profiles","Visual tools","Game Boost"})items.Children.Add(ModernNavButton(name));
            items.Children.Add(new Border {Width=1,Height=16,Background=new SolidColorBrush(Color.FromArgb(60,255,255,255)),Margin=new Thickness(5,0,5,0),IsHitTestVisible=false});
            settingsRailButton=ModernNavButton("Settings");
            items.Children.Add(settingsRailButton);
            tabStrip=new Border {Background=new SolidColorBrush(Color.FromArgb(178,16,16,18)),CornerRadius=new CornerRadius(12),Padding=new Thickness(3,3,4,3),Height=34,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,Child=items};
        }
        Border tabStrip;
        readonly List<TextBlock> tabLabels=new List<TextBlock>();
        // A tab: the page's icon and name (the settings gear is just the icon). The page in view is lit.
        Button ModernNavButton(string title)
        {
            bool gear=title=="Settings";
            var icon=Design.RailIcon(title,gear?18:16);
            UIElement content=icon;
            if(!gear)
            {
                icon.Margin=new Thickness(0,0,7,0);
                var label=new TextBlock {Text=TabTitle(title),FontSize=12.5,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
                tabLabels.Add(label);
                var row=new StackPanel {Orientation=Orientation.Horizontal};
                row.Children.Add(icon);row.Children.Add(label);
                content=row;
            }
            content.Opacity=.62;
            var button=new Button {Content=content,Tag=title,Style=(Style)Resources["TabAction"],Margin=new Thickness(1,0,1,0),ToolTip=null};
            if(gear){button.Padding=new Thickness(8,0,8,0);button.ToolTip=RailTooltip(TabTitle(title),button,false,null,0,false);}
            System.Windows.Automation.AutomationProperties.SetName(button,TabTitle(title));
            button.MouseEnter+=(s,e)=>Design.Animate(content,OpacityProperty,content.Opacity,1,90);
            button.MouseLeave+=(s,e)=>Design.Animate(content,OpacityProperty,content.Opacity,(string)button.Tag==page||title=="Settings"&&settingsOpen?1:.62,180);
            button.Click+=(s,e)=>{if(title=="Music")MediaPopup();else ShowPage(title);};
            navigationButtons.Add(button);return button;
        }
        // Narrow windows show only the icons, so the tabs never run into the window buttons.
        void FitTabs(double available)
        {
            if(tabStrip==null||tabLabels.Count==0)return;
            foreach(var label in tabLabels)label.Visibility=Visibility.Visible;
            foreach(var label in tabLabels)((StackPanel)label.Parent).Children[0].SetValue(MarginProperty,new Thickness(0,0,7,0));
            tabStrip.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            bool compact=tabStrip.DesiredSize.Width>available;
            foreach(var label in tabLabels)
            {
                label.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
                ((StackPanel)label.Parent).Children[0].SetValue(MarginProperty,new Thickness(compact?0:0,0,compact?0:7,0));
            }
        }
        ToolTip RailTooltip(string title,Button target,bool mirrored=false,Brush surface=null,double clearance=0,bool above=false)
        {
            // Coming back with alt+tab puts the keyboard focus on a button again, and Windows would show its tip as if the pointer were there.
            ToolTipService.SetShowsToolTipOnKeyboardFocus(target,false);
            var slide=new TranslateTransform();
            var tip=new ToolTip{Content=title,PlacementTarget=target,Placement=System.Windows.Controls.Primitives.PlacementMode.Custom,FontFamily=Design.Font,FontSize=13,FontWeight=FontWeights.SemiBold,RenderTransform=slide,Background=surface??tooltipSurface};
            // A control at the window's right edge mirrors the rail placement: beside it, centered, same gap.
            tip.CustomPopupPlacementCallback=(size,anchor,offset)=>above
                ?new[]{new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point((anchor.Width-size.Width)/2,-size.Height-8),System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)}
                :new[]{new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(mirrored?-size.Width-8-clearance:anchor.Width+8,(anchor.Height-size.Height)/2),System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)};
            tip.Opened+=(s,e)=>
            {
                Design.Animate(tip,OpacityProperty,0,1,160);
                if(above)Design.Animate(slide,TranslateTransform.YProperty,4,0,180);
                else Design.Animate(slide,TranslateTransform.XProperty,mirrored?4:-4,0,180);
            };
            return tip;
        }
        void UpdateRailSelection()
        {
            foreach(var button in navigationButtons)
            {
                bool active=(string)button.Tag==page||((string)button.Tag=="Settings"&&settingsOpen);
                button.Background=active&&(string)button.Tag!="Settings"?new SolidColorBrush(Color.FromArgb(40,255,255,255)):(Brush)Brushes.Transparent;
                var icon=button.Content as UIElement;if(icon!=null){icon.BeginAnimation(OpacityProperty,null);icon.Opacity=active?1:.62;}
            }
        }
        void BuildModernBody()
        {
            var body=new DockPanel {Margin=new Thickness(16,8,16,14)};Grid.SetColumn(body,1);workspace.Children.Add(body);
            var top=new DockPanel {Height=42,LastChildFill=true,Margin=new Thickness(0,0,0,4)};
            DockPanel.SetDock(top,Dock.Top);
            body.Children.Add(top);
            // The tab strip sits on the left of the top row; the window buttons sit on the right.
            DockPanel.SetDock(tabStrip,Dock.Left);top.Children.Add(tabStrip);
            top.SizeChanged+=(s,e)=>FitTabs(Math.Max(120,e.NewSize.Width-150));
            var group=new StackPanel {Orientation=Orientation.Horizontal};
            var chrome=new Border {Background=new SolidColorBrush(Color.FromArgb(178,16,16,18)),CornerRadius=new CornerRadius(11),Padding=new Thickness(4,2,4,2),Child=group,Height=34,VerticalAlignment=VerticalAlignment.Top};
            DockPanel.SetDock(chrome,Dock.Right);top.Children.Add(chrome);
            windowButtons=chrome;chrome.Loaded+=(s,e)=>UpdatePageClip();chrome.SizeChanged+=(s,e)=>UpdatePageClip();
            AddWindowButtons(group);
            var drag=new Border {Background=Brushes.Transparent};
            drag.MouseLeftButtonDown+=(s,e)=>
            {
                // A double click on the top does nothing (it used to maximize the window); the maximize button is still there.
                if(e.ClickCount==2)return;
                else DragMove();
            };
            top.Children.Add(drag);
            // The page is under this strip now; the wheel over it still scrolls the page.
            drag.MouseWheel+=(s,e)=>{if(contentScroll!=null&&!settingsOpen&&!bigPlayerOpen){contentScroll.ScrollToVerticalOffset(contentScroll.VerticalOffset-e.Delta);e.Handled=true;}};
            status.Foreground=Muted;status.FontSize=11;status.TextWrapping=TextWrapping.Wrap;
            // The player buttons are one pill that never moves. Its background fades once the bar is up, because the bar is behind them then.
            playerDock=new Border {Height=34,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,16+PlayerBar.BarRight,24+PlayerBar.BarBottom+7),Visibility=Visibility.Collapsed,RenderTransform=new TranslateTransform()};
            Panel.SetZIndex(playerDock,9);
            Design.SetStill(playerDock,true);
            root.Children.Add(playerDock);
            playerArrow=PlayerDockAction("PlayerBar","Show player bar","Toggle player bar",TogglePlayerBar,()=>playerBarShown,0);
            // The toggle shows a window with an empty strip along its bottom, which fills in while the bar is up.
            barOff=(Viewbox)playerArrow.Content;
            barOn=Design.Icon("PlayerBarOn",18);
            barOn.Opacity=0;
            playerArrow.Content=null;
            var barIcons=new Grid();
            barIcons.Children.Add(barOff);
            barIcons.Children.Add(barOn);
            playerArrow.Content=barIcons;
            playerDockButton=PlayerDockAction("OpenPlayer","Open mini player","Toggle mini player",MediaPopup,()=>mediaPlayer!=null,32);
            bigPlayerButton=PlayerDockAction("BigPlayer","Open player","Toggle player",ToggleBigPlayer,()=>bigPlayerOpen||bigPlayerWindow!=null,64);
            var dockButtons=new StackPanel {Orientation=Orientation.Horizontal};
            dockButtons.Children.Add(playerArrow);
            dockButtons.Children.Add(playerDockButton);
            dockButtons.Children.Add(bigPlayerButton);
            // A divider, then the list of what played recently, as the last of the icons.
            dockButtons.Children.Add(new Border {Width=1,Height=18,Background=new SolidColorBrush(Color.FromArgb(70,255,255,255)),Margin=new Thickness(5,0,5,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
            historyPanel=new HistoryPanel {Margin=new Thickness(0,58,16,PlayerBar.BarHeight+PlayerBar.BarBottom+37)};
            Panel.SetZIndex(historyPanel,9);
            root.Children.Add(historyPanel);
            historyButton=PlayerDockAction("Queue","Recently played","Toggle recently played",()=>historyPanel.Set(!historyPanel.IsOpen),()=>historyPanel.IsOpen,96);
            historyPanel.OpenChanged+=open=>ShowPlayerOpen(historyButton,open);
            dockButtons.Children.Add(historyButton);
            playerDock.Child=new Border {Background=dockPillBrush,CornerRadius=new CornerRadius(10),Padding=new Thickness(5,3,5,3),Child=dockButtons};
            // The big player covers the whole window, the sidebar included, with its own window buttons.
            var overlayButtons=new StackPanel {Orientation=Orientation.Horizontal};
            var popOut=WindowAction("PopOut",PopOutBigPlayer);
            System.Windows.Automation.AutomationProperties.SetName(popOut,"Pop out player");
            popOut.ToolTip="Pop out player";
            overlayButtons.Children.Add(popOut);
            AddWindowButtons(overlayButtons);
            var overlayChrome=new Border {Background=new SolidColorBrush(Veil(12)),CornerRadius=new CornerRadius(11),Padding=new Thickness(4,2,4,2),Child=overlayButtons,Height=34,Margin=new Thickness(0,8,16,0)};
            var closePlayer=WindowAction("Collapse",()=>SetBigPlayerOpen(false));
            System.Windows.Automation.AutomationProperties.SetName(closePlayer,"Close player");
            closePlayer.ToolTip="Close player";
            // Looking up lyrics sends the song to a public lyrics service, so it stays off until switched on, and the choice is remembered.
            BigPlayerView.LyricsEnabled=()=>preferences.ShowLyrics;
            ArtistPictures.Enabled=()=>preferences.ArtistPictures;
            BigPlayerView.PerformanceMode=()=>preferences.PerformanceMode;
            BigPlayerView.LyricsChanged=on=>{preferences.ShowLyrics=on;saveTimer.Stop();saveTimer.Start();};
            bigPlayerView=new BigPlayerView(this,TestMode,closePlayer,overlayChrome,true);
            // The cover of the player bar, for the player to grow out of and shrink into. Where the bar will be once it is up: it is hidden while the player is open.
            bigPlayerView.CoverTarget=()=>
            {
                if(!playerBarOpen||!playerDockShown||root.ActualHeight<=0)return (Rect?)null;
                return new Rect(16+PlayerBar.BarLeft+14,root.ActualHeight-14-PlayerBar.BarBottom-PlayerBar.BarHeight+14,40,40);
            };
            Panel.SetZIndex(bigPlayerView,8);
            root.Children.Add(bigPlayerView);
            // The page and its fade-out copy share one frame that gives way to the player bar.
            pageHost=new Grid();
            pageLayer=new Grid();
            pageHost.Children.Add(pageLayer);
            playerBar=new PlayerBar(this,TestMode){OpenBig=()=>SetBigPlayerOpen(true)};
            playerBar.Core.hold=HoldForColors;
            Design.Instant=preferences.PerformanceMode;
            playerBar.Backdrop.On=!preferences.PerformanceMode;
            playerBar.SetGlass(GlassOn);
            playerBar.SetBlurSources(ambientBackdrop,flowHost,pageHost);
            pageHost.SizeChanged+=(s,e)=>{UpdatePageClip();playerBar.FitBlur();};
            playerBar.LayoutUpdated+=(s,e)=>playerBar.FitBlur();
            pageArea=new Grid();
            pageArea.Children.Add(pageHost);
            pageArea.Children.Add(playerBar);
            // The page is laid out from the top of the window, behind the strip with the window buttons (which is drawn above it and still drags the window).
            pageArea.Margin=new Thickness(0,-PageTop,0,0);
            body.Children.Add(pageArea);
            Panel.SetZIndex(top,10);
            if(preferences.PlayerBarOpen)playerBarOpen=true;
        }
        // Minimize, maximize and close, for the title area and for the full-window player.
        void AddWindowButtons(Panel into)
        {
            var minimize=WindowAction("Minimize",MinimizeWithMotion);
            var maximize=WindowAction("Maximize",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
            var close=WindowAction("Close",Close);
            into.Children.Add(minimize);into.Children.Add(maximize);into.Children.Add(close);
            StateChanged+=(s,e)=>
            {
                var icon=Design.ChromeIcon(WindowState==WindowState.Maximized?"Restore":"Maximize");
                icon.Opacity=WindowInk;
                maximize.Content=icon;
            };
        }
        const double WindowInk=Design.WindowInk;
        Border playerDock;
        BigPlayerView bigPlayerView;
        PlayerBar playerBar;
        Button playerArrow,historyButton;
        HistoryPanel historyPanel;
        Viewbox barOff,barOn;
        readonly SolidColorBrush dockPillBrush=new SolidColorBrush(Color.FromArgb(22,255,255,255));
        bool playerDockShown,bigPlayerOpen,playerBarOpen,playerBarShown;
        DateTime lastMediaSeen=DateTime.MinValue;
        // A tooltip opens to the left, so a button further right clears its neighbor by tipClearance.
        // The look of a button by the bottom right: nothing but a soft round highlight behind its icon while the pointer is over it.
        static ControlTemplate DockTemplate()
        {
            var template=new ControlTemplate(typeof(Button));
            var face=new FrameworkElementFactory(typeof(Border),"Face");
            face.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            face.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));
            var content=new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            face.AppendChild(content);
            template.VisualTree=face;
            var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};
            hover.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(38,255,255,255)),"Face"));
            template.Triggers.Add(hover);
            return template;
        }
        Button PlayerDockAction(string icon,string tip,string name,Action toggle,Func<bool> isOpen,double tipClearance)
        {
            var button=new Button{Content=Design.Icon(icon,18),Style=(Style)Resources["RailAction"],Template=DockTemplate(),Width=32,Height=28,Opacity=.65};
            button.ToolTip=RailTooltip(tip,button,true,null,tipClearance,true);   // the title appears above the icon
            ToolTipService.SetInitialShowDelay(button,500);
            ToolTipService.SetBetweenShowDelay(button,500);
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            button.Click+=(s,e)=>toggle();
            // The icon is bright and the highlight shows while the pointer is over it; nothing moves or eases.
            button.MouseEnter+=(s,e)=>{button.BeginAnimation(OpacityProperty,null);button.Opacity=1;};
            button.MouseLeave+=(s,e)=>{button.BeginAnimation(OpacityProperty,null);button.Opacity=isOpen()?1:.65;};
            return button;
        }        // The player buttons only appear while there is media to control. A short grace period
        // and any open player keep them from flickering away between tracks.
        internal void SetMediaAvailable(bool available)
        {
            if(available)lastMediaSeen=DateTime.UtcNow;
            bool show=available||mediaPlayer!=null||bigPlayerOpen||bigPlayerWindow!=null||DateTime.UtcNow-lastMediaSeen<TimeSpan.FromSeconds(2);
            if(show==playerDockShown)return;
            playerDockShown=show;
            ApplyPlayerBar();
            var slide=(TranslateTransform)playerDock.RenderTransform;
            if(show)
            {
                playerDock.Visibility=Visibility.Visible;
                Design.Animate(playerDock,OpacityProperty,playerDock.Opacity==1?0:playerDock.Opacity,1,200);
                slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(14,0,TimeSpan.FromMilliseconds(280)){EasingFunction=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.7}});
                return;
            }
            var fade=new DoubleAnimation(playerDock.Opacity,0,TimeSpan.FromMilliseconds(170));
            fade.Completed+=(s,e)=>{if(!playerDockShown)playerDock.Visibility=Visibility.Collapsed;};
            playerDock.BeginAnimation(OpacityProperty,fade);
            slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,10,TimeSpan.FromMilliseconds(170)));
        }
        Button WindowAction(string name,Action action){return Design.WindowButton((Style)Resources["ChromeAction"],name,action);}
        // Only one player is on screen at a time: opening one closes the others, and the bar steps aside until they are gone.
        bool PlayerElsewhere {get{return bigPlayerOpen||bigPlayerWindow!=null||mediaPlayer!=null;}}
        void CloseOtherPlayers(string keep)
        {
            if(keep!="big")SetBigPlayerOpen(false);
            if(keep!="window"&&bigPlayerWindow!=null)bigPlayerWindow.Close();
            if(keep!="mini"&&mediaPlayer!=null)mediaPlayer.Close();
        }
        void TogglePlayerBar()
        {
            if(playerBarShown){SetPlayerBarOpen(false);return;}
            CloseOtherPlayers("bar");
            SetPlayerBarOpen(true);
            ApplyPlayerBar();
        }
        // The bar along the bottom shows only while media is detected, only if it was switched on, and only when no other player is up.
        void SetPlayerBarOpen(bool open)
        {
            if(open==playerBarOpen)return;
            playerBarOpen=open;
            preferences.PlayerBarOpen=open;
            saveTimer.Stop();saveTimer.Start();
            ApplyPlayerBar();
        }
        // With the bar up, the page stops at the bar's top edge: below it is the player bar.
        // The window buttons are drawn above the page (which runs up to the top edge behind them); their pill is dark enough that what scrolls under it does not show through.
        Border windowButtons;
        void UpdatePageClip()
        {
            if(pageHost==null||pageHost.ActualHeight<=0||pageHost.ActualWidth<=0)return;
            double width=pageHost.ActualWidth,height=pageHost.ActualHeight;
            // With the glass the page runs on underneath the bar, so there is something behind it to see through (its own end still keeps clear of the bar: see
            // pageInset). Without the glass the page ends where the bar begins.
            double bottom=(!GlassOn&&playerBarShown)?Math.Max(0,height-(PlayerBar.BarHeight+PlayerBar.BarBottom)):height;
            if(bottom>=height){pageHost.Clip=null;return;}
            Geometry shape=new RectangleGeometry(new Rect(0,0,width,bottom));
            if(shape.CanFreeze)shape.Freeze();
            pageHost.Clip=shape;
        }        internal void ApplyPlayerBar()
        {
            bool show=playerBarOpen&&playerDockShown&&!PlayerElsewhere;
            if(show==playerBarShown)return;
            playerBarShown=show;
            ShowPlayerOpen(playerArrow,show);
            ((ToolTip)playerArrow.ToolTip).Content=show?"Hide player bar":"Show player bar";
            Design.Animate(barOn,OpacityProperty,barOn.Opacity,show?1:0,200);
            Design.Animate(barOff,OpacityProperty,barOff.Opacity,show?0:1,200);
            playerBar.SetOpen(show);
            UpdatePageClip();
            // The page makes room above the bar, and the buttons' pill fades, since the bar is behind them now.
            // The page runs on underneath the bar (the bar is glass), so what scrolls past shows through it; the page's end keeps clear of the bar.
            pageInset=show?PlayerBar.PageInset:0;
            // Only the page in view glides to its new room; the others (hidden, but kept laid out) are simply given it, so they are not laid out again at every step.
            foreach(var holder in pageHolders.Values)
            {
                if(ReferenceEquals(holder,content)&&!Design.Instant)holder.BeginAnimation(MarginProperty,new ThicknessAnimation(new Thickness(0,PageTop,0,pageInset),TimeSpan.FromMilliseconds(show?260:200)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
                else{holder.BeginAnimation(MarginProperty,null);holder.Margin=new Thickness(0,PageTop,0,pageInset);}
            }
            dockPillBrush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Color.FromArgb(show?(byte)0:(byte)22,255,255,255),TimeSpan.FromMilliseconds(220)));
        }        // Centering on the cursor's monitor would hang the window off a smaller or portrait display.
        void AvoidTooSmallScreen()
        {
            var area=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            if(area.Width>=Width&&area.Height>=Height)return;
            var main=SystemParameters.WorkArea;
            WindowStartupLocation=WindowStartupLocation.Manual;
            Left=main.Left+Math.Max(0,(main.Width-Width)/2);
            Top=main.Top+Math.Max(0,(main.Height-Height)/2);
        }
        void InitializeAdaptiveTheme()
        {
            Design.EnableSliderJump();
            InstallWindowMotion();
            root.Children.Insert(0,ambientBackdrop);Panel.SetZIndex(ambientBackdrop,0);
            // Moving colors sit just above the still ones, under everything else, with a little shade so text stays easy to read.
            flowHost.Children.Add(flowBackdrop);
            root.Children.Insert(1,flowHost);Panel.SetZIndex(flowHost,1);
            StateChanged+=(s,e)=>flowBackdrop.Hold(WindowState==WindowState.Minimized);
            // While the window is being dragged larger or smaller the colors rest, so every frame goes to resizing.
            var resizeSettle=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(220)};
            resizeSettle.Tick+=(s,e)=>{resizeSettle.Stop();flowBackdrop.Hold(WindowState==WindowState.Minimized);WakePages();};
            SizeChanged+=(s,e)=>{flowBackdrop.Hold(true);resizeSettle.Stop();resizeSettle.Start();if(e.WidthChanged||e.HeightChanged)RestPages();};
            gifShade=new Border {IsHitTestVisible=false,Visibility=Visibility.Collapsed,Background=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Color.FromArgb(50,0,0,0),0),new GradientStop(Color.FromArgb(155,0,0,0),1)},new Point(0,0),new Point(0,1))};
            Panel.SetZIndex(gifShade,2);
            root.Children.Add(gifShade);
            paletteTimer.Tick+=async(s,e)=>await ReadAmbientMedia();
            Loaded+=(s,e)=>{if(!TestMode)paletteTimer.Start();};
            SizeChanged+=(s,e)=>SizeSettingsPanel();
            KeyDown+=(s,e)=>
            {
                if(e.Key==Key.Escape&&settingsOpen&&!e.Handled)
                {
                    SetSettingsOpen(false);
                    e.Handled=true;
                }
                else if(e.Key==Key.Escape&&zoomed&&!e.Handled)
                {
                    // A way out that needs no shortcut: Lucent keeps the keyboard even while the zoom is up.
                    zoomed=false;zoomHoldActive=false;zoomFromButton=false;
                    UpdateEffects();
                    e.Handled=true;
                }
                else if(e.Key==Key.Escape&&bigPlayerOpen&&!e.Handled)
                {
                    SetBigPlayerOpen(false);
                    e.Handled=true;
                }
                else if(!e.Handled)ForwardScrollKey(e);
            };
        }
        // Scrollbars are hidden and the viewers are not focus stops, so keys no
        // control consumed still have to reach the visible page.
        void ForwardScrollKey(KeyEventArgs e)
        {
            var scroll=settingsOpen?settingsScroll:contentScroll;if(scroll==null||e.KeyboardDevice.Modifiers!=ModifierKeys.None)return;
            var focused=Keyboard.FocusedElement;bool unfocused=focused==null||ReferenceEquals(focused,this);
            switch(e.Key)
            {
                case Key.PageUp:scroll.PageUp();break;
                case Key.PageDown:scroll.PageDown();break;
                case Key.Home:scroll.ScrollToTop();break;
                case Key.End:scroll.ScrollToEnd();break;
                case Key.Up:if(!unfocused)return;scroll.LineUp();break;
                case Key.Down:if(!unfocused)return;scroll.LineDown();break;
                default:return;
            }
            e.Handled=true;
        }
        // How long nothing has been playing. After a few seconds of it every player (mini, big, popped out) is closed: there is nothing for them to show.
        DateTime noMediaSince;
        const double NoMediaSeconds=4;
        void CloseWhenNoMedia(bool available)
        {
            if(available){noMediaSince=default(DateTime);return;}
            if(noMediaSince==default(DateTime))noMediaSince=DateTime.UtcNow;
            if(TestMode||!PlayerElsewhere||(DateTime.UtcNow-noMediaSince).TotalSeconds<NoMediaSeconds)return;
            CloseOtherPlayers("none");
        }
        async Task ReadAmbientMedia()
        {
            // With the window hidden or minimized the app does not draw anything for the music, but a player window of its own (mini, popped out) still needs to know when the music is gone.
            bool hidden=!IsVisible||WindowState==WindowState.Minimized;
            if(readingPalette||closing||(hidden&&mediaPlayer==null&&bigPlayerWindow==null))return;
            readingPalette=true;
            try
            {
                var track=await paletteSession.Read();
                if(closing)return;
                CloseWhenNoMedia(track!=null);
                if(hidden)return;
                SetMediaAvailable(track!=null);PlayHistory.Add(track);
                ambientTrack=track;
                UpdateFlowBackdrop();
                if(customBackgroundLoaded&&preferences.Artwork)
                {
                    UpdateHome();
                    animatedBackground.RefreshAccent();
                    UpdateAmbientColor();
                    return;
                }
                PrepareTrackColors(track);
                // The Now playing card shows the new song with the new colors, not before them.
                colorSync.AfterFlush(UpdateHome);
            }
            catch{ }finally{readingPalette=false;}
        }
        // Coming back from the tray or the taskbar: nothing was read while the window was away, so the song may have changed. It is read once now (quickly, or not
        // at all if that takes too long), and when it is another song its colors are put in place without a fade, before the window shows.
        bool snapColors;
        async Task RefreshColorsBeforeShowing()
        {
            if(readingPalette||closing)return;
            readingPalette=true;
            try
            {
                var read=paletteSession.Read();
                if(await Task.WhenAny(read,Task.Delay(120))!=read||closing)return;
                var track=read.Result;
                string key=track==null?"":track.Source+"|"+track.Title+"|"+track.Artist;
                if(key==paletteTrack&&MediaSession.SameArtwork(paletteArtwork,track==null?null:track.Artwork))return;
                if(customBackgroundLoaded&&preferences.Artwork)return;
                snapColors=true;Design.Instant=true;
                SetMediaAvailable(track!=null);ambientTrack=track;
                PrepareTrackColors(track);UpdateHome();
                // The pictures of the new colors are made off the UI thread; they are in place a moment later.
                await Task.Delay(140);
            }
            catch(Exception){}
            finally{snapColors=false;Design.Instant=preferences.PerformanceMode;readingPalette=false;}
        }
        // Works out the colors of a song's cover (once per song) and starts the change to them.
        void PrepareTrackColors(MediaTrack track)
        {
            string key=track==null?"":track.Source+"|"+track.Title+"|"+track.Artist;
            if(key==paletteTrack&&MediaSession.SameArtwork(paletteArtwork,track==null?null:track.Artwork))return;
            paletteTrack=key;paletteArtwork=track==null?null:track.Artwork;paletteBitmap=null;mediaAccent=Color.FromRgb(46,46,48);
            if(paletteArtwork!=null)using(var stream=new MemoryStream(paletteArtwork))
            {
                var bitmap=new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption=BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth=128;
                bitmap.StreamSource=stream;
                bitmap.EndInit();
                bitmap.Freeze();
                paletteBitmap=bitmap;
                mediaAccent=Design.Palette(PaletteField);
            }
            UpdateAmbientColor();
        }
        // The bar asks before it shows a new song: the song's colors begin to change first (the bar may read the song before the app's own read does), and the song is
        // shown at the same moment the colors start.
        void HoldForColors(MediaTrack track,Action show)
        {
            if(TestMode||track==null||preferences.PerformanceMode||!Adaptive||(customBackgroundLoaded&&preferences.Artwork)||!IsVisible||WindowState==WindowState.Minimized){show();return;}
            try{PrepareTrackColors(track);}catch(Exception){show();return;}
            colorSync.AfterFlush(show);
        }
        // The drifting colors of the artwork behind the app (the same motion as the mini player). They show while something is playing and the
        // setting is on; they only move while it is playing, and rest when it is paused, when the window is minimized or hidden.
        // The same colors as the players: the same cover, light, color and shade (only how fast it drifts differs), so the app, the bar and every player match.
        readonly FlowingBackdrop flowBackdrop=new FlowingBackdrop {Player=true,LightCap=185,Lively=true,Speed=1.9,Travel=1.5,Shade=.45};
        readonly Grid flowHost=new Grid {Visibility=Visibility.Collapsed,IsHitTestVisible=false};
        BitmapSource flowCover;
        // The cover as the backdrops see it: its minor colors pulled to the one it has the most of. (The sections keep following the cover's average.)
        BitmapSource fieldSource,fieldResult;
        BitmapSource PaletteField
        {
            get
            {
                if(paletteBitmap==null)return null;
                if(!ReferenceEquals(fieldSource,paletteBitmap)){fieldSource=paletteBitmap;fieldResult=Design.TowardDominant(paletteBitmap);}
                return fieldResult;
            }
        }
        // True when the moving colors are being worked out for a new cover.
        bool UpdateFlowBackdrop()
        {
            bool started=false;
            bool want=preferences.AnimatedBackground&&!preferences.PerformanceMode&&Adaptive&&paletteBitmap!=null&&!(customBackgroundLoaded&&preferences.Artwork);
            flowHost.Visibility=want?Visibility.Visible:Visibility.Collapsed;
            if(want)
            {
                var field=paletteBitmap;
                if(!ReferenceEquals(flowCover,field)){flowCover=field;started=flowBackdrop.SetCover(field);}
                // The moving colors are a fixed amount, not tied to the color amount, which would have left them nearly invisible.
                flowHost.Opacity=1;
            }
            // The colors keep drifting while the song is paused (they only rest when nothing is loaded): freezing the moment you paused looked broken.
            flowBackdrop.SetActive(want&&ambientTrack!=null);
            return started;
        }
        // The window's colors, the moving colors, the bar's band and the tint of the sections and the bar all follow the cover, and they change together
        // (the sections used to change first, because the others take a moment to work out).
        readonly ColorSync colorSync=new ColorSync();
        // The colors of what is playing show in the interface unless adaptive colors are off, or performance mode (which turns off what is only for show).
        bool Adaptive {get{return preferences.AdaptiveTheme&&!preferences.PerformanceMode;}}
        // The bar is glass when the setting is on and performance mode is not.
        bool GlassOn {get{return preferences.Glass&&!preferences.PerformanceMode;}}
        // Performance mode and the glass setting, applied to the bar, the page behind it and the colors.
        void ApplyGlass()
        {
            Design.Instant=preferences.PerformanceMode;
            if(playerBar==null)return;
            playerBar.Backdrop.On=!preferences.PerformanceMode;
            playerBar.SetGlass(GlassOn);
            UpdatePageClip();
            UpdateAmbientColor();
        }
        void UpdateAmbientColor()
        {
            // Surfaces are plain white veils, so the backdrop alone carries the color.
            Color accent=Adaptive?customBackgroundLoaded&&preferences.Artwork?animatedBackground.AccentColor:mediaAccent:Color.FromRgb(46,46,48);
            double strength=Math.Max(0,Math.Min(1,preferences.ThemeIntensity));
            bool colored=Adaptive&&(paletteArtwork!=null||customBackgroundLoaded||mediaAccent!=Color.FromRgb(46,46,48));
            colorSync.Enabled=!TestMode&&!preferences.PerformanceMode&&!snapColors;
            ambientBackdrop.Sync=flowBackdrop.Sync=colorSync;
            if(playerBar!=null)playerBar.Backdrop.Sync=colorSync;
            int round=colorSync.Begin(),waiting=0;
            if(ambientBackdrop.SetPalette(accent,strength,colored,Adaptive&&!(customBackgroundLoaded&&preferences.Artwork)?PaletteField:null))waiting++;
            // The sections follow what the cover is made of as a whole, not its most colorful patch (a black cover with a little green is still a dark cover).
            Color tone=colored&&paletteBitmap!=null&&Adaptive&&!(customBackgroundLoaded&&preferences.Artwork)?Design.Average(paletteBitmap):accent;
            if(playerBar!=null&&playerBar.Backdrop.SetCover(Adaptive&&!(customBackgroundLoaded&&preferences.Artwork)?PaletteField:null,accent,colored,strength))waiting++;
            if(UpdateFlowBackdrop())waiting++;
            // The tint of the sections and of the bar begins with the others (at once when none of them has anything new to work out).
            double tint=colored?strength:0;bool band=!preferences.PerformanceMode,glassy=GlassOn;
            colorSync.Expect(round,waiting,()=>
            {
                UpdateSurfacePalette(accent,tint,tone);
                if(playerBar!=null)TransitionColor(playerBar.Surface,SolidColorBrush.ColorProperty,PlayerBar.SurfaceColor(tone,tint,band,glassy));
            });
        }
        readonly SolidColorBrush optionSurface=new SolidColorBrush(Color.FromRgb(32,32,33));
        readonly SolidColorBrush selectionSurface=new SolidColorBrush(Color.FromRgb(48,48,49));
        readonly SolidColorBrush trackSurface=new SolidColorBrush(Color.FromRgb(46,46,47));
        readonly SolidColorBrush settingsGroupSurface=new SolidColorBrush(Color.FromRgb(35,35,36));
        readonly SolidColorBrush settingsOptionSurface=new SolidColorBrush(Color.FromRgb(40,40,42)),settingsSelectionSurface=new SolidColorBrush(Color.FromRgb(51,51,53));
        readonly SolidColorBrush tooltipSurface=new SolidColorBrush(Color.FromRgb(24,24,26)),settingsTooltipSurface=new SolidColorBrush(Color.FromRgb(24,24,26));
        void InitializeMutablePalette()
        {
            // Starting at the final veils avoids fading through gray on launch.
            foreach(var stop in cardSurface.GradientStops)stop.Color=Veil(11);
            sidebarSurface.Color=RailFor(Colors.Black,0);
            displaySurface.Color=Veil(11);
            selectedDisplaySurface.Color=Veil(26);
            optionSurface.Color=Veil(22);
            selectionSurface.Color=Veil(38);
            trackSurface.Color=Veil(26);
            // WPF may freeze brushes when a style first resolves a resource.
            // An attached clock keeps our shared, animated palette mutable.
            foreach(var brush in new[]{optionSurface,selectionSurface,trackSurface,settingsGroupSurface,tooltipSurface})
                brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(brush.Color,brush.Color,TimeSpan.Zero));
        }
        // The sidebar is darker than the page beside it.
        // The sections of a page are a deeper shade of the color coming from what is playing, so they read as part of it; plain veil with nothing playing.
        static Color SectionTint(Color accent,double strength)
        {
            if(strength<=0)return Veil(11);
            // The cover's average color, a little brighter and more colorful, laid over the page as a see-through tint: a dark cover gives dark sections, a warm one warm sections.
            // Never whitish: a pale or grey cover gives a mid grey tint, not a light one; the colors of the cover are what stand out.
            double own=accent.R*.2126+accent.G*.7152+accent.B*.0722,luma=Math.Min(86,30+own*.42),s=Math.Max(0,Math.Min(1,strength));
            Func<byte,byte> channel=c=>(byte)Math.Max(0,Math.Min(150,luma+(c-own)*2.2));
            return Color.FromArgb((byte)(8+8*s),channel(accent.R),channel(accent.G),channel(accent.B));
        }
        // The sidebar is a light shade over the page: lighter still over a bright cover, so it never turns into a dark slab.
        static Color RailFor(Color accent,double strength)
        {
            if(strength<=0)return Color.FromArgb(0,255,255,255); // the sidebar has no background of its own: the colors of the window show through it
            double lightness=(accent.R*.2126+accent.G*.7152+accent.B*.0722)/255;
            // A dark veil in the cover's own color, so the sidebar follows it too.
            return Color.FromArgb(0,(byte)(accent.R*.25),(byte)(accent.G*.25),(byte)(accent.B*.25));
        }
        static Color Veil(byte alpha){return Color.FromArgb(alpha,255,255,255);}
        void UpdateSurfacePalette(Color accent,double strength,Color tone)
        {
            // Settings keep their own neutral look, and the page behind them is seen through them, so its sections go plain while they are open.
            // (The sections used to go plain while settings were open, which showed as a gray flash of the colors on opening and closing them.)
            var section=SectionTint(tone,strength);
            foreach(var stop in cardSurface.GradientStops)TransitionColor(stop,GradientStop.ColorProperty,section);
            TransitionColor(sidebarSurface,SolidColorBrush.ColorProperty,RailFor(tone,strength));
            TransitionColor(displaySurface,SolidColorBrush.ColorProperty,section);
            TransitionColor(selectedDisplaySurface,SolidColorBrush.ColorProperty,Veil(26));
            TransitionColor(optionSurface,SolidColorBrush.ColorProperty,Veil(22));
            TransitionColor(selectionSurface,SolidColorBrush.ColorProperty,Veil(38));
            TransitionColor(trackSurface,SolidColorBrush.ColorProperty,Veil(26));
            // Settings deliberately keep their own neutral palette.
            double gain=210d/Math.Max(80d,Math.Max(accent.R,Math.Max(accent.G,accent.B)));
            Color rich=Color.FromRgb((byte)Math.Min(255,16+accent.R*gain*strength*.31),(byte)Math.Min(255,16+accent.G*gain*strength*.31),(byte)Math.Min(255,16+accent.B*gain*strength*.31));
            TransitionColor(tooltipSurface,SolidColorBrush.ColorProperty,rich);
        }
        void TransitionColor(Animatable target,DependencyProperty property,Color color)
        {
            Color from=(Color)target.GetValue(property);if(from==color)return;
            target.BeginAnimation(property,new ColorAnimation(from,color,TimeSpan.FromMilliseconds(Design.Instant?0:500)){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
        }
        void BuildFloatingSettings()
        {
            // Hidden retains the measured layout; first opening has no layout build.
            settingsLayer=new Grid {Visibility=Visibility.Hidden,Background=Brushes.Transparent};
            settingsLayer.Resources["AdaptiveControlBrush"]=settingsOptionSurface;
            settingsLayer.Resources["AdaptiveSelectionBrush"]=settingsSelectionSurface;
            settingsLayer.Resources["AdaptiveTrackBrush"]=new SolidColorBrush(Color.FromRgb(57,57,60));
            settingsLayer.Resources["AdaptiveTooltipBrush"]=settingsTooltipSurface;
            settingsLayer.Resources["AdaptiveSettingBrush"]=settingsGroupSurface;
            var shade=new Border {Background=new SolidColorBrush(Color.FromArgb(120,0,0,0))};
            settingsShade=shade;
            shade.MouseLeftButtonDown+=(s,e)=>SetSettingsOpen(false);
            settingsLayer.Children.Add(shade);
            var layout=new Grid();
            var menu=new DockPanel {Background=Brushes.Black,Margin=new Thickness(0),LastChildFill=true};
            var brand=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(18,14,18,14)};
            brand.Children.Add(Art.Logo(23));
            brand.Children.Add(new TextBlock {Text="lucent",FontSize=18,FontWeight=FontWeights.Bold,Margin=new Thickness(10,0,0,0)});
            DockPanel.SetDock(brand,Dock.Bottom);
            menu.Children.Add(brand);
            var search=new TextBox {Text="",Height=36,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(12,12,12,17),ToolTip="Search settings",FontSize=13};
            System.Windows.Automation.AutomationProperties.SetName(search,"Search settings");
            var searchBox=new Grid();
            searchBox.Children.Add(search);
            var placeholder=new TextBlock {Text="Search settings…",Foreground=Muted,FontSize=13,Margin=new Thickness(25,22,0,0),IsHitTestVisible=false};
            searchBox.Children.Add(placeholder);
            var categories=new StackPanel();
            categories.Children.Add(new TextBlock {Text="Interface",Foreground=Muted,FontSize=12,Margin=new Thickness(9,0,0,9),FontWeight=FontWeights.SemiBold});
            foreach(string name in new[]{"General","Appearance","Shortcuts","Backgrounds","Mini Player","Discord RPC","Stats","About"})
            {
                if(name=="Discord RPC")categories.Children.Add(new TextBlock{Text="Integrations",Foreground=Muted,FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(9,24,0,9)});
                if(name=="Shortcuts")categories.Children.Add(new TextBlock{Text="Application",Foreground=Muted,FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(9,24,0,9)});
                string category=name;
                var button=SettingsCategoryButton(name,()=>category==settingsCategory);
                button.Click+=(s,e)=>SelectSettingsCategory(category);
                settingsCategoryButtons[name]=button;
                categories.Children.Add(button);
            }
            // One highlight behind every row, which slides to whichever row is selected.
            settingsPill=new Border {Height=32,VerticalAlignment=VerticalAlignment.Top,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(Color.FromRgb(44,44,46)),IsHitTestVisible=false,RenderTransform=new TranslateTransform()};
            categories.SizeChanged+=(s,e)=>MoveSettingsPill(false);
            var categoryList=new Grid {Margin=new Thickness(10,0,10,0)};
            categoryList.Children.Add(settingsPill);
            categoryList.Children.Add(categories);
            menu.Children.Add(categoryList);
            search.TextChanged+=(s,e)=>
            {
                placeholder.Visibility=string.IsNullOrWhiteSpace(search.Text)?Visibility.Visible:Visibility.Collapsed;
                string query=search.Text.Trim();string first=null;
                foreach(var entry in settingsCategoryButtons)
                {
                    string terms=entry.Key=="Shortcuts"?"Shortcuts keys bind resolution crosshair zoom music next previous":entry.Key=="Appearance"?"Appearance theme colors adaptive background motion":entry.Key=="Mini Player"?"Mini Player media music player position topmost lyrics size center background movement speed hide controls idle window size reset":entry.Key=="Backgrounds"?"Backgrounds gif animated custom picture preview import wallpaper":entry.Key=="Discord RPC"?"Discord RPC Rich Presence status activity playing game with lucent profile ptb canary":entry.Key=="Stats"?"Stats statistics numbers usage time launches zoom resolution games fun":entry.Key=="General"?"General startup Windows tray audio sound volume footsteps limit import":"About lucent version";
                    bool show=query.Length==0||terms.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
                    entry.Value.Visibility=show?Visibility.Visible:Visibility.Collapsed;
                    if(settingsCards.ContainsKey(entry.Key))settingsCards[entry.Key].Visibility=entry.Value.Visibility;
                    if(show&&first==null)first=entry.Key;
                }
                // Searching shows the cards that match.
                if(query.Length>0&&!settingsOverviewShown)ShowSettingsOverview();
            };
            // The overview: the search box, then one card per category.
            settingsOverview=new StackPanel();
            searchBox.Margin=new Thickness(0,0,0,16);
            search.Margin=new Thickness(0);placeholder.Margin=new Thickness(13,10,0,0);
            settingsOverview.Children.Add(searchBox);
            var cardGrid=new WrapPanel();
            foreach(var entry in settingsCategoryButtons)
            {
                var card=SettingsCategoryCard(entry.Key);
                settingsCards[entry.Key]=card;cardGrid.Children.Add(card);
            }
            settingsOverview.Children.Add(cardGrid);
            settingsOverview.SizeChanged+=(s,e)=>
            {
                // Two or three cards across, whatever the width of the window.
                int across=e.NewSize.Width>640?3:e.NewSize.Width>400?2:1;
                double width=Math.Max(150,Math.Floor((e.NewSize.Width-across*12)/across));
                foreach(FrameworkElement child in cardGrid.Children)child.Width=width;
            };
            var right=new DockPanel {Background=new SolidColorBrush(Color.FromRgb(19,19,20)),Margin=new Thickness(0),LastChildFill=true};
            layout.Children.Add(right);
            var heading=new DockPanel {Margin=new Thickness(22,22,16,18)};DockPanel.SetDock(heading,Dock.Top);right.Children.Add(heading);
            var close=WindowAction("Close",()=>SetSettingsOpen(false));DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
            // In a category, a button takes you back to the grid of categories.
            settingsBack=Action("‹  All settings",ShowSettingsOverview);
            settingsBack.Padding=new Thickness(12,6,14,6);settingsBack.MinHeight=0;settingsBack.Margin=new Thickness(0,0,12,0);settingsBack.VerticalAlignment=VerticalAlignment.Center;settingsBack.Visibility=Visibility.Collapsed;
            DockPanel.SetDock(settingsBack,Dock.Left);heading.Children.Add(settingsBack);
            var labels=new StackPanel();
            settingsHeading=Text("Appearance",18,true);
            settingsDescription=Text("",13);
            settingsDescription.Foreground=Muted;
            labels.Children.Add(settingsHeading);
            labels.Children.Add(settingsDescription);
            heading.Children.Add(labels);
            settingsContent=new ContentControl {Focusable=false,Margin=new Thickness(22,0,18,20),RenderTransform=new TranslateTransform()};
            settingsScroll=new ScrollViewer {Content=settingsContent,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,Padding=new Thickness(0,0,7,0)};
            right.Children.Add(settingsScroll);
            var settingsMove=new TransformGroup();
            settingsMove.Children.Add(new ScaleTransform(1,1));settingsMove.Children.Add(new TranslateTransform());
            settingsCard=new Border {RenderTransformOrigin=new Point(.5,.5),RenderTransform=settingsMove,Child=layout,CornerRadius=new CornerRadius(18),BorderBrush=Brushes.Black,BorderThickness=new Thickness(7),ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,28,0,0)};
            settingsCard.SizeChanged+=(s,e)=>layout.Clip=new RectangleGeometry(new Rect(0,0,layout.ActualWidth,layout.ActualHeight),12,12);
            KeyboardNavigation.SetTabNavigation(settingsCard,KeyboardNavigationMode.Cycle);settingsLayer.Children.Add(settingsCard);
            settingsOverlayGear=ModernNavButton("Settings");
            settingsOverlayGear.ToolTip=RailTooltip("Settings",settingsOverlayGear,false,settingsTooltipSurface);
            settingsOverlayGear.HorizontalAlignment=HorizontalAlignment.Left;
            settingsOverlayGear.VerticalAlignment=VerticalAlignment.Top;
            settingsLayer.Children.Add(settingsOverlayGear);
            Design.ShareMotion(settingsRailButton,settingsOverlayGear);
            // While settings is closed the copy keeps sitting exactly over the sidebar gear, whatever the window does.
            workspace.LayoutUpdated+=(s,e)=>{if(!settingsOpen)PositionSettingsGear();};
            // The blurred copy of the app behind settings must sit exactly over the app: the brush is mapped to the app's own size, not to the bounds of everything drawn in it
            // (parts of the app draw past its edge, like the blur under the player bar, and that made the copy look zoomed in).
            FitSettingsBackdrop();root.SizeChanged+=(s,e)=>FitSettingsBackdrop();
            settingsBackdropLayer.Children.Add(settingsBackdrop);
            windowSurface.ClipToBounds=true;
            Panel.SetZIndex(settingsBackdropLayer,19);
            windowSurface.Children.Add(settingsBackdropLayer);
            Panel.SetZIndex(settingsLayer,20);
            windowSurface.Children.Add(settingsLayer);
            ShowSettingsOverview();
            SizeSettingsPanel();
            ApplySettingsMotion(0);
        }
        // Aims the growing motion of the settings window at the settings gear, so it looks as if it comes from there.
        void AimSettingsOrigin()
        {
            var gear=settingsRailButton;
            if(settingsCard==null||gear==null||!gear.IsLoaded||settingsCard.Width<=0||settingsCard.Height<=0)return;
            var middle=gear.TranslatePoint(new Point(gear.ActualWidth/2,gear.ActualHeight/2),windowSurface);
            var corner=VisualTreeHelper.GetOffset(settingsCard);
            settingsCard.RenderTransformOrigin=new Point((middle.X-corner.X)/settingsCard.Width,(middle.Y-corner.Y)/settingsCard.Height);
        }
        void FitSettingsBackdrop()
        {
            double width=root.ActualWidth,height=root.ActualHeight;
            if(width<=0||height<=0)return;
            const double bleed=24;   // the backdrop rectangle reaches this far past every edge so its blur has no faded border
            settingsBackdrop.Fill=new VisualBrush(root){Stretch=Stretch.Fill,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,TileMode=TileMode.None,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(-bleed,-bleed,width+2*bleed,height+2*bleed),ViewportUnits=BrushMappingMode.RelativeToBoundingBox,Viewport=new Rect(0,0,1,1)};
        }        void SizeSettingsPanel()
        {
            if(settingsCard==null)return;
            settingsCard.Width=Math.Min(980,Math.Max(600,ActualWidth-130));
            settingsCard.Height=Math.Min(710,Math.Max(400,ActualHeight-110));
            PositionSettingsGear();
        }
        void PositionSettingsGear()
        {
            if(settingsOverlayGear==null||settingsRailButton==null||!settingsRailButton.IsLoaded)return;
            // Measured from the middle of the button. A press or hover scales the button about its middle, and the corner of a scaled button
            // moves, which used to pull the copy up and to the left by a few pixels whenever the gear was clicked.
            double halfWidth=settingsRailButton.ActualWidth/2,halfHeight=settingsRailButton.ActualHeight/2;
            var middle=settingsRailButton.TranslatePoint(new Point(halfWidth,halfHeight),windowSurface);
            var position=new Point(middle.X-halfWidth,middle.Y-halfHeight);
            var margin=settingsOverlayGear.Margin;
            if(Math.Abs(margin.Left-position.X)<.01&&Math.Abs(margin.Top-position.Y)<.01)return;
            settingsOverlayGear.Margin=new Thickness(position.X,position.Y,0,0);
        }
        readonly Dictionary<string,Border> settingsCards=new Dictionary<string,Border>();
        StackPanel settingsOverview;
        Button settingsBack;
        bool settingsOverviewShown;
        // What each category is for, in a line.
        static string SettingsBlurb(string name)
        {
            return name=="Appearance"?"The way lucent looks and behaves.":name=="Shortcuts"?"Customize global shortcuts. Press Esc while binding to clear a key.":name=="Mini Player"?"The floating mini player: where it opens, how it looks and when its controls hide.":name=="Backgrounds"?"Add your own GIF, preview it, then use it across Lucent.":name=="Stats"?"A few friendly numbers about how you use Lucent.":name=="Discord RPC"?"Show on Discord the game you are playing with Lucent.":name=="General"?"How lucent behaves on your computer.":"A lightweight display and game utility for Windows.";
        }
        // Settings opens as a grid of categories; a category opens on its own page, with a button back to the grid.
        void ShowSettingsOverview()
        {
            if(settingsOverview==null)return;
            if(settingsOverviewShown&&settingsContent.Content==settingsOverview)return;
            bool switching=settingsContent.Content!=null&&IsLoaded;
            var current=settingsContent.Content as UIElement;
            var oldParent=current==null?null:VisualTreeHelper.GetParent(current) as ContentControl;
            settingsContent.Content=null;
            settingsContent.Content=settingsOverview;
            settingsOverviewShown=true;
            settingsBack.Visibility=Visibility.Collapsed;
            settingsHeading.Text="Settings";
            settingsDescription.Text="Pick a category.";
            if(switching)
            {
                var rise=(TranslateTransform)settingsContent.RenderTransform;
                Design.Animate(settingsContent,OpacityProperty,0,1,220);
                rise.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(10,0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            }
            if(settingsScroll!=null)settingsScroll.ScrollToVerticalOffset(0);
        }
        // One category as a card: its icon, its name and a line about it.
        Border SettingsCategoryCard(string name)
        {
            var icon=Design.SettingsIcon(name,22);
            icon.HorizontalAlignment=HorizontalAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;
            var tile=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(13),Background=new SolidColorBrush(Color.FromArgb(26,255,255,255)),Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Top,Child=icon};
            var copy=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            copy.Children.Add(new TextBlock {Text=name,FontSize=15,FontWeight=FontWeights.Bold});
            copy.Children.Add(new TextBlock {Text=SettingsBlurb(name),FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0)});
            var row=new DockPanel();
            DockPanel.SetDock(tile,Dock.Left);row.Children.Add(tile);row.Children.Add(copy);
            var button=new Button {Content=row,Style=(Style)Resources["SettingsRow"],Padding=new Thickness(16,14,16,14),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,MinHeight=96};
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            string category=name;
            button.Click+=(s,e)=>SelectSettingsCategory(category);
            return new Border {Child=button,Background=settingsGroupSurface,CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,12,12),ClipToBounds=true};
        }        void SelectSettingsCategory(string name)
        {
            if(!settingsSections.ContainsKey(name))return;
            if(name==settingsCategory&&settingsContent.Content!=null&&!settingsOverviewShown)return;
            settingsCategory=name;settingsOverviewShown=false;settingsBack.Visibility=Visibility.Visible;
            var section=settingsSections[name];var panel=section.Content;
            var oldParent=panel.Parent as Border;if(oldParent!=null)oldParent.Child=null;
            bool switching=settingsContent.Content!=null&&IsLoaded;
            settingsContent.Content=null;panel.Margin=new Thickness(0);
            settingsContent.Content=panel;
            // Changing tabs in settings moves the same way as changing tabs in the app: the new page fades in and rises a little.
            if(switching)
            {
                var rise=(TranslateTransform)settingsContent.RenderTransform;
                Design.Animate(settingsContent,OpacityProperty,0,1,220);
                rise.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(10,0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            }
            settingsHeading.Text=name;
            settingsDescription.Text=SettingsBlurb(name);
            if(panel.Children.Count>0&&panel.Children[0] is TextBlock)panel.Children[0].Visibility=Visibility.Collapsed;
            foreach(var item in settingsCategoryButtons)SetCategoryInk(item.Value,item.Key==name||item.Value.IsMouseOver);
            MoveSettingsPill(true);
        }
        Button SettingsCategoryButton(string name,Func<bool> selected)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Opacity=.6};
            var icon=Design.SettingsIcon(name,16);
            icon.Margin=new Thickness(0,0,11,0);
            icon.VerticalAlignment=VerticalAlignment.Center;
            row.Children.Add(icon);
            row.Children.Add(new TextBlock {Text=name,FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var button=new Button {Content=row,Style=(Style)Resources["SettingsRow"],Margin=new Thickness(0,1,0,1)};
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            button.MouseEnter+=(s,e)=>SetCategoryInk(button,true);
            button.MouseLeave+=(s,e)=>SetCategoryInk(button,selected());
            return button;
        }
        static void SetCategoryInk(Button button,bool bright)
        {
            var row=(UIElement)button.Content;
            Design.Animate(row,OpacityProperty,row.Opacity,bright?1:.6,bright?90:160);
        }
        void MoveSettingsPill(bool animate)
        {
            if(settingsPill==null)return;
            var target=settingsCategoryButtons[settingsCategory];
            settingsPill.Visibility=target.Visibility;
            double y=VisualTreeHelper.GetOffset(target).Y;
            var slide=(TranslateTransform)settingsPill.RenderTransform;
            if(!animate)
            {
                slide.BeginAnimation(TranslateTransform.YProperty,null);
                slide.Y=y;
                return;
            }
            // A fixed 250 ms whatever the distance, starting quickly and settling gently.
            var motion=new DoubleAnimationUsingKeyFrames();
            motion.KeyFrames.Add(new SplineDoubleKeyFrame(y,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)),new KeySpline(.25,.1,.25,1)));
            slide.BeginAnimation(TranslateTransform.YProperty,motion);
        }
        void ArrangeShortcutTiles(SectionPanel general)
        {
            var tiles=new WrapPanel {Margin=new Thickness(0,12,0,0)};
            foreach(var row in general.Content.Children.OfType<DockPanel>().ToArray())
            {
                general.Children.Remove(row);row.Margin=new Thickness(0);row.VerticalAlignment=VerticalAlignment.Center;
                var button=row.Children.OfType<Button>().FirstOrDefault();
                if(button!=null)
                {
                    button.Background=new SolidColorBrush(Color.FromRgb(20,20,21));
                    button.Margin=new Thickness(8,0,0,0);
                    button.Padding=new Thickness(10,8,10,8);
                    button.MinHeight=30;
                    button.FontSize=12;
                }
                var label=row.Children.OfType<TextBlock>().FirstOrDefault();
                if(label!=null)
                {
                    label.FontWeight=FontWeights.SemiBold;
                    label.Margin=new Thickness(0);
                    label.FontSize=13;
                }
                if(label!=null)
                {
                    row.Children.Remove(label);
                    var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
                    copy.Children.Add(label);
                    copy.Children.Add(new TextBlock{Text="Global",FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Muted,Margin=new Thickness(0,2,0,0)});
                    var icon=Design.Icon(label.Text.Contains("pause")?"Play":label.Text.Contains("Next")?"Next":label.Text.Contains("Previous")?"Previous":label.Text.Contains("crosshair")?"Crosshair":label.Text.Contains("resolution")?"Resolution":"Keyboard",18);
                    icon.Margin=new Thickness(0,0,10,0);
                    DockPanel.SetDock(icon,Dock.Left);
                    row.Children.Add(icon);
                    row.Children.Add(copy);
                }
                tiles.Children.Add(new Border {Child=row,Background=settingsGroupSurface,CornerRadius=new CornerRadius(14),Padding=new Thickness(12,7,8,7),Height=52,Margin=new Thickness(0,0,8,8)});
            }
            tiles.SizeChanged+=(s,e)=>
            {
                double width=e.NewSize.Width>570?(e.NewSize.Width-20)/2:e.NewSize.Width-10;
                foreach(FrameworkElement child in tiles.Children)child.Width=Math.Max(200,width);
            };
            general.Children.Add(tiles);
        }
        void SetSettingsOpen(bool open)
        {
            if(settingsLayer==null||settingsOpen==open)return;
            settingsOpen=open;int transition=++settingsTransition;HideStatus();UpdateRailSelection();UpdateAmbientColor();
            if(!open)AimSettingsOrigin();
            if(open)
            {
                EndBackgroundPreview();
                // Settings always opens on the grid of categories (a caller that wants one category selects it right after).
                ShowSettingsOverview();
                SizeSettingsPanel();
                animatedBackground.SetPlaying(false);
                PositionSettingsGear();
                settingsRailButton.Opacity=0;
                root.IsHitTestVisible=false;
                // Motion is at 0 here unless reopening mid-close, so nothing stale can reach the first frame.
                settingsBackdropLayer.Visibility=Visibility.Visible;
                settingsLayer.Visibility=Visibility.Visible;
                settingsLayer.UpdateLayout();
                AimSettingsOrigin();
                settingsCard.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
            foreach(var gear in new[]{settingsRailButton,settingsOverlayGear})Design.Animate((UIElement)gear.Content,OpacityProperty,open?.57:1,open?1:.57,180);
            // Behind settings is a blurred copy of the window. While the colors drift that copy changes every frame, so the whole window was blurred again at every
            // step: the colors rest while settings is open, and the card moves as one picture, so each step only moves pictures.
            if(!TestMode){flowBackdrop.Hold(true);settingsCard.CacheMode=new BitmapCache(VisualTreeHelper.GetDpi(this).DpiScaleX);}
            // Building the blur makes the first frame slow. Start
            // the clock after it is drawn, or the curve would skip ahead to catch up.
            int frames=0;EventHandler begin=null;
            begin=(s,e)=>
            {
                if(transition!=settingsTransition){CompositionTarget.Rendering-=begin;return;}
                if(++frames<2)return;CompositionTarget.Rendering-=begin;
                double from=(double)GetValue(SettingsMotionProperty),to=open?1:0;
                // A partial reversal takes proportionally less time at the same pace.
                var length=TimeSpan.FromMilliseconds(Math.Max(80,(open?400:230)*Math.Abs(to-from)));
                var motion=new DoubleAnimationUsingKeyFrames();
                motion.KeyFrames.Add(new LinearDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
                motion.KeyFrames.Add(new SplineDoubleKeyFrame(to,KeyTime.FromTimeSpan(length),open?new KeySpline(.2,.2,.2,1):new KeySpline(.25,.2,.25,1)));
                motion.Completed+=(c,d)=>
                {
                    if(transition!=settingsTransition)return;
                    BeginAnimation(SettingsMotionProperty,null);SetValue(SettingsMotionProperty,to);
                    settingsCard.CacheMode=null;
                    if(settingsOpen)return;
                    if(!TestMode)flowBackdrop.Hold(WindowState==WindowState.Minimized||!IsVisible);
                    settingsLayer.Visibility=Visibility.Hidden;
                    settingsBackdropLayer.Visibility=Visibility.Collapsed;
                    settingsRailButton.Opacity=1;
                    root.IsHitTestVisible=true;
                    settingsRailButton.Focus();
                    ApplyAppearance();
                };
                BeginAnimation(SettingsMotionProperty,motion);
            };
            CompositionTarget.Rendering+=begin;
        }
    }
}
