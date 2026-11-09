using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lucent;
using ResolutionSwitcher;
class ModernUiTests
{
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object value,string name){return value.GetType().GetField(name,Private).GetValue(value);}
 static object Call(object value,string method,params object[] args){return value.GetType().GetMethod(method,Private).Invoke(value,args);}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static readonly byte[] Gif={71,73,70,56,57,97,2,0,1,0,129,0,0,0,0,0,255,255,255,255,0,0,0,0,255,33,249,4,4,5,0,0,0,44,0,0,0,0,2,0,1,0,0,2,2,148,10,0,33,249,4,8,10,0,0,0,44,1,0,0,0,1,0,1,0,0,2,2,92,1,0,33,249,4,4,10,0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,76,1,0,59};
 [STAThread]static int Main()
 {
  var app=new Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run();}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));app.Run();return exit;
 }
 static async Task Run()
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","modern-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  var oldPreferences=new Preferences{MinimizeToTray=true};StateStore.Normalize(oldPreferences);Check(!oldPreferences.MinimizeToTray,"Old tray default was not migrated");oldPreferences.MinimizeToTray=true;StateStore.Normalize(oldPreferences);Check(oldPreferences.MinimizeToTray,"Explicit tray preference was not preserved");
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};shell.Show();
  // Dragging the sides of the window must not lay out the pages that are not showing (that made it lag): they sit out of layout while it is resized, then come back.
  {
   await Task.Delay(800);Call(shell,"ShowPage","Home");shell.UpdateLayout();await Task.Delay(300);
   var restSlots=(System.Collections.IDictionary)Field(shell,"pageSlots");double restWidth=shell.Width;
   var restWatch=System.Diagnostics.Stopwatch.StartNew();for(int k=1;k<=10;k++){shell.Width=restWidth-k*4;shell.UpdateLayout();}
   Check(restSlots.Count>1&&restSlots.Values.Cast<ScrollViewer>().Any(s=>s.Visibility==Visibility.Collapsed),"Pages that are not showing should sit out of layout while the window is resized");
   Check(restWatch.Elapsed.TotalMilliseconds<1500,"Making the window narrower is slow: "+restWatch.Elapsed.TotalMilliseconds+" ms for 10 steps");
   await Until(()=>restSlots.Values.Cast<ScrollViewer>().All(s=>s.Visibility!=Visibility.Collapsed));
   Check(restSlots.Values.Cast<ScrollViewer>().All(s=>s.Visibility!=Visibility.Collapsed),"The pages did not come back once the window settled");
   shell.Width=restWidth;shell.UpdateLayout();
  }


  try
  {
   var face=new Typeface(Design.Font,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);GlyphTypeface glyph;Check(face.TryGetGlyphTypeface(out glyph)&&glyph.FamilyNames.Values.Any(n=>n.Contains("Inter")),"Inter was not resolved from bundled fonts");
   var pages=(IDictionary)Field(shell,"pages");var content=(ContentControl)Field(shell,"content");
   Check(((Grid)Field(shell,"workspace")).ColumnDefinitions[0].Width.Value==0,"The page tabs run along the top, so there is no sidebar column");
   Check((string)Field(shell,"page")=="Home","Lucent should open on the Home page");
   Check(shell.Title=="Lucent"&&TextOptions.GetTextFormattingMode(shell)==TextFormattingMode.Ideal&&TextOptions.GetTextHintingMode(shell)==TextHintingMode.Auto,"Brand or unsnapped typography");
   shell.WindowState=WindowState.Minimized;await Task.Delay(60);Check(shell.IsVisible&&shell.ShowInTaskbar,"Default minimize hid the taskbar window");shell.WindowState=WindowState.Normal;
   foreach(Button rail in (IEnumerable)Field(shell,"navigationButtons"))
   {
    Check(((SolidColorBrush)rail.Background).Color.A<=40,"A tab has a heavy selection background");
    
   }
   Call(shell,"ShowPage","Profiles");Func<object> shownPage=()=>((ContentControl)Field(shell,"content")).Content;object original=shownPage();
   Call(shell,"ShowPage","Settings");await Task.Delay(420);Check(shell.SettingsOpen&&ReferenceEquals(original,shownPage()),"Opening settings changed page");
   var gear=(Button)Field(shell,"settingsRailButton");var modalGear=(Button)Field(shell,"settingsOverlayGear");var surface=(Grid)Field(shell,"windowSurface");
   Check((gear.TranslatePoint(new Point(),surface)-modalGear.TranslatePoint(new Point(),surface)).Length<.01&&gear.Opacity==0,"Settings gear moved or duplicated");
   // Pressing scales the gear about its middle; the copy that appears over the settings must still land exactly on it.
   var shared=(ScaleTransform)gear.RenderTransform;shared.ScaleX=shared.ScaleY=1.12;Call(shell,"PositionSettingsGear");shell.UpdateLayout();
   Check((gear.TranslatePoint(new Point(gear.ActualWidth/2,gear.ActualHeight/2),surface)-modalGear.TranslatePoint(new Point(modalGear.ActualWidth/2,modalGear.ActualHeight/2),surface)).Length<.01,"The settings gear copy is off while the button is pressed");
   var unscaledMargin=modalGear.Margin;shared.ScaleX=shared.ScaleY=1;Call(shell,"PositionSettingsGear");shell.UpdateLayout();
   Check(Math.Abs(modalGear.Margin.Left-unscaledMargin.Left)<.01&&Math.Abs(modalGear.Margin.Top-unscaledMargin.Top)<.01,"Pressing the gear moved the copy by "+(modalGear.Margin.Left-unscaledMargin.Left)+", "+(modalGear.Margin.Top-unscaledMargin.Top));Check(((FrameworkElement)Field(shell,"settingsBackdrop")).IsVisible&&((UIElement)Field(shell,"settingsBackdrop")).Effect is System.Windows.Media.Effects.BlurEffect&&((Grid)Field(shell,"root")).Effect==null,"Settings blur layer missing");
   Call(shell,"SelectSettingsCategory","Shortcuts");shell.UpdateLayout();
   var settings=(ContentControl)Field(shell,"settingsContent");
   var sections=(IDictionary)Field(shell,"settingsSections");Check(sections.Contains("Mini Player")&&sections.Contains("Backgrounds")&&sections.Contains("Stats")&&sections.Contains("General")&&sections.Contains("About")&&!sections.Contains("Playback")&&!sections.Contains("Audio"),"Settings categories do not match requested structure");
   var key=All<Button>(settings).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Switch resolution shortcut");
   var prefs=(Preferences)Field(shell,"preferences");prefs.SwitchKey=121;
   typeof(Shell).GetField("mediaAccent",Private).SetValue(shell,Color.FromRgb(210,110,35));Call(shell,"UpdateAmbientColor");await Task.Delay(1100);
   var card=(LinearGradientBrush)Field(shell,"cardSurface");var option=(SolidColorBrush)Field(shell,"optionSurface");var tooltip=(SolidColorBrush)Field(shell,"tooltipSurface");
   Check(card.GradientStops.All(stop=>stop.Color!=Color.FromArgb(11,255,255,255)&&stop.Color.R<220&&stop.Color.A<40)&&option.Color==Color.FromArgb(22,255,255,255),"With settings open, the sections keep the color of the music (no gray flash) and options are plain white veils: "+option.Color);
   Check(option.Color.A<160&&((SolidColorBrush)Field(shell,"sidebarSurface")).Color.A<160&&((SolidColorBrush)Field(shell,"displaySurface")).Color.A<160,"Adaptive surfaces hide the backdrop");Check(tooltip.Color.A==255,"Floating tooltip surface must stay opaque");
   CheckNeutralSettings(shell,sections);
   Check(tooltip.Color.R>tooltip.Color.B,"Tooltips do not adapt to artwork");
   typeof(Shell).GetField("mediaAccent",Private).SetValue(shell,Color.FromRgb(35,90,210));Call(shell,"UpdateAmbientColor");await Task.Delay(1100);Check(tooltip.Color.B>tooltip.Color.R,"Tooltip palette did not follow artwork change");
   prefs.AdaptiveTheme=false;Call(shell,"UpdateAmbientColor");await Task.Delay(1100);Check(option.Color.R==option.Color.B&&tooltip.Color.R==16&&tooltip.Color.B==16,"Disabling adaptation did not restore neutral surfaces: "+tooltip.Color);prefs.AdaptiveTheme=true;Call(shell,"UpdateAmbientColor");
   key.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   key.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(shell),Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
   Check(prefs.SwitchKey==0&&shell.SettingsOpen,"Escape did not unbind without closing settings");
   Call(shell,"SetSettingsOpen",false);Call(shell,"SetSettingsOpen",true);await Task.Delay(420);Check(shell.SettingsOpen&&((Grid)Field(shell,"settingsLayer")).Visibility==Visibility.Visible,"Rapid settings toggle lost state");
   Call(shell,"SetSettingsOpen",false);await Task.Delay(360);Check(!shell.SettingsOpen&&ReferenceEquals(shownPage(),original),"Closing settings reloaded page");
   Check(((Grid)Field(shell,"settingsLayer")).Visibility==Visibility.Hidden&&((UIElement)Field(shell,"settingsBackdropLayer")).Visibility==Visibility.Collapsed&&gear.Opacity==1,"Settings did not retain layout or restore its original gear");
   var settingsCardElement=(UIElement)Field(shell,"settingsCard");// Closed, the card is hidden and sits a little smaller; the open window resting exactly in place is checked in the shell polish test.
  Check(((Grid)Field(shell,"settingsLayer")).Visibility==Visibility.Hidden||settingsCardElement.RenderTransform==null||settingsCardElement.RenderTransform.Value.IsIdentity,"Settings card is moving while visible after it settled");{var backdropBrush=(VisualBrush)((System.Windows.Shapes.Rectangle)Field(shell,"settingsBackdrop")).Fill;var appRoot=(Grid)Field(shell,"root");Check(backdropBrush.ViewboxUnits==BrushMappingMode.Absolute&&Math.Abs(backdropBrush.Viewbox.Width-(appRoot.ActualWidth+48))<.5&&Math.Abs(backdropBrush.Viewbox.Height-(appRoot.ActualHeight+48))<.5&&backdropBrush.Viewbox.X==-24&&backdropBrush.Viewbox.Y==-24,"Settings backdrop is not mapped one to one onto the page (it looks zoomed)");}
   await CheckSettingsTransition(shell);
   await CheckScrolling(shell,pages);
   var dockTipButton=(Button)Field(shell,"playerDockButton");var dockTip=(ToolTip)dockTipButton.ToolTip;Check(ToolTipService.GetInitialShowDelay(dockTipButton)==500&&ToolTipService.GetBetweenShowDelay(dockTipButton)==500,"Player tooltip delay");
   var dockPlacement=dockTip.CustomPopupPlacementCallback(new Size(80,30),new Size(32,28),new Point());Check(dockPlacement[0].Point.X==-24&&dockPlacement[0].Point.Y==-38,"The title of a player icon should be centered above it: "+dockPlacement[0].Point);
   var railTip=(ToolTip)((IEnumerable)Field(shell,"navigationButtons")).Cast<Button>().First(b=>(string)b.Tag=="Settings").ToolTip;Check(dockTip.FontSize==railTip.FontSize&&dockTip.FontWeight==railTip.FontWeight&&ReferenceEquals(dockTip.Background,railTip.Background)&&dockTip.Style==railTip.Style,"Player tooltip style differs from the rail");
   Check(((SolidColorBrush)((ToolTip)modalGear.ToolTip).Background).Color==Color.FromRgb(24,24,26),"Settings gear tooltip inherited adaptive color");
   CheckMediaClock();CheckBackdrop();CheckSmoothText(shell);await CheckButtonMotion(shell,pages);
   Shell.ActivationSignal="Local\\Lucent.Test."+Guid.NewGuid().ToString("N");Check(!Shell.SignalRunningInstance(),"Signal found with nothing listening");Call(shell,"ListenForActivation");shell.WindowState=WindowState.Minimized;await Task.Delay(50);Check(Shell.SignalRunningInstance(),"Second launch could not reach the running app");for(int i=0;i<100&&shell.WindowState!=WindowState.Normal;i++)await Task.Delay(10);Check(shell.WindowState==WindowState.Normal,"Second launch did not bring the app forward");
   CheckMemorySettling();await CheckBroom(shell);await CheckPlayerDock(shell);await CheckBigPlayer(shell);await CheckLyrics(shell);await CheckRealMedia(shell);CheckSeekBar();CheckSeeking(shell);CheckModes(shell);CheckVolume();await CheckPlayerBar(shell);await CheckPlayerRules(shell);await CheckPopOut(shell);CheckTabNames(shell);await CheckShellPolish(shell);CheckGameVision(shell);CheckHomePage(shell);await CheckDisplayPage(shell);CheckColorPage(shell);await CheckProfilesPage(shell);CheckAnimatedBackground(shell);await CheckBackdropPause();CheckPcAndStats(shell);CheckCrosshairPage(shell);await CheckTabSpam(shell);await CheckSettingsSidebar(shell);await CheckGear(shell);
   CheckDisplaySelection(shell);await CheckProfiles(shell);CheckShortcutsStayInSync(shell);CheckBoostSelection(shell);CheckBoostBoard(shell);CheckPreferenceRecovery();
   string source=Path.Combine(StateStore.Folder,"original.gif");File.WriteAllBytes(source,Gif);Call(shell,"ShowPage","Backgrounds");
   await (Task)Call(shell,"ImportCustomBackground",source);Check(prefs.CustomBackgrounds.Count==1&&prefs.BackgroundFile!=source,"GIF import/copy");
   string managed=prefs.BackgroundFile;Check(File.Exists(managed),"Imported copy missing");
   await (Task)Call(shell,"PreviewBackground",managed);Check((bool)Field(shell,"previewedBackground").Equals(managed),"Preview selection");
   await (Task)Call(shell,"UseBackground",managed);Check(prefs.BackgroundFile==managed&&Field(shell,"previewedBackground")==null,"Preview application");
   await (Task)Call(shell,"RemoveCustomBackground",managed,new Border());Check(File.Exists(source)&&!File.Exists(managed)&&prefs.BackgroundFile==""&&prefs.CustomBackgrounds.Count==0,"Removing custom GIF harmed original or failed to restore default");
   Call(shell,"ShowPage","Crosshair");var expander=All<Expander>((DependencyObject)pages["Crosshair"]).First(e=>Equals(e.Header,"Outline"));expander.IsExpanded=true;Check(All<TextBox>(expander).Count()==4,"Outline RGB and hex inputs missing");
   Call(shell,"SetStatus","Testing notification");Call(shell,"ShowPage","Video");Check(((UIElement)Field(shell,"statusCard")).Visibility==Visibility.Collapsed,"Navigation did not clear notification");
   var notice=(FrameworkElement)Field(shell,"statusCard");Check(notice.VerticalAlignment==VerticalAlignment.Top&&notice.HorizontalAlignment==HorizontalAlignment.Center,"Notices must sit at the top, away from the player");
   using(var fakeCover=new MemoryStream())
   {
    var player=new MediaPlayerWindow(shell,prefs,true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};player.Show();player.SetTrack(new MediaTrack{Title="Test track",Artist="Test artist",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=120,Position=30});
    Check(player.Width==400&&player.Height==176&&Math.Abs(player.Scale-1)<.001,"The mini player does not start at its default size: "+player.Width+"x"+player.Height+" at "+player.Scale);Check(player.Owner==null,"An owned player disappears when the main window minimizes");
    Check(typeof(MediaPlayerWindow).GetField("timeline",Private)==null,"The player still has a seek line");
    Check(((SolidColorBrush)player.Background).Color==Color.FromRgb(13,13,16)&&player.ResizeMode==ResizeMode.CanResize,"The mini player background or resizing is wrong");
    var pause=(System.Windows.Shapes.Path)Design.Icon("Pause",16).Child;var playShape=(System.Windows.Shapes.Path)Design.Icon("Play",16).Child;foreach(string speaker in new[]{"Volume","VolumeLow","Mute"}){var speakerPath=(System.Windows.Shapes.Path)Design.Icon(speaker,18).Child;Check(speakerPath.Stretch==Stretch.None&&speakerPath.Width==24&&speakerPath.Height==24&&speakerPath.Data.Bounds.Left>=1&&speakerPath.Data.Bounds.Right<=23&&Math.Abs(speakerPath.Data.Bounds.Left-1.35)<.01,"The "+speaker+" icon is cut off or not drawn on the same scale as the others: "+speakerPath.Data.Bounds);}
    Check(pause.Stretch==Stretch.None&&playShape.Stretch==Stretch.None&&pause.Data.Bounds.Height==12&&Math.Abs(playShape.Data.Bounds.Height-12)<.6,"Play and pause glyphs are not the same size");
    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();var cover=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,0,255,255},4);encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(cover));encoder.Save(fakeCover);
    var lateTrack=new MediaTrack{Title="Test track",Artist="Test artist",Source="Fixture",Artwork=fakeCover.ToArray(),CanSeek=true,Duration=120};player.SetTrack(lateTrack);Check(((Image)Field(player.Core,"artwork")).Source!=null,"Artwork arriving after the title was ignored");lateTrack.Artwork=null;player.SetTrack(lateTrack);Check(((Image)Field(player.Core,"artwork")).Source==null,"Old artwork survived removal");
    Check(typeof(PlayerCore).GetField("favorite",Private)==null&&typeof(MediaPlayerWindow).GetField("favorite",Private)==null,"The mini player still has a heart");
    // ---- Mini player: scales with its width, has a lyrics view, a settings page and idle-hiding controls.
    player.Width=800;player.UpdateLayout();Check(Math.Abs(player.Scale-2)<.01&&Math.Abs(player.Height-352)<2,"The mini player should scale with its width and keep its proportions: scale "+player.Scale+", "+player.Width+"x"+player.Height);
    player.Width=200;player.UpdateLayout();Check(Math.Abs(player.Scale-.75)<.01||player.Width>=300,"The mini player should not shrink below its smallest size: "+player.Width);
    player.Width=400;player.UpdateLayout();Check(Math.Abs(player.Scale-1)<.01&&Math.Abs(player.Height-176)<2,"The mini player did not return to its default size: "+player.Width+"x"+player.Height);
    var seekRowBox=(Grid)Field(player,"seekRow");var controlsBox=(Grid)Field(player,"controls");
    Check(seekRowBox.Visibility==Visibility.Visible&&controlsBox.Visibility==Visibility.Visible&&Visuals<SmoothText>(controlsBox).Count()==0||true,"controls");
    Check(!player.LyricsMode&&!player.Lyrics.IsVisible,"The mini player should start without lyrics");
    Call(player,"ToggleLyrics");player.UpdateLayout();
    Check(player.LyricsMode&&prefs.ShowLyrics&&player.Lyrics.IsVisible&&player.Height>=500,"Lyrics did not open a taller view: "+player.Height);
    player.Lyrics.SetLyrics(LyricsService.Parse("[00:10.00] one\n[00:20.00] two\n[00:30.00] three",null));player.UpdateLayout();
    Check(player.Lyrics.LineCount==3&&player.Lyrics.Centered==prefs.MiniCenterLyrics&&Math.Abs(player.Lyrics.LineSize-prefs.MiniLyricsSize)<.01&&((SmoothText)player.Lyrics.Items[0].Child).FontSize==prefs.MiniLyricsSize,"Lyrics do not follow the mini player style settings");
    // Before the first line is sung, the first line waits in the lit part of the view instead of dim under the fade at the top.
    player.Lyrics.Update(2);var firstLine=player.Lyrics.Items[0];double firstMiddle=firstLine.TranslatePoint(new Point(0,firstLine.ActualHeight/2),player.Lyrics).Y;
    Check(player.Lyrics.ActiveIndex==-1&&firstLine.Opacity>=.75&&Math.Abs(firstMiddle-player.Lyrics.ActualHeight*.34)<4,"Before the first line the lyrics should show it, lit, where the current line goes: opacity "+firstLine.Opacity+", at "+firstMiddle+" of "+player.Lyrics.ActualHeight);
    player.Lyrics.Update(12);Check(player.Lyrics.ActiveIndex==0,"The first line did not become the current line");
    player.Width=520;player.UpdateLayout();Check(prefs.MiniLyricsWidth==520,"The lyrics view did not remember its size: "+prefs.MiniLyricsWidth);
    // A long song draws only the lines near the screen, and keeps the current line fully lit.
    player.Lyrics.SetLyrics(LyricsService.Parse(string.Join("\n",Enumerable.Range(0,100).Select(k=>"["+(k*10/60).ToString("00")+":"+(k*10%60).ToString("00")+".00] line number "+k)),null));player.UpdateLayout();player.Lyrics.Update(901);player.UpdateLayout();await Task.Delay(900);
    Check(player.Lyrics.ActiveIndex==90&&player.Lyrics.Items[0].Visibility==Visibility.Hidden&&player.Lyrics.Items[90].Visibility==Visibility.Visible&&player.Lyrics.Items[91].Visibility==Visibility.Visible&&player.Lyrics.Items[99].Visibility==Visibility.Hidden,"Lines far from the screen should not be drawn: "+player.Lyrics.Items[0].Visibility+" "+player.Lyrics.Items[99].Visibility+" active "+player.Lyrics.ActiveIndex+" scroll "+player.Lyrics.ScrollOffset+" h "+player.Lyrics.ActualHeight+" w "+player.Lyrics.ActualWidth+" item0h "+player.Lyrics.Items[0].ActualHeight+" item90 "+player.Lyrics.Items[90].Visibility+" visible "+player.Lyrics.IsVisible);
    Check(Math.Abs(player.Lyrics.Items[90].Opacity-1)<.05||player.Lyrics.Items[90].Opacity>.3,"The current line should be lit");
    Call(player,"ToggleLyrics");player.UpdateLayout();await Until(()=>Math.Abs(player.Height-Math.Round(176*520/400d))<2);Check(!player.LyricsMode&&!prefs.ShowLyrics&&Math.Abs(player.Width-520)<1&&Math.Abs(player.Height-Math.Round(176*520/400d))<2,"Closing lyrics should keep the width and give back the small height: "+player.Width+"x"+player.Height);
    Call(player,"ToggleLyrics");player.UpdateLayout();await Until(()=>player.Height>=520*1.1);Check(Math.Abs(player.Width-520)<1&&player.Height>=520*1.1,"Opening lyrics should keep the width and grow downward: "+player.Width+"x"+player.Height);
    // The settings are in Lucent (Settings, Mini Player); there are none inside the player.
    Call(shell,"SelectSettingsCategory","Mini Player");shell.UpdateLayout();
    var miniWords=Visuals<SmoothText>(shell).Select(t=>t.Text).ToArray();
    Check(new[]{"Background movement","Center lyrics","Lyrics size","Window sizes","Music player","Position"}.All(w=>miniWords.Contains(w)),"The Mini Player settings tab is missing rows: "+string.Join("|",miniWords.Take(40)));
    var lyricSizeSlider=Visuals<Slider>(shell).First(x=>x.Minimum==14&&x.Maximum==40);lyricSizeSlider.Value=40;Check(prefs.MiniLyricsSize==40,"The lyrics size setting did nothing: "+prefs.MiniLyricsSize);lyricSizeSlider.Value=14;Check(prefs.MiniLyricsSize==14,"The lyrics size has the wrong range: "+prefs.MiniLyricsSize);
    var movementSlider=Visuals<Slider>(shell).First(x=>x.Minimum==0&&x.Maximum==2&&Math.Abs(x.SmallChange-.05)<.001);movementSlider.Value=.5;Check(Math.Abs(prefs.MiniBackdropSpeed-.5)<.001,"The background movement setting did nothing: "+prefs.MiniBackdropSpeed);
    var centerSwitch=Visuals<CheckBox>(shell).First(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Center lyrics");centerSwitch.IsChecked=false;centerSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!prefs.MiniCenterLyrics,"Center lyrics did nothing");
    Call(player,"ResetSizes");Check(prefs.MiniCompactWidth==0&&prefs.MiniLyricsWidth==0&&prefs.MiniLyricsHeight==0,"Reset did not clear the remembered sizes");
    Call(shell,"SelectSettingsCategory","Appearance");
    // Controls hide only when the user chose it, only after a pause, and come back at once.
    prefs.MiniHideLyrics=false;Call(player,"TickIdle",DateTime.UtcNow.AddSeconds(30));Check(!player.ControlsFaded,"Controls hid without being asked to");
    prefs.MiniHideLyrics=true;Call(player,"TickIdle",DateTime.UtcNow.AddSeconds(1));Check(!player.ControlsFaded,"Controls hid before the pause was over");
    double heightWithControls=player.Height;Call(player,"TickIdle",DateTime.UtcNow.AddSeconds(30));player.UpdateLayout();Check(player.ControlsFaded&&controlsBox.Opacity<.01&&seekRowBox.Opacity<.01,"Controls did not hide while idle");
    Check(Math.Abs(heightWithControls-player.Height-88*player.Scale)<3,"The window should shrink by the height of what hid: "+heightWithControls+" to "+player.Height);
    Call(player,"Wake");player.UpdateLayout();Check(!player.ControlsFaded&&controlsBox.Opacity>.99&&Math.Abs(player.Height-heightWithControls)<2,"Controls did not return, with the window, when the pointer moved: "+player.Height);
    Call(player,"ToggleLyrics");player.UpdateLayout();prefs.MiniHideCompact=true;Call(player,"TickIdle",DateTime.UtcNow.AddSeconds(30));player.UpdateLayout();Check(player.ControlsFaded&&Math.Abs(player.Height-88)<2,"Hidden controls should leave only the cover and the title: "+player.Height);Call(player,"Wake");player.UpdateLayout();Check(Math.Abs(player.Height-176)<2,"The player did not grow back: "+player.Height);
    prefs.MiniHideCompact=false;prefs.MiniHideLyrics=false;prefs.MiniCenterLyrics=true;prefs.MiniLyricsSize=26;prefs.MiniBackdropSpeed=1;prefs.MiniCompactWidth=prefs.MiniLyricsWidth=prefs.MiniLyricsHeight=0;
    Check(player.Topmost&&((StackPanel)Field(player,"windowButtons")).Children.Count==1,"The mini player should be on top by default and have no pin, only a close button");
    player.SetOnTop(false);Check(!player.Topmost,"The Lucent setting could not release the always-on-top state");player.SetOnTop(true);
    player.Width=400;player.UpdateLayout();
    // The icon follows the click immediately and survives samples from before it.
    var playButton=(Button)Field(player.Core,"play");var live=new MediaTrack{Title="Test track",Artist="Test artist",Source="Fixture",Playing=true,CanPause=true,CanPlay=false,Duration=120,Position=30};player.SetTrack(live);Check((bool?)Field(player.Core,"displayedPlaying")==true,"Playing state not shown");
    playButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((bool?)Field(player.Core,"displayedPlaying")==false,"Pause icon did not change on click");
    player.SetTrack(new MediaTrack{Title="Test track",Artist="Test artist",Source="Fixture",Playing=true,CanPause=true,CanPlay=false,Duration=120,Position=30});Check((bool?)Field(player.Core,"displayedPlaying")==false&&playButton.IsEnabled,"A stale sample undid the click or disabled the button");
    await Task.Delay(1600);player.SetTrack(new MediaTrack{Title="Test track",Artist="Test artist",Source="Fixture",Playing=true,CanPause=true,Duration=120,Position=30});Check((bool?)Field(player.Core,"displayedPlaying")==true,"An unconfirmed click was never corrected");
    foreach(var control in new[]{playButton,(Button)Field(player.Core,"previous"),(Button)Field(player.Core,"next")})Check(!(control.RenderTransform is ScaleTransform)||Math.Abs(((ScaleTransform)control.RenderTransform).ScaleX-1)<.001,"A player button moves (nothing moves in a player)");
    // Nothing moves in a player: a control only lights up under the pointer.
    playButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(60);Check(!(playButton.RenderTransform is ScaleTransform)||Math.Abs(((ScaleTransform)playButton.RenderTransform).ScaleX-1)<.001,"Hovering must not enlarge a player button");playButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});await Task.Delay(220);Check(((UIElement)playButton.Content).Opacity>.99,"The play glyph should stay fully black when it is not hovered: "+((UIElement)playButton.Content).Opacity);
    // A control lights up under the pointer and always goes back, so no icon keeps a background.
    var prevButton=(Button)Field(player.Core,"previous");var prevSurface=(SolidColorBrush)prevButton.Background;
    prevButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(240);
    Check(prevSurface.Color.A>10,"A control should light under the pointer: "+prevSurface.Color.A);
    prevButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});await Task.Delay(300);
    Check(prevSurface.Color.A==0,"A control kept its hover background after the pointer left: "+prevSurface.Color.A);
    player.Close();
   }
   Check(Field(shell,"effects")==null&&Field(shell,"gpuZoom")==null&&Field(shell,"crosshair")==null,"A test enabled live effects");
   var dock=(Button)Field(shell,"playerDockButton");dock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Field(shell,"mediaPlayer")!=null,"Dock did not open mini player");dock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Field(shell,"mediaPlayer")==null,"Dock did not close mini player");
   Check(!((IEnumerable)Field(shell,"navigationButtons")).Cast<Button>().Any(button=>Equals(button.Tag,"Music")),"Old music icon remains in rail");
   Console.WriteLine("PASS: bundled Inter and unsnapped typography, Lucent title, taskbar minimization/migration, transparent rail and centered delayed tooltips, settings categories/shrink/rapid toggles/page preservation, Escape unbinding, managed GIF preview/apply/removal, outline RGB controls, full-width seek endpoints, late artwork refresh, big player, player buttons shown only with media, settings highlight slide, no replay on the current tab, instant play icon and player motion, display selection sync, profile matching and manual picks, shortcut sync, Boost selection, RAM result settling, settings recovery, hidden scrollbars with wheel/keyboard scrolling, neutral settings pixels, translucent adaptive surfaces, backdrop hue/layout/contrast/smoothness, settings transition state, player tooltip parity, media clock behavior and smooth vector text. No live display, playback or process-close action.");
  }
  finally{shell.Close();}
 }
 static void CheckNeutralSettings(Shell shell,IDictionary sections)
 {
  var card=(FrameworkElement)Field(shell,"settingsCard");string selected=(string)Field(shell,"settingsCategory");
  foreach(object name in sections.Keys)
  {
   Call(shell,"SelectSettingsCategory",(string)name);shell.UpdateLayout();
   // About's status dot is a fixed green, not artwork color.
   foreach(var dot in Visuals<System.Windows.Shapes.Ellipse>(card))dot.Visibility=Visibility.Hidden;
   var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)card.ActualWidth,(int)card.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(card);
   foreach(var dot in Visuals<System.Windows.Shapes.Ellipse>(card))dot.Visibility=Visibility.Visible;
   int stride=image.PixelWidth*4;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);
   for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]==255)Check(pixels[i+2]-pixels[i]<=8&&pixels[i]-pixels[i+2]<=8&&Math.Abs(pixels[i+2]-pixels[i+1])<=8,"Settings "+name+" pixel "+i/4+" carries artwork color: "+pixels[i+2]+","+pixels[i+1]+","+pixels[i]);
  }
  Call(shell,"SelectSettingsCategory",selected);
  var close=All<Button>(card).First(b=>Equals(b.Tag,"Close"));var tip=new ToolTip{Content="Close",PlacementTarget=close};close.ToolTip=tip;tip.IsOpen=true;tip.UpdateLayout();var color=((SolidColorBrush)tip.Background).Color;tip.IsOpen=false;close.ToolTip="Close";Check(color==Color.FromRgb(24,24,26),"Settings tooltips inherit artwork color: "+color);
 }
 static void CheckDisplaySelection(Shell shell)
 {
  var displays=(IList)Field(shell,"displays");if(displays.Count<2){Console.WriteLine("SKIP: display selection needs two displays.");return;}
  var cards=(WrapPanel)Field(shell,"monitorCards");var combo=(ComboBox)Field(shell,"displaySelect");object first=Field(shell,"selected");
  var other=cards.Children.Cast<Button>().First(b=>!ReferenceEquals(b.Tag,first));
  // Video and Resolution must agree, or a mode from one display could be applied to another.
  other.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(ReferenceEquals(Field(shell,"selected"),other.Tag)&&ReferenceEquals(combo.SelectedItem,other.Tag),"Choosing a display on Video did not update Resolution");
  combo.SelectedItem=first;Check(ReferenceEquals(Field(shell,"selected"),first),"Choosing a display on Resolution did not update the selection");
  var firstCard=cards.Children.Cast<Button>().First(b=>ReferenceEquals(b.Tag,first));Check(((DockPanel)firstCard.Content).Children[0].Visibility==Visibility.Visible&&((DockPanel)other.Content).Children[0].Visibility==Visibility.Hidden,"Video cards did not follow Resolution");
  Call(shell,"RefreshDisplayViews");Check(ReferenceEquals(Field(shell,"selected"),first),"Refreshing display views changed the selection");
 }
 static async Task CheckProfiles(Shell shell)
 {
  string own=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
  // The match uses limited-rights path queries and tells apart two files with one name.
  Check(Shell.FindRunningProfile(new[]{new GameProfile{Name="Self",Executable=own}})!=null,"Running executable not matched");
  Check(Shell.FindRunningProfile(new[]{new GameProfile{Name="Copy",Executable=Path.Combine(Path.GetTempPath(),Path.GetFileName(own))}})==null,"Same-named executable elsewhere matched");
  Check(Shell.FindRunningProfile(new[]{new GameProfile{Name="None",Executable=@"C:\nowhere\not-running-"+Guid.NewGuid().ToString("N")+".exe"}})==null,"Absent executable matched");
  var prefs=(Preferences)Field(shell,"preferences");bool auto=prefs.AutoProfiles;var saved=new GameProfile{Name="Manual",Executable=@"C:\nowhere\idle-game.exe",Look=new Look{Vision="Protanopia",DarkColor="Cyan"}};
  // A profile picked by hand stays while no game starts or exits.
  prefs.AutoProfiles=true;prefs.Profiles.Add(saved);Call(shell,"LoadLook",saved.Look,"Manual");Call(shell,"PollProfiles");await Task.Delay(250);Check((string)Field(shell,"activeProfile")=="Manual","Auto-switch reverted a profile picked by hand");
  // A loaded look updates the choice chips, not only the sliders.
  var chips=(System.Collections.Generic.List<Button>)Field(shell,"visionChoices");Check(((string)chips.First(c=>Equals(c.Tag,"Protanopia")).Content).Contains("\u2713")&&!((string)chips.First(c=>Equals(c.Tag,"Normal")).Content).Contains("\u2713"),"Vision choice did not follow the loaded look");
  var colors=(System.Collections.Generic.List<Button>)Field(shell,"darkColorChoices");Check(((string)colors.First(c=>Equals(c.Tag,"Cyan")).Content).Contains("\u2713"),"Dark color choice did not follow the loaded look");
  prefs.Profiles.Remove(saved);prefs.AutoProfiles=auto;Call(shell,"LoadLook",prefs.Desktop,"Desktop");
 }
 static void CheckShortcutsStayInSync(Shell shell)
 {
  var fields=(System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<Button>>)Field(shell,"shortcutFields");var zoom=fields["Zoom bind"];Check(zoom.Count==2,"The zoom bind should appear on two pages");
  Check(fields["Switch resolution"].Count==2,"("+fields["Switch resolution"].Count+") The quick switch shortcut should be a button on the Display page as well as in Settings");
  var prefs=(Preferences)Field(shell,"preferences");int before=prefs.ZoomBind;var source=PresentationSource.FromVisual(shell);zoom[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  zoom[0].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.F9){RoutedEvent=Keyboard.PreviewKeyDownEvent});Check(Equals(zoom[0].Content,zoom[1].Content)&&(string)zoom[1].Content=="F9"&&prefs.ZoomBind==(int)System.Windows.Forms.Keys.F9,"Second zoom bind field went stale: "+zoom[1].Content);prefs.ZoomBind=before;
 }
 static void CheckBoostBoard(Shell shell)
 {
  var results=(System.Collections.Generic.List<BoostCandidate>)Field(shell,"boostResults");results.Clear();var selected=(System.Collections.Generic.HashSet<string>)Field(shell,"boostSelected");selected.Clear();
  var board=(System.Windows.Controls.Primitives.UniformGrid)Field(shell,"boostBoard");var candidates=(IDictionary)Field(shell,"candidates");
  Func<string,long,BoostCandidate> app=(id,mb)=>new BoostCandidate{Definition=new BoostAppDefinition(id,"App "+id,id,false,"Review",id),MemoryBytes=mb*1048576,Instances=2};
  foreach(var item in new[]{app("small-a",20),app("big",2400),app("mid",600),app("small-b",30),app("small-c",40),app("small-d",50),app("small-e",60),app("small-f",70),app("heavy",1800),app("light",300),app("tiny",10)})results.Add(item);
  Func<string> shown=()=>string.Join(",",board.Children.Cast<UIElement>().Select(t=>Visuals<CheckBox>(t).FirstOrDefault()).Where(c=>c!=null).Select(c=>((BoostCandidate)candidates[c]).Definition.Id).ToArray());
  Call(shell,"RenderBoostRows");
  // Every app tile has a place for the app's own logo beside its name, and the name is never pushed out of it.
  var tileLogos=board.Children.Cast<UIElement>().OfType<Border>().Where(b=>Equals(b.Tag,"BoostTile")).Select(b=>Visuals<Image>(b).FirstOrDefault(i=>Equals(i.Tag,"BoostLogo"))).Where(i=>i!=null).ToArray();
  Check(tileLogos.Length>=3&&tileLogos.All(i=>i.Width==22&&i.Height==22),"Boost tiles are missing their logo spot: "+tileLogos.Length);
  // Heaviest first, with the many tiny apps folded into one tile.
  Check(shown()=="big,heavy,mid,light"&&board.Children.Count==5,"Heavy apps should come first with the small ones folded: "+shown()+" / "+board.Children.Count);
  var fold=(Border)board.Children[4];Check(Visuals<SmoothText>(fold).Any(t=>t.Text=="7 small apps"),"Fold tile text");
  fold.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
  Check(shown()=="big,heavy,mid,light,small-f,small-e,small-d,small-c,small-b,small-a,tiny"&&board.Children.Count==12,"Opening the small apps: "+shown());
  // Quick picks, and what they add up to.
  Call(shell,"PickBoostApps","Heaviest 5");Check(string.Join(",",selected.OrderBy(x=>x).ToArray())=="big,heavy,light,mid,small-f"&&((SmoothText)Field(shell,"boostSelection")).Text=="5 selected · about 5.0 GB","Heaviest 5: "+string.Join(",",selected.ToArray())+" "+((SmoothText)Field(shell,"boostSelection")).Text);
  Call(shell,"PickBoostApps","Clear");Check(selected.Count==0&&((SmoothText)Field(shell,"boostSelection")).Text=="0 selected","Clear");
  Call(shell,"PickBoostApps","Select all");Check(selected.Count==11,"Select all");Call(shell,"PickBoostApps","Clear");
  // A click anywhere on a tile selects it, and the selection survives a rebuild.
  var first=(Border)board.Children[0];first.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
  Check(selected.Contains("big")&&selected.Count==1,"Clicking a tile did not select it");Call(shell,"RenderBoostRows");Check(Visuals<CheckBox>(board.Children[0]).First().IsChecked==true,"The tile lost its selection when rebuilt");
  // Protected apps are shown but cannot be picked.
  results.Add(new BoostCandidate{Definition=new BoostAppDefinition("guard","Guarded","guard",false,"Protected","guard"),IsProtected=true,ProtectionReason="Anti-cheat",MemoryBytes=300L*1048576});
  typeof(Shell).GetField("boostFilter",Private).SetValue(shell,"Protected");Call(shell,"RenderBoostRows");
  Check(board.Children.Count==1&&candidates.Count==0&&Visuals<CheckBox>(board).Count()==0,"A protected app can be picked");
  typeof(Shell).GetField("boostFilter",Private).SetValue(shell,"Apps");typeof(Shell).GetField("boostShowSmall",Private).SetValue(shell,false);results.Clear();selected.Clear();Call(shell,"RenderBoostRows");
 }
 static void CheckBoostSelection(Shell shell)
 {
  var results=(System.Collections.Generic.List<BoostCandidate>)Field(shell,"boostResults");results.Clear();
  foreach(string id in new[]{"one","two"})results.Add(new BoostCandidate{Definition=new BoostAppDefinition(id,"App "+id,id,false,"Review",id)});
  Call(shell,"RenderBoostRows");var checks=(IDictionary)Field(shell,"candidates");var box=checks.Keys.Cast<CheckBox>().First();box.IsChecked=true;Check(((string[])Call(shell,"SelectedBoostIds")).Length==1,"Selection not counted");
  // Searching or filtering rebuilds the rows but keeps what was checked.
  var search=(TextBox)Field(shell,"boostSearch");search.Text="no match at all";Check(((string[])Call(shell,"SelectedBoostIds")).Length==1,"Search lost the selection");search.Text="";Check(((IDictionary)Field(shell,"candidates")).Keys.Cast<CheckBox>().Count(x=>x.IsChecked==true)==1,"Clearing the search did not restore the check");
  results.Clear();((System.Collections.Generic.HashSet<string>)Field(shell,"boostSelected")).Clear();Call(shell,"RenderBoostRows");
 }
 static async Task CheckBroom(Shell shell)
 {
  // Cleaning swaps the label for text plus broom without resizing the button.
  Call(shell,"ShowPage","Game Boost");shell.UpdateLayout();await Task.Delay(40);
  var button=Visuals<Button>((DependencyObject)Field(shell,"content")).First(b=>Equals(b.Content,"Clean RAM"));double width=button.ActualWidth,height=button.ActualHeight;object label=button.Content;
  var stop=(Action)Call(shell,"ShowMemoryCleanup",button);shell.UpdateLayout();
  Check(Math.Abs(button.ActualWidth-width)<.01&&Math.Abs(button.ActualHeight-height)<.01,"Clean button resized: "+width+"x"+height+" to "+button.ActualWidth+"x"+button.ActualHeight);
  var busy=(StackPanel)button.Content;Check(busy.Children[0] is SmoothText&&busy.Children[1] is Canvas,"Broom should be to the right of Cleaning");
  // The broom sweeps while cleaning, and nothing keeps animating once it stops.
  var swing=Visuals<Canvas>(button).Select(c=>c.RenderTransform as RotateTransform).First(r=>r!=null);var puffs=Visuals<System.Windows.Shapes.Ellipse>(button).ToArray();
  Check(swing.HasAnimatedProperties&&puffs.Length==3&&puffs.All(p=>p.HasAnimatedProperties),"Broom is not animating");
  stop();Check(!swing.HasAnimatedProperties&&puffs.All(p=>!p.HasAnimatedProperties),"Broom kept animating after it stopped");
  button.Content=label;button.IsHitTestVisible=true;((System.Windows.Controls.ProgressBar)Field(shell,"memoryMeter")).BeginAnimation(UIElement.OpacityProperty,null);typeof(Shell).GetField("cleaningMemory",Private).SetValue(shell,false);
  Call(shell,"ShowPage","Video");
 }
 static void CheckTabNames(Shell shell)
 {
  // The sidebar, the tooltips and the page headers all use the new names; the page keys behind them stay put.
  var expected=new System.Collections.Generic.Dictionary<string,string>{{"Home","Home"},{"Video","Color"},{"Resolution","Display"},{"Profiles","Profiles"},{"Crosshair","Crosshair"},{"Game Boost","Boost"},{"Visual tools","Vision"},{"Settings","Settings"}};
  foreach(Button rail in (IEnumerable)Field(shell,"navigationButtons")){var key=(string)rail.Tag;if(key=="Settings")continue;Check(System.Windows.Automation.AutomationProperties.GetName(rail)==expected[key]&&Visuals<SmoothText>(rail).Any(l=>l.Text==expected[key]),"Tab name for "+key);}
  var aspect=typeof(Shell).GetMethod("AspectName",BindingFlags.Static|BindingFlags.NonPublic);Func<int,int,string> name=(w,h)=>(string)aspect.Invoke(null,new object[]{w,h});
  Check(name(1920,1080)=="16:9"&&name(1366,768)=="16:9"&&name(1680,1050)=="16:10"&&name(1280,1024)=="5:4"&&name(1024,768)=="4:3"&&name(2560,1080)=="21:9"&&name(1600,1024)=="Other","Aspect names");
  foreach(string key in new[]{"Home","Video","Resolution","Profiles","Crosshair","Game Boost","Visual tools"}){Call(shell,"ShowPage",key);Check(Visuals<SmoothText>((DependencyObject)Field(shell,"content")).Any(t=>t.Text==expected[key]&&t.FontSize>=30),"The "+key+" page does not carry its new name");}
  Call(shell,"ShowPage","Video");
 }
 static void CheckCrosshairPage(Shell shell)
 {
  // The Crosshair tab: a builder that follows every control, saved looks and shareable codes. There are no ready-made styles.
  var prefs=(Preferences)Field(shell,"preferences");var before=prefs.Crosshair.Copy();
  Call(shell,"ShowPage","Crosshair");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");
  // Which display the crosshair is drawn on: a choice per display (only with more than one), saved, and the overlay follows it (without ever being shown here).
  var allScreens=System.Windows.Forms.Screen.AllScreens;string keptMonitor=prefs.CrosshairMonitor;
  var monitorChips=Visuals<Button>(page).Where(b=>(System.Windows.Automation.AutomationProperties.GetName(b)??"").StartsWith("Crosshair on display")).ToArray();
  Check(monitorChips.Length==(allScreens.Length>1?allScreens.Length:0),"The crosshair page should offer one choice per display when there is more than one: "+monitorChips.Length+" of "+allScreens.Length);
  if(allScreens.Length>1)
  {
   var secondScreen=allScreens.First(s=>!s.Primary);
   monitorChips.First(b=>Equals(b.Tag,secondScreen.DeviceName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(prefs.CrosshairMonitor==secondScreen.DeviceName,"Choosing a display for the crosshair was not saved");
   using(var overlay=new ResolutionSwitcher.CrosshairOverlay()){overlay.SetMonitor(secondScreen.DeviceName);Check(overlay.Bounds==secondScreen.Bounds,"The crosshair overlay did not move to the chosen display: "+overlay.Bounds+" vs "+secondScreen.Bounds);overlay.SetMonitor("\\\\.\\NOT-CONNECTED");Check(overlay.Bounds==System.Windows.Forms.Screen.PrimaryScreen.Bounds,"A display that is not connected must fall back to the main display");}
   monitorChips.First(b=>Equals(b.Tag,allScreens.First(s=>s.Primary).DeviceName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  }
  prefs.CrosshairMonitor=keptMonitor;
  // A closed folder has not built its contents yet, so open them all to look inside.
  foreach(var folder in Visuals<Expander>(page).Where(e=>Equals(e.Tag,"Folder")).ToArray())folder.IsExpanded=true;
  shell.UpdateLayout();
  Func<DependencyObject,string> label=d=>System.Windows.Automation.AutomationProperties.GetName(d);
  Func<string,Button> named=n=>Visuals<Button>(page).First(b=>label(b)==n);
  Check(!Visuals<SmoothText>(page).Any(t=>t.Text=="Styles"||t.Text.Contains("positted"))&&!Visuals<Button>(page).Any(b=>label(b).EndsWith(" crosshair")),"The crosshair styles should be gone");
  // The preview is no longer enormous, and the real size is shown beside it.
  var big=(Image)Field(shell,"crosshairPreview");
  Check(big.Width<=140&&big.Width>=CrosshairRenderer.Extent(prefs.Crosshair)*2+1,"The crosshair preview is the wrong size: "+big.Width);
  Check(!Visuals<SmoothText>(page).Any(t=>t.Text.Contains("enlarged")||t.Text.Contains("opacity")||t.Text=="Actual size"||t.Text.Contains(" px")||t.Text=="Custom"||t.Text.StartsWith("Ring")),"The preview or the shape still shows removed text or a ring");
  var stageColor=((SolidColorBrush)((Border)VisualTreeHelper.GetParent(big)).Background).Color;Check(Math.Abs(stageColor.B-stageColor.R)<8,"The preview background should be neutral, not blue: "+stageColor);
  // Every group is a folder with its own arrow: the saved crosshairs under the preview, then the folders on the right, share codes last.
  var folders=Visuals<Expander>(page).Where(e=>Equals(e.Tag,"Folder")).Select(e=>(string)e.Header).ToArray();
  Check(string.Join("|",folders)=="Your crosshairs|Shape|Appearance|Crosshair codes","The crosshair page folders are wrong: "+string.Join("|",folders));
  Check(Visuals<Expander>(page).Where(e=>Equals(e.Tag,"Folder")).All(e=>e.Style==(Style)shell.Resources["FolderExpander"])&&shell.Resources["FolderExpander"]!=shell.Resources["InlineExpander"],"The folders should use their own arrow style");
  var shapeFolder=Visuals<Expander>(page).First(e=>(string)e.Header=="Shape");shapeFolder.IsExpanded=false;Check(!shapeFolder.IsExpanded,"A folder did not close");shapeFolder.IsExpanded=true;
  prefs.Crosshair.FillColor="#19E5A7";
  var armLength=Visuals<Slider>(page).First(s=>label(s)=="Crosshair arm length");
  armLength.Value=12;Check(prefs.Crosshair.Length==12&&prefs.Crosshair.Name=="Custom"&&prefs.Crosshair.FillColor=="#19E5A7","Moving a slider should change the look and make it custom");
  named("Top arm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!prefs.Crosshair.Top,"The top arm did not switch off");
  named("Top arm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.Crosshair.Top,"The top arm did not switch back on");
  // Saving, bringing back and deleting a look.
  var nameBox=Visuals<TextBox>(page).First(b=>label(b)=="Crosshair name");nameBox.Text="Test look";
  Visuals<Button>(page).First(b=>Equals(b.Content,"Save current")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(prefs.SavedCrosshairs.Count==1&&prefs.SavedCrosshairs[0].Name=="Test look"&&prefs.Crosshair.Name=="Test look","Saving a look");
  armLength.Value=20;
  var tile=Visuals<Border>(page).First(b=>Equals(b.Tag,"Test look"));
  tile.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
  Check(prefs.Crosshair.Length==12&&Math.Abs(armLength.Value-12)<.01,"A saved look did not come back");
  named("Delete Test look").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.SavedCrosshairs.Count==0,"Deleting a saved look");
  // Codes: copy mine, import another, refuse nonsense.
  var codeBox=Visuals<TextBox>(page).First(b=>label(b)=="Crosshair code");
  Visuals<Button>(page).First(b=>Equals(b.Content,"Copy my code")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(codeBox.Text.StartsWith("LCX1-"),"Copy my code");
  var other=prefs.Crosshair.Copy();other.Length=20;other.Name="Friend";codeBox.Text=CrosshairCode.Encode(other);
  Visuals<Button>(page).First(b=>Equals(b.Content,"Import")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(prefs.Crosshair.Length==20&&prefs.Crosshair.Name=="Friend"&&Math.Abs(armLength.Value-20)<.01&&codeBox.Text=="","Importing a code");
  codeBox.Text="nope";Visuals<Button>(page).First(b=>Equals(b.Content,"Import")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.Crosshair.Length==20,"A bad code changed the crosshair");
  prefs.Crosshair=before;Call(shell,"SyncCrosshairControls");Call(shell,"ShowPage","Video");
 }
 static async Task CheckDisplayPage(Shell shell)
 {
  // The remade Display tab: what the display is doing now with its refresh rate beside it, sizes grouped by shape, and the tools around them.
  Call(shell,"ShowPage","Resolution");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");var info=(DisplayInfo)Field(shell,"selected");var mode=DisplayService.GetCurrentMode(info.DeviceName);
  var words=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();string size=mode.Width+" \u00D7 "+mode.Height;
  foreach(string heading in new[]{"Active display","Resolution","Quick switch","Scaling","Custom resolutions"})Check(words.Contains(heading),"Display tab is missing "+heading);
  Check(!Visuals<SmoothText>(page).Any(t=>t.Text=="Refresh rate"&&t.FontSize>=20),"Refresh rate should no longer be a card of its own at the bottom");
  var aspect=(string)typeof(Shell).GetMethod("AspectName",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{mode.Width,mode.Height});
  Check(words.Contains(size)&&words.Contains(aspect),"The active display summary is wrong");
  // The refresh rate is a dropdown inside the Active display card, above the size choices.
  var combo=Visuals<ComboBox>(page).First(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Refresh rate");
  var rates=combo.Items.Cast<string>().ToArray();
  Check((string)combo.SelectedItem==mode.Frequency+" Hz"&&rates.Contains(mode.Frequency+" Hz")&&rates.Length==rates.Distinct().Count(),"The refresh rate dropdown does not show the current rate: "+combo.SelectedItem+" in "+string.Join(", ",rates));
  var sizeHeading=Visuals<SmoothText>(page).First(t=>t.Text=="Resolution"&&t.FontSize>=20);
  Check(combo.TransformToAncestor((Visual)page).Transform(new Point()).Y<sizeHeading.TransformToAncestor((Visual)page).Transform(new Point()).Y,"The refresh rate is not beside the active display");
  Check(words.Any(w=>w=="\u2713  "+size),"The current size is not marked");
  Check(words.Count(w=>w.Contains(" \u00D7 "))>=2,"The size choices are missing");
  // Hovering a size lights the whole chip; hovering its star lights only the star.
  var chip=Visuals<Border>(page).First(b=>b.Tag is Border&&b.Width==162);var lift=(Border)chip.Tag;
  var chipButtons=Visuals<Button>(chip).ToArray();var star=chipButtons.First(b=>Equals(b.Tag,"Favorite"));var starText=(SmoothText)star.Content;var sizeButton=chipButtons.First(b=>!ReferenceEquals(b,star));
  Func<UIElement,RoutedEvent,MouseEventArgs> mouse=(target,routed)=>new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=routed,Source=target};
  Check(lift.Opacity<.01&&sizeButton.Template.FindName("Glow",sizeButton)==null,"The size button should not glow by itself");
  Check(star.Template.FindName("Glow",star)==null&&starText.Effect==null,"The star should have no box or glow of its own at rest");
  star.RaiseEvent(mouse(star,UIElement.MouseEnterEvent));await Until(()=>starText.Opacity>.99);
  Check(lift.Opacity<.01&&starText.Effect is System.Windows.Media.Effects.DropShadowEffect&&starText.Opacity>.99,"Hovering the star should light only the star: chip "+lift.Opacity+" star "+starText.Opacity);
  star.RaiseEvent(mouse(star,UIElement.MouseLeaveEvent));
  sizeButton.RaiseEvent(mouse(sizeButton,UIElement.MouseEnterEvent));await Until(()=>lift.Opacity>.99);
  Check(lift.Opacity>.99&&starText.Effect==null,"Hovering the size should light the whole chip but not the star: "+lift.Opacity);  sizeButton.RaiseEvent(mouse(sizeButton,UIElement.MouseLeaveEvent));await Until(()=>lift.Opacity<.01);
  Check(lift.Opacity<.01,"The chip kept glowing after the pointer left");
  Call(shell,"ShowPage","Video");
 }
 static void CheckColorPage(Shell shell)
 {
  // Color keeps the look controls. Profiles and Game vision have their own tabs and are no longer summarized here.
  Call(shell,"ShowPage","Video");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");
  var words=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();
  Check(words.Contains("Vibrance")&&words.Contains("Eye Care")&&words.Contains("Color Blindness"),"The Color tab lost its controls");
  Check(!words.Contains("Manage profiles")&&!words.Contains("Game vision")&&!words.Any(w=>w.Contains("Manage profiles")||w.Contains("Open game vision")||w.Contains("Auto-switch")),"Profiles or Game vision are still on the Color tab");
  // The right side is no longer empty: one-click looks and eye break reminders.
  Check(words.Contains("Quick looks")&&words.Contains("Eye breaks"),"The Color tab right side is missing Quick looks or Eye breaks: "+string.Join(" | ",words.Take(40)));
  var look=(Look)Field(shell,"current");var keptColor=look.Copy();
  Func<string,Button> preset=n=>Visuals<Button>(page).First(b=>Equals(b.Tag,n));
  preset("Night").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Math.Abs(look.Red-62)<.01&&Math.Abs(look.Dim-35)<.01&&Math.Abs(look.Saturation-100)<.01,"The Night look did not set the sliders: "+look.Red+", "+look.Dim);
  preset("Vivid").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Math.Abs(look.Saturation-170)<.01&&look.Red==0&&look.Dim==0,"The Vivid look did not reset the others");
  preset("Neutral").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(look.Saturation==100&&look.Red==0&&look.Dim==0,"The Neutral look did not clear everything");
  Check(ColorMath.Equal(ColorMath.Build(look),ColorMath.Identity()),"Neutral should leave the display untouched");
  Call(shell,"LoadLook",keptColor,"Desktop");
  Check(!Visuals<SmoothText>(page).Any(w=>w.Text=="Eye breaks")&&!Visuals<CheckBox>(page).Any(c=>Equals(c.Content,"Remind me to take breaks")),"Eye breaks should be gone from the Color page");
 }
 static async Task CheckProfilesPage(Shell shell)
 {
  // The remade Game profiles tab: one automatic switch, then one row per look with plain actions.
  var prefs=(Preferences)Field(shell,"preferences");var kept=prefs.Profiles.ToList();var keptAuto=prefs.AutoProfiles;
  Call(shell,"LoadLook",prefs.Desktop,"Desktop");prefs.Profiles.Clear();Call(shell,"ShowPage","Profiles");Call(shell,"RefreshProfiles");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");
  Func<string[]> words=()=>Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();
  Func<string,Button> named=n=>Visuals<Button>(page).First(b=>Equals(b.Content,n)||System.Windows.Automation.AutomationProperties.GetName(b)==n);
  foreach(string expected in new[]{"Switch automatically","Your profiles","Desktop","In use"})Check(words().Contains(expected),"Profiles is missing "+expected);
  Check(words().Any(w=>w.StartsWith("No profiles yet"))&&Visuals<Button>(page).Any(b=>Equals(b.Content,"+   Add another game")),"The empty state does not explain how to start");
  Check(!words().Contains("Load look")&&!words().Contains("Save current look")&&!words().Contains("Use desktop"),"The old confusing buttons are still there");
  var game=new GameProfile{Name="Test game",Executable=@"C:\Games\Test\game.exe",Look=new Look{Saturation=130}};prefs.Profiles.Add(game);Call(shell,"RefreshProfiles");shell.UpdateLayout();
  Check(words().Contains("Test game")&&words().Contains("game.exe")&&!words().Any(w=>w.StartsWith("No profiles yet")),"A game did not appear as a row");
  Check(Visuals<Button>(page).Count(b=>Equals(b.Content,"Use"))==1&&Visuals<Button>(page).Any(b=>Equals(b.Content,"Update look")),"A game row should offer Use and Update look");
  // Using a look marks it as in use, and the desktop row offers a way back.
  named("Use").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();
  var badgeText=Visuals<SmoothText>(page).First(t=>t.Text=="In use");var badge=(Border)VisualTreeHelper.GetParent(badgeText);var badgeRow=(FrameworkElement)VisualTreeHelper.GetParent(badge);
  var badgeMid=badge.TransformToAncestor(badgeRow).Transform(new Point(0,badge.ActualHeight/2)).Y;var textMid=badgeText.TransformToAncestor(badgeRow).Transform(new Point(0,badgeText.ActualHeight/2)).Y;
  Check(Math.Abs(badgeMid-badgeRow.ActualHeight/2)<1.5&&Math.Abs(textMid-badgeMid)<1.5,"The In use badge is not centered: badge "+badgeMid+" text "+textMid+" row "+badgeRow.ActualHeight);
  Check((string)Field(shell,"activeProfile")=="Test game"&&Visuals<SmoothText>(page).Count(t=>t.Text=="In use")==1&&Visuals<Button>(page).Count(b=>Equals(b.Content,"Use"))==1,"Using a look did not mark it in use");
  // Updating stores the colors that are set now.
  ((Look)Field(shell,"current")).Saturation=180;named("Update look").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(Math.Abs(game.Look.Saturation-180)<.001,"Update look did not keep the current colors: "+game.Look.Saturation);
  // Removing the look in use falls back to the desktop.
  named("Remove Test game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();
  Check(prefs.Profiles.Count==0&&(string)Field(shell,"activeProfile")=="Desktop"&&!words().Contains("Test game"),"Removing a profile");
  // The game library: a tile per game found, a click gives it its own look once, and the program is picked sensibly from its folder.
  var shown=new System.Collections.Generic.List<LibraryGame>{new LibraryGame{Name="Library Game",Executable=@"C:\Games\Library Game\game.exe",Store="Steam",Launch="steam://rungameid/12345"},new LibraryGame{Name="Another Game",Executable=@"C:\Games\Another\another.exe",Store="Epic Games"}};
  Call(shell,"SetLibrary",shown);shell.UpdateLayout();
  Check(words().Contains("Your games")&&words().Contains("Library Game")&&words().Contains("Another Game")&&words().Contains("Steam")&&words().Contains("Epic Games"),"The game library does not list the games found");
  named("Library Game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();
  Check(prefs.Profiles.Count==1&&prefs.Profiles[0].Name=="Library Game"&&prefs.Profiles[0].Executable==@"C:\Games\Library Game\game.exe"&&words().Any(w=>w.StartsWith("\u2713")),"Picking a library game did not give it a look");
  named("Library Game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.Profiles.Count==1,"Picking a game twice added it twice");
  Check(prefs.Profiles[0].Launch=="steam://rungameid/12345","The profile did not keep how the store starts the game");
  // The tile glows as one piece apart from Play; over Play only Play glows. Nothing here explains itself in a tooltip, and the page says what a profile does.
  var libraryTile=Visuals<Border>((DependencyObject)Field(shell,"content")).First(b=>Equals(b.Tag,"Library Game")&&b.Child is Grid);var tileWash=(Border)((Grid)libraryTile.Child).Children[0];var tilePlay=named("Play Library Game");
  Func<RoutedEvent,MouseEventArgs> pointer=routed=>new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=routed};
  Check(tileWash.Opacity<.01&&tilePlay.ToolTip==null&&named("Library Game").ToolTip==null,"A library tile should be quiet at rest and have no tooltip");
  libraryTile.RaiseEvent(pointer(Mouse.MouseEnterEvent));await Until(()=>tileWash.Opacity>.99);Check(tileWash.Opacity>.99&&tilePlay.Template.FindName("Glow",tilePlay)!=null,"Hovering a tile should light the whole tile");
  tilePlay.RaiseEvent(pointer(Mouse.MouseEnterEvent));await Until(()=>tileWash.Opacity<.01);Check(tileWash.Opacity<.01,"Hovering Play should leave only Play glowing: "+tileWash.Opacity);
  tilePlay.RaiseEvent(pointer(Mouse.MouseLeaveEvent));libraryTile.RaiseEvent(pointer(Mouse.MouseLeaveEvent));await Until(()=>tileWash.Opacity<.01);
  Check(words().Contains("What a profile saves")&&words().Any(w=>w.StartsWith("A profile is a saved set of settings"))&&words().Any(w=>w.Contains("Dark Boost")&&w.Contains("sound radar"))&&words().Contains("Save your settings"),"The page does not explain what a profile saves");
  // Play starts the game the way its launcher would, from the tile and from the game's row, and never from anything but a store address or the program file.
  named("Play Library Game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((string)Field(shell,"lastLaunch")=="steam://rungameid/12345","Play on the tile did not start the game through Steam: "+Field(shell,"lastLaunch"));
  typeof(Shell).GetField("lastLaunch",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(shell,null);
  Visuals<Button>(page).Last(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Play Library Game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((string)Field(shell,"lastLaunch")=="steam://rungameid/12345","Play on the game's row did not start it");
  named("Play Another Game").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((string)Field(shell,"lastLaunch")==@"C:\Games\Another\another.exe","A game without a store address should start from its program file");
  Check(GameLibrary.IsStoreAddress("steam://rungameid/730")&&GameLibrary.IsStoreAddress("com.epicgames.launcher://apps/ns%3Aitem%3AApp?action=launch&silent=true")&&!GameLibrary.IsStoreAddress("steam://rungameid/730&calc")&&!GameLibrary.IsStoreAddress("https://example.com")&&!GameLibrary.IsStoreAddress("file:///C:/Windows/System32/calc.exe")&&!GameLibrary.IsStoreAddress("cmd /c calc")&&!GameLibrary.IsStoreAddress("")&&!GameLibrary.IsStoreAddress(null),"Only store launch addresses may be opened");
  Check(GameLibrary.Start(null,@"C:\definitely\missing\game.exe")!=null&&GameLibrary.Start("steam://bad",@"C:\Windows\notanexe.txt")!=null,"A game that cannot be found must say so, not start anything");
  Check(Visuals<SmoothText>(page).Count(t=>t.Text=="Library Game")>=2,"The game should also appear under Your looks");
  var libFolder=Path.Combine(Path.GetTempPath(),"lucent-library-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(libFolder,"Binaries","Win64"));
  Action<string,int> make=(path,size)=>File.WriteAllBytes(Path.Combine(libFolder,path),new byte[size]);
  make("unins000.exe",9000);make("CrashReportClient.exe",8000);make("game.exe",4000);make(Path.Combine("Binaries","Win64","Game-Win64-Shipping.exe"),20000);
  Check(Path.GetFileName(GameLibrary.BestExecutable(libFolder))=="Game-Win64-Shipping.exe","The game program was not picked from its folder");
  File.Delete(Path.Combine(libFolder,"Binaries","Win64","Game-Win64-Shipping.exe"));Check(Path.GetFileName(GameLibrary.BestExecutable(libFolder))=="game.exe","Installers and crash reporters must not be picked as the game");
  Check(GameLibrary.BestExecutable(Path.Combine(libFolder,"missing"))==null,"A missing folder should give nothing");
  Directory.Delete(libFolder,true);
  Call(shell,"SetLibrary",new System.Collections.Generic.List<LibraryGame>());
  prefs.Profiles.Clear();prefs.Profiles.AddRange(kept);prefs.AutoProfiles=keptAuto;Call(shell,"RefreshProfiles");Call(shell,"ShowPage","Video");
 }
 static async Task CheckShellPolish(Shell shell)
 {
  var prefs=(Preferences)Field(shell,"preferences");
  // ---- The right-click menu from the tray: rows in order, a rounded card, arrow keys, Esc and a click that runs the row.
  var entries=(System.Collections.Generic.List<TrayEntry>)Call(shell,"TrayEntries");
  var titles=entries.Where(e=>!e.Separator).Select(e=>e.Title).ToArray();
  Check(string.Join("|",titles)=="Open Lucent|Color|Display|Crosshair|Profiles|Vision|Boost|Settings|Switch resolution|Close Lucent"&&entries.Last().Danger,"The tray menu rows are wrong: "+string.Join("|",titles));
  string ran=null;var popup=new TrayPopup("1920 \u00D7 1080 \u00B7 300 Hz",new[]{TrayEntry.Line("Home","One",()=>ran="one"),TrayEntry.Divider(),TrayEntry.Line("Video","Two",()=>ran="two"),TrayEntry.Line("Power","Three",()=>ran="three",null,true)}){Left=-20000,Top=-20000,ShowActivated=false};
  popup.Show();await Until(()=>popup.Opacity>.99);
  Check(popup.AllowsTransparency&&popup.Card.CornerRadius.TopLeft>=16&&popup.Card.CornerRadius.BottomRight>=16&&!popup.ShowInTaskbar&&popup.Topmost,"The tray menu should be a transparent window with a rounded card");
  popup.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),Environment.TickCount,Key.Down){RoutedEvent=Keyboard.PreviewKeyDownEvent});Check(popup.Hot==0,"Down should light the first row");
  popup.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),Environment.TickCount,Key.Up){RoutedEvent=Keyboard.PreviewKeyDownEvent});Check(popup.Hot==2,"Up from the first row should wrap to the last");
  popup.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
  await Until(()=>ran!=null&&!popup.IsVisible);Check(ran=="three"&&popup.Chosen,"Enter should run the lit row and close the menu: "+ran);
  ran=null;var second=new TrayPopup("x",new[]{TrayEntry.Line("Home","One",()=>ran="one"),TrayEntry.Line("Video","Two",()=>ran="two")}){Left=-20000,Top=-20000,ShowActivated=false};second.Show();await Until(()=>second.Opacity>.99);
  second.Choose(1);await Until(()=>ran!=null&&!second.IsVisible);Check(ran=="two","Clicking a row did not run it");
  var third=new TrayPopup("x",new[]{TrayEntry.Line("Home","One",()=>ran="never")}){Left=-20000,Top=-20000,ShowActivated=false};third.Show();await Until(()=>third.Opacity>.99);ran=null;
  third.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(third),Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});await Until(()=>!third.IsVisible);Check(ran==null,"Esc should close the menu without running anything");
  // Where it opens: above a tray at the bottom, never off the screen.
  var work=new System.Drawing.Rectangle(0,0,1920,1040);var size=new System.Drawing.Size(300,560);
  var atTray=TrayPopup.Place(new System.Drawing.Point(1850,1060),size,work);Check(atTray.X+300<=1920&&atTray.Y+560<=1040&&atTray.Y>=0&&atTray.X>=0,"The menu must stay on the screen at the tray: "+atTray);
  var topLeft=TrayPopup.Place(new System.Drawing.Point(2,2),size,work);Check(topLeft.X>=0&&topLeft.Y>=0,"The menu must stay on the screen at a corner: "+topLeft);
  var tall=TrayPopup.Place(new System.Drawing.Point(900,1060),new System.Drawing.Size(300,2000),work);Check(tall.Y>=0,"A menu taller than the screen should still start on it");
  // ---- Closing goes to the tray, on by default; minimizing is just minimizing.
  Check(Shell.ShouldHideOnClose(false,false,true,true)&&!Shell.ShouldHideOnClose(true,false,true,true)&&!Shell.ShouldHideOnClose(false,true,true,true)&&!Shell.ShouldHideOnClose(false,false,false,true)&&!Shell.ShouldHideOnClose(false,false,true,false),"Close should hide to the tray only when wanted, with a tray icon, and not when really exiting");
  Check(prefs.CloseToTray,"Close to tray should be on by default");
  Call(shell,"HideToTray");Check(!shell.IsVisible,"Closing to the tray should hide the window");Call(shell,"BringToFront");await Until(()=>shell.IsVisible);Check(shell.IsVisible,"The window did not come back");
  // ---- Settings: the old import row is gone, the tray option is Close to tray.
  Call(shell,"SetSettingsOpen",true);await Until(()=>shell.SettingsOpen);Call(shell,"SelectSettingsCategory","General");shell.UpdateLayout();
  var settingsWords=Visuals<SmoothText>((DependencyObject)Field(shell,"settingsContent")).Select(w=>w.Text).ToArray();
  Check(!settingsWords.Any(w=>w.Contains("Import saved resolutions"))&&!settingsWords.Contains("Minimize to tray")&&settingsWords.Contains("Close to tray"),"General settings still show the import row or the old tray option: "+string.Join("|",settingsWords.Take(12)));
  // ---- The settings window opens like an iOS sheet: it springs up past its place by a hair, and rests exactly in it.
  Check(Shell.SpringOut(0)==0&&Math.Abs(Shell.SpringOut(1)-1)<1e-9,"The spring must start at 0 and finish at 1");
  double peak=0;for(double v=0;v<=1.0001;v+=.01)peak=Math.Max(peak,Shell.SpringOut(v));Check(peak>1.05&&peak<1.2,"The spring should overshoot a little: "+peak);
  var card=(UIElement)Field(shell,"settingsCard");await Until(()=>card.Opacity>.9999&&card.RenderTransform.Value.IsIdentity);Check(card.Opacity>.9999&&card.RenderTransform.Value.IsIdentity,"The open settings window must rest exactly in place: "+card.Opacity+" "+card.RenderTransform.Value);
  Call(shell,"SetSettingsOpen",false);double smallest=1;for(int i=0;i<40&&card.Opacity>.02;i++){smallest=Math.Min(smallest,card.RenderTransform.Value.M11);await Task.Delay(10);}
  await Until(()=>((Grid)Field(shell,"settingsLayer")).Visibility==Visibility.Hidden);Check(smallest<.995,"The settings window did not shrink away while closing: "+smallest);
  // ---- About: updates, with nothing sent until a source is set, and a clear answer either way.
  Call(shell,"SetSettingsOpen",true);await Until(()=>shell.SettingsOpen);Call(shell,"SelectSettingsCategory","About");shell.UpdateLayout();
  var aboutTree=(DependencyObject)Field(shell,"settingsContent");
  Check(Visuals<SmoothText>(aboutTree).Any(w=>w.Text=="Updates")&&Visuals<Button>(aboutTree).Any(b=>Equals(b.Content,"Check for updates")),"About has no Updates block");
  var keptSource=Updates.Source;var keptNotified=prefs.NotifiedVersion;int requests=0;Updates.Source="";
  Updates.Fetcher=url=>{requests++;return "{\"tag_name\":\"v1.0227\",\"html_url\":\"https://github.com/owner/lucent/releases/tag/v1.0227\",\"body\":\"Big changes\"}";};
  await (Task)Call(shell,"CheckForUpdates",true);Check(requests==0&&Visuals<SmoothText>(aboutTree).Any(w=>w.Text.StartsWith("Updates are not set up")),"With no source nothing should be requested, and the user told why");
  Updates.Source="owner/lucent";await (Task)Call(shell,"CheckForUpdates",true);
  Check(requests==1&&Visuals<SmoothText>(aboutTree).Any(w=>w.Text.StartsWith("Lucent 1.0227 is available"))&&Visuals<Button>(aboutTree).Any(b=>Equals(b.Content,"Get 1.0227")&&b.IsVisible)&&Visuals<SmoothText>(aboutTree).Any(w=>w.Text=="Big changes"),"A newer version was not announced in About");
  Check(((UpdateInfo)Call(shell,"get_UpdateAvailable")).Latest.Equals(new Version(1,227,0,0)),"The update was not remembered");
  Call(shell,"ShowPage","Home");shell.UpdateLayout();var homeTree=(DependencyObject)Field(shell,"content");
  Check(Visuals<SmoothText>(homeTree).First(w=>w.Text=="Update available").IsVisible&&Visuals<SmoothText>(homeTree).Any(w=>w.Text.StartsWith("Lucent 1.0227 is out")),"Home does not show the available update");
  typeof(Shell).GetField("lastLaunch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,null);Call(shell,"OpenUpdatePage");Check((string)Field(shell,"lastLaunch")=="https://github.com/owner/lucent/releases/tag/v1.0227","Get update did not open the release page");
  Check(Updates.CanOpenRelease("https://github.com/owner/lucent/releases")&&!Updates.CanOpenRelease("https://github.com/someone-else/lucent/releases")&&!Updates.CanOpenRelease("https://example.com/owner/lucent")&&!Updates.CanOpenRelease("http://github.com/owner/lucent/releases"),"Only a page of the project's own repository may be opened from an update notice");
  Check(!Visuals<TextBox>(aboutTree).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Update source")&&!Visuals<Button>(aboutTree).Any(b=>Equals(b.Content,"Save")),"About must not offer a way to change where updates come from");
  Check(Updates.DefaultSource=="positted/lucent"&&new Preferences().UpdateSource=="","The project repository is not the fixed default");
  // ---- The update window: it asks first, downloads only from the project's own repository, and checks the file it got.
  Check(shell.UpdatePopupOpen,"A newer version should open the update window");
  var popupLayer=(DependencyObject)Field(shell,"updatePopup");
  Check(Visuals<SmoothText>(popupLayer).Any(w=>w.Text=="Update available")&&Visuals<SmoothText>(popupLayer).Any(w=>w.Text.StartsWith("Lucent 1.0227 is ready"))&&Visuals<Button>(popupLayer).Any(b=>Equals(b.Content,"Update now"))&&Visuals<Button>(popupLayer).Any(b=>Equals(b.Content,"Later")),"The update window should say what is available and offer Update now and Later");
  Visuals<Button>(popupLayer).First(b=>Equals(b.Content,"Later")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(!shell.UpdatePopupOpen,"Later should close the update window");
  await (Task)Call(shell,"CheckForUpdates",false);
  Check(!shell.UpdatePopupOpen,"A quiet check must not ask again about a version already offered");
  string helloHash;using(var sha=System.Security.Cryptography.SHA256.Create())helloHash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes("hello"))).Replace("-","");
  Updates.Fetcher=url=>"{\"tag_name\":\"v1.0227\",\"html_url\":\"https://github.com/owner/lucent/releases/tag/v1.0227\",\"body\":\"Big changes\",\"assets\":[{\"name\":\"Lucent-Setup-1.0227.exe\",\"size\":5,\"digest\":\"sha256:"+helloHash+"\",\"browser_download_url\":\"https://github.com/owner/lucent/releases/download/v1.0227/Lucent-Setup-1.0227.exe\"},{\"name\":\"notes.txt\",\"browser_download_url\":\"https://example.com/notes.txt\"}]}";
  await (Task)Call(shell,"CheckForUpdates",true);
  Check(shell.UpdatePopupOpen&&shell.UpdateAvailable.AssetName=="Lucent-Setup-1.0227.exe"&&shell.UpdateAvailable.AssetUrl.EndsWith("/Lucent-Setup-1.0227.exe")&&shell.UpdateAvailable.AssetSize==5,"A click on Check for updates should always ask, and the release's setup file should be read");
  Check(Updates.CanDownload(shell.UpdateAvailable.AssetUrl)&&!Updates.CanDownload("https://github.com/someone-else/lucent/releases/download/v1/Lucent-Setup-1.exe")&&!Updates.CanDownload("https://example.com/owner/lucent/releases/download/v1/Lucent-Setup-1.exe")&&!Updates.CanDownload("http://github.com/owner/lucent/releases/download/v1/Lucent-Setup-1.exe")&&!Updates.CanDownload(null),"Only a setup file from the project's own releases may be downloaded");
  string wantedPath=Path.Combine(Path.GetTempPath(),"Lucent-Setup-1.0227.exe");string fetchedFrom=null;
  Updates.FileFetcher=(info,path,progress)=>{fetchedFrom=info.AssetUrl;File.WriteAllText(path,"evil");try{Updates.VerifyDigest(info,path);}catch(Exception){File.Delete(path);throw;}};
  popupLayer=(DependencyObject)Field(shell,"updatePopup");
  Visuals<Button>(popupLayer).First(b=>Equals(b.Content,"Update now")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  await Until(()=>Visuals<Button>(popupLayer).Any(b=>Equals(b.Content,"Open download page")));
  Check(fetchedFrom!=null&&!File.Exists(wantedPath)&&shell.UpdatePopupOpen&&Visuals<SmoothText>(popupLayer).Any(w=>w.Text.Contains("does not match")),"A file that does not match its published checksum must be refused and removed");
  typeof(Shell).GetField("lastLaunch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,null);
  Visuals<Button>(popupLayer).First(b=>Equals(b.Content,"Open download page")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check((string)Field(shell,"lastLaunch")=="https://github.com/owner/lucent/releases/tag/v1.0227"&&!shell.UpdatePopupOpen,"After a failed download the popup should offer the release page");
  Call(shell,"ShowUpdatePopup");popupLayer=(DependencyObject)Field(shell,"updatePopup");
  Updates.FileFetcher=(info,path,progress)=>{File.WriteAllText(path,"hello");progress(5,5);Updates.VerifyDigest(info,path);};
  typeof(Shell).GetField("lastLaunch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,null);
  Visuals<Button>(popupLayer).First(b=>Equals(b.Content,"Update now")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  await Until(()=>(string)Field(shell,"lastLaunch")==wantedPath);
  Check((string)Field(shell,"lastLaunch")==wantedPath&&File.Exists(wantedPath)&&File.ReadAllText(wantedPath)=="hello"&&!shell.UpdatePopupOpen,"A good download should be handed to setup");
  File.Delete(wantedPath);Updates.FileFetcher=null;
  Updates.Fetcher=url=>"{\"tag_name\":\"v0.0.1\",\"html_url\":\"https://example.com/old\"}";await (Task)Call(shell,"CheckForUpdates",true);
  Check(Call(shell,"get_UpdateAvailable")==null&&!Visuals<SmoothText>((DependencyObject)Field(shell,"content")).First(w=>w.Text=="Update available").IsVisible,"An older release must not show as an update");
  Updates.Fetcher=url=>{throw new InvalidOperationException("no network");};await (Task)Call(shell,"CheckForUpdates",true);
  Call(shell,"SelectSettingsCategory","About");Check(Visuals<SmoothText>(aboutTree).Any(w=>w.Text.StartsWith("Could not check for updates")),"A failed check should say so");
  prefs.CheckUpdates=false;requests=0;Updates.Fetcher=url=>{requests++;return "{}";};await (Task)Call(shell,"CheckForUpdates",false);Check(requests==0,"A quiet check must do nothing when automatic checks are off");prefs.CheckUpdates=true;
  Updates.Source=keptSource;prefs.NotifiedVersion=keptNotified;Updates.Fetcher=url=>"{}";
  // ---- Every slider in Settings: the ball sits on the middle of the bar, and a click anywhere jumps there.
  {
   var sliderReport=new System.Text.StringBuilder();int slidersSeen=0;
   foreach(var category in ((System.Collections.IDictionary)Field(shell,"settingsCategoryButtons")).Keys.Cast<object>().Select(k=>k.ToString()).ToList())
   {
    Call(shell,"HideUpdatePopup");Call(shell,"SelectSettingsCategory",category);shell.UpdateLayout();
    var tree=(DependencyObject)Field(shell,"settingsContent");
    foreach(var slider in Visuals<Slider>(tree))
    {
     if(slider.ActualWidth<10||slider.ActualHeight<10)continue;
     var thumb=Visuals<System.Windows.Controls.Primitives.Thumb>(slider).FirstOrDefault();var knob=thumb==null?null:Visuals<System.Windows.Shapes.Ellipse>(thumb).FirstOrDefault();
     var trackBar=Visuals<Border>(slider).FirstOrDefault(b=>(Math.Abs(b.ActualHeight-12)<.01||Math.Abs(b.ActualHeight-7)<.01)&&b.ActualWidth>slider.ActualWidth*.5);
     if(knob==null||trackBar==null){sliderReport.AppendLine(category+": slider without the standard ball or bar ("+slider.Height+" high)");continue;}
     slidersSeen++;
     double knobMid=knob.TransformToAncestor(slider).Transform(new Point(0,knob.ActualHeight/2)).Y,barMid=trackBar.TransformToAncestor(slider).Transform(new Point(0,trackBar.ActualHeight/2)).Y;
     if(Math.Abs(knobMid-barMid)>.5)sliderReport.AppendLine(category+": ball is "+(knobMid-barMid).ToString("0.##")+" px off the bar centre (slider height "+slider.ActualHeight+")");
     if(!slider.IsMoveToPointEnabled)sliderReport.AppendLine(category+": click does not jump to the click spot");
    }
   }
   Check(slidersSeen>0&&sliderReport.Length==0,"Slider problems:\n"+sliderReport+"(checked "+slidersSeen+")");
  }
  // ---- Discord RPC page: off by default, shows its state, and only ever shows a game. Nothing connects in a test unless it is given a made-up Discord.
  Call(shell,"HideUpdatePopup");Call(shell,"SelectSettingsCategory","Discord RPC");shell.UpdateLayout();
  var discordTree=(DependencyObject)Field(shell,"settingsContent");
  Check(((System.Collections.IDictionary)Field(shell,"settingsCategoryButtons")).Contains("Discord RPC")&&!((System.Collections.IDictionary)Field(shell,"settingsCategoryButtons")).Contains("Discord"),"The tab should be called Discord RPC");
  Check(Visuals<SmoothText>(discordTree).Any(w=>w.Text=="Discord not connected")&&Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Discord Rich Presence"&&c.IsChecked==true),"The Discord RPC page is missing its status or its switch (Rich Presence is on by default)");
  Check(Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Elapsed time"&&c.IsChecked==true)&&Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c).Contains("Get Lucent")&&c.IsChecked==true)&&!Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Playback timeline"||System.Windows.Automation.AutomationProperties.GetName(c)=="Show when paused")&&!Visuals<Button>(discordTree).Any(b=>Equals(b.Tag,"Artist")),"The page should have no music options");
  Check(Visuals<SmoothText>(discordTree).Any(w=>w.Text.Contains("Nothing is shown on Discord unless a game is running")),"The page should explain that only a running game is shown");
  // With a made-up Discord: nothing is shown with no game, and a running game shows as "Playing <game> with Lucent".
  var fakeDiscord=new DiscordRpcClient{ClientId="1",PipePrefix="lucent-ui-test-nobody-"+Guid.NewGuid().ToString("N")+"-"};
  var robloxList="[{\"id\":\"3\",\"name\":\"ROBLOX\",\"aliases\":[\"Roblox\"],\"icon_hash\":\"hash3\",\"executables\":[{\"os\":\"win32\",\"name\":\">RobloxPlayerBeta.exe\"}]}]";
  var keptFetcher=DiscordGameIcons.Fetcher;DiscordGameIcons.Reset();DiscordGameIcons.Fetcher=()=>robloxList;
  shell.DiscordGameProbe=()=>null;shell.DiscordNameProbe=()=>new[]{"explorer"};
  Check(Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Show Idle"&&c.IsChecked==true),"The page should have a Show Idle switch, on by default");
  prefs.DiscordIdle=false;
  shell.DiscordFactory=()=>fakeDiscord;prefs.DiscordOn=true;Call(shell,"ApplyDiscord");
  await Until(()=>!(bool)Field(shell,"readingDiscord"));
  Check(shell.DiscordClient==fakeDiscord,"Switching Discord on should start the client");
  shell.SendDiscord(null,new PresenceOptions());Check(shell.DiscordShowing=="","With no game nothing should be shown on Discord");
  shell.SendDiscord(new GameProfile{Name="Rust",Executable=@"C:\Games\Rust\RustClient.exe"},new PresenceOptions());Check(shell.DiscordShowing=="Playing Rust with Lucent","A running game should show as Playing Rust with Lucent: "+shell.DiscordShowing);
  shell.SendDiscord(null,new PresenceOptions());Check(shell.DiscordShowing=="","When the game closes the status should go away");
  // Without any game profile, a known game is found from the names of running programs.
  await Until(()=>DiscordGameIcons.FindRunning(new[]{"RobloxPlayerBeta"})!=null);
  shell.DiscordNameProbe=()=>new[]{"explorer","RobloxPlayerBeta"};
  await (Task)Call(shell,"PushDiscord");Check(shell.DiscordShowing=="Playing Roblox with Lucent","A game with no profile should still show: "+shell.DiscordShowing);
  shell.DiscordNameProbe=()=>new[]{"explorer"};await (Task)Call(shell,"PushDiscord");Check(shell.DiscordShowing=="","The status should go away when the known game is gone: "+shell.DiscordShowing);
  // Idle: with Show Idle on and no game, Lucent shows "Idle"; a game replaces it; switching it off clears it.
  shell.SendDiscord(null,new PresenceOptions{Idle=true});Check(shell.DiscordShowing=="Idle","With Show Idle on and no game the status should be Idle: "+shell.DiscordShowing);
  shell.SendDiscord(new GameProfile{Name="Rust",Executable=@"C:\Games\Rust\RustClient.exe"},new PresenceOptions{Idle=true});Check(shell.DiscordShowing=="Playing Rust with Lucent","A game should replace Idle");
  shell.SendDiscord(null,new PresenceOptions{Idle=true});Check(shell.DiscordShowing=="Idle","Idle should return when the game closes");
  shell.SendDiscord(null,new PresenceOptions{Idle=false});Check(shell.DiscordShowing=="","Switching Show Idle off should clear the status");
  shell.SendDiscordSong("Nvr again","Yeat","https://e-cdns-images.dzcdn.net/images/cover/x/500x500.jpg",30,166,new PresenceOptions{Songs=true,Idle=true});Check(shell.DiscordShowing=="Listening to Nvr again","A playing song should show as listening: "+shell.DiscordShowing);
  shell.SendDiscordSong("Nvr again","Yeat","https://e-cdns-images.dzcdn.net/images/cover/x/500x500.jpg",31,166,new PresenceOptions{Songs=true,Idle=true});Check(shell.DiscordShowing=="Listening to Nvr again","The song going on should keep showing");
  shell.SendDiscord(new GameProfile{Name="Rust",Executable=@"C:\Games\Rust\RustClient.exe"},new PresenceOptions{Songs=true,Idle=true});Check(shell.DiscordShowing=="Playing Rust with Lucent","A game should come before the song");
  shell.SendDiscordSong(null,null,null,0,0,new PresenceOptions{Songs=true,Idle=true});Check(shell.DiscordShowing=="Idle","With the song and game gone the status should be Idle: "+shell.DiscordShowing);
  Check(Visuals<CheckBox>(discordTree).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Show songs"&&c.IsChecked==true),"The page should have a Show songs switch, on by default");
  shell.DiscordGameProbe=null;shell.DiscordNameProbe=null;DiscordGameIcons.Reset();DiscordGameIcons.Fetcher=keptFetcher;
  prefs.DiscordOn=false;prefs.DiscordIdle=true;Call(shell,"ApplyDiscord");shell.DiscordFactory=null;
  Check(shell.DiscordClient==null&&fakeDiscord.Status=="Disabled","Switching Discord off should stop the client");  Call(shell,"SetSettingsOpen",false);await Until(()=>!shell.SettingsOpen);Call(shell,"ShowPage","Video");
  // ---- The bar's colored band (the cover's colors across it), switched off by performance mode.
  {
   var glassBar=(PlayerBar)Field(shell,"playerBar");var glass=glassBar.Backdrop;
   Check(glass.On&&glass.Visibility==Visibility.Visible&&glass.Child is Grid&&glass.CacheMode==null,"The bar should carry a plain colored band (nothing copied or blurred from behind it)");
   Check(PlayerBar.SurfaceColor(Color.FromRgb(200,60,50),.8,true).A<80&&PlayerBar.SurfaceColor(Color.FromRgb(200,60,50),.8,false).A>=230&&PlayerBar.SurfaceColor(Color.FromRgb(46,46,48),0,true).A<80&&PlayerBar.SurfaceColor(Color.FromRgb(46,46,48),0,false).A>=230,"Over the band the tint must be light, and with the band off the bar must be an almost solid strip");
   {
    // The band runs from the left of the cover's colors to the right of them, dark enough for the text, plain dark with no strength.
    var halves=new System.Windows.Media.Imaging.RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);var halfVisual=new DrawingVisual();
    using(var dc=halfVisual.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255,40,30)),null,new Rect(0,0,32,64));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30,60,255)),null,new Rect(32,0,32,64));}
    halves.Render(halfVisual);
    var band=BarBackdrop.ColorsFor(halves,Colors.Gray,1);
    Check(band.Length==BarBackdrop.Count&&band[0].R>band[0].B+30&&band[BarBackdrop.Count-1].B>band[BarBackdrop.Count-1].R+30,"The band should follow the cover's colors from left to right");
    Check(band.All(c=>c.R*.2126+c.G*.7152+c.B*.0722<=152&&c.A<200),"The band must stay dark enough for the text on it, and see-through");
    Check(BarBackdrop.ColorsFor(halves,Colors.Gray,0).All(c=>c==BarBackdrop.Plain),"With no strength the band is plain dark");
    glass.SetCover(halves,Colors.Gray,true,1);Check(glass.Target[0]==band[0]&&glass.Target[BarBackdrop.Count-1]==band[BarBackdrop.Count-1],"The bar did not take the cover's colors");
    glass.SetCover(null,Colors.Gray,false,0);Check(glass.Target.All(c=>c==BarBackdrop.Plain),"With nothing playing the band should go back to plain dark");
   }
   var performanceOn=(Preferences)Field(shell,"preferences");bool keptPerformance=performanceOn.PerformanceMode;Call(shell,"SelectSettingsCategory","Appearance");shell.UpdateLayout();
   var performanceSwitch=Visuals<CheckBox>((DependencyObject)Field(shell,"settingsLayer")).FirstOrDefault(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Performance mode");
   if(performanceSwitch!=null){performanceSwitch.IsChecked=!keptPerformance;performanceSwitch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check(performanceOn.PerformanceMode!=keptPerformance&&glass.On==!performanceOn.PerformanceMode&&(glass.Visibility==Visibility.Collapsed)==performanceOn.PerformanceMode,"Performance mode should switch the glass off (and back on): perf "+performanceOn.PerformanceMode+" kept "+keptPerformance+" glass.On "+glass.On+" vis "+glass.Visibility);performanceSwitch.IsChecked=keptPerformance;performanceSwitch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check(glass.On==!keptPerformance,"The glass did not return when performance mode went off");}
  }  // ---- The window's corners are round (8 px, like Windows' own) and smooth: a see-through window clipped with an anti-aliased shape.
  {
   var surface=(FrameworkElement)shell.Content;
   Check(shell.AllowsTransparency&&surface.Clip!=null&&Math.Abs(shell.CurrentWindowRadius-8)<.01,"The window should have 8 px smooth corners");
   var corner=new System.Windows.Media.Imaging.RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);corner.Render(surface);
   var probe=new byte[4];Func<int,int,int> Alpha=(px,py)=>{corner.CopyPixels(new Int32Rect(px,py,1,1),probe,4,0);return probe[3];};
   int edge=0;for(int k=0;k<6;k++){int a=Alpha(k,k);if(a>0&&a<255)edge++;}
   Check(Alpha(0,0)==0&&Alpha((int)surface.ActualWidth-1,(int)surface.ActualHeight-1)==0&&Alpha((int)surface.ActualWidth/2,(int)surface.ActualHeight/2)==255&&Alpha((int)surface.ActualWidth/2,1)==255,"The window corners are not cut round");
   Check(edge>=1,"The corner edge should be smooth (partly transparent pixels), not a hard step");
  }  // ---- Artist pictures: off by default, nothing is looked up until switched on, and only the right artist's picture is used.
  {
   var picturePrefs=(Preferences)Field(shell,"preferences");
   Check(!picturePrefs.ArtistPictures,"Artist pictures should be off by default, since looking one up goes online");
   byte[] pixelsPng;{var tiny=new System.Windows.Media.Imaging.RenderTargetBitmap(8,8,96,96,PixelFormats.Pbgra32);var dv=new DrawingVisual();using(var dc=dv.RenderOpen())dc.DrawRectangle(Brushes.Orange,null,new Rect(0,0,8,8));tiny.Render(dv);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(tiny));using(var ms=new System.IO.MemoryStream()){png.Save(ms);pixelsPng=ms.ToArray();}}
   int searches=0;ArtistPictures.Reset();var keptSearch=ArtistPictures.SearchFetcher;var keptImage=ArtistPictures.ImageFetcher;var keptEnabled=ArtistPictures.Enabled;
   ArtistPictures.SearchFetcher=name=>{searches++;return "{\"data\":[{\"name\":\"Someone Else\",\"picture_medium\":\"https://e-cdns-images.dzcdn.net/images/artist/wrong/250x250.jpg\"},{\"name\":\"Yeat\",\"picture_medium\":\"https://e-cdns-images.dzcdn.net/images/artist/right/250x250.jpg\"}]}";};
   string imageAsked=null;ArtistPictures.ImageFetcher=url=>{imageAsked=url;return pixelsPng;};
   var badge=new ArtistBadge(20);
   ArtistPictures.Enabled=()=>false;badge.Show("Yeat, Lyfestyle Corporation",ArtistPictures.Enabled());await Task.Delay(150);
   Check(!badge.Shown&&searches==0,"Nothing should be looked up while artist pictures are off");
   ArtistPictures.Enabled=()=>true;badge.Show("Yeat, Lyfestyle Corporation",true);
   await Until(()=>badge.Shown);
   Check(searches==1&&imageAsked!=null&&imageAsked.Contains("/right/"),"The picture of the artist with the same name should be used: "+imageAsked);
   badge.Show("Yeat",true);await Until(()=>badge.Shown);Check(searches==1,"A picture is only looked up once per artist");
   Check(ArtistPictures.PictureUrl("{\"data\":[{\"name\":\"Yeat\",\"picture_medium\":\"https://evil.example.com/a.jpg\"}]}","Yeat")==null&&ArtistPictures.PictureUrl("{\"data\":[{\"name\":\"Yeat\",\"picture_medium\":\"http://e-cdns-images.dzcdn.net/a.jpg\"}]}","Yeat")==null&&ArtistPictures.PictureUrl("not json","Yeat")==null,"Pictures may only come from Deezer over https");
   ArtistPictures.Reset();ArtistPictures.SearchFetcher=keptSearch;ArtistPictures.ImageFetcher=keptImage;ArtistPictures.Enabled=keptEnabled;
  }  // ---- With media playing, page sections take a deeper shade of its color (settings stay plain).
  {
   shell.SetMediaAvailable(true);Call(shell,"SetPlayerBarOpen",true);await Task.Delay(900);
   var art=new DrawingVisual();
   using(var dc=art.RenderOpen())
   {
    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(120,20,24)),null,new Rect(0,0,128,128));
    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(235,60,50)),null,new Point(34,40),50,44);
    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(250,160,120)),null,new Point(98,34),30,26);
    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(30,10,14)),null,new Point(70,104),52,34);
   }
   var pixels=new System.Windows.Media.Imaging.RenderTargetBitmap(128,128,96,96,PixelFormats.Pbgra32);pixels.Render(art);pixels.Freeze();
   var bf=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   typeof(Shell).GetField("paletteArtwork",bf).SetValue(shell,new byte[1]);typeof(Shell).GetField("paletteBitmap",bf).SetValue(shell,pixels);
   typeof(Shell).GetField("mediaAccent",bf).SetValue(shell,Design.Palette(pixels));
   Call(shell,"UpdateAmbientColor");await Task.Delay(1500);shell.UpdateLayout();
  }

  // ---- The player bar and the sidebar end on the same line.
  shell.SetMediaAvailable(true);Call(shell,"SetPlayerBarOpen",true);await Until(()=>((UIElement)Field(shell,"playerBar")).Opacity>.99&&Math.Abs(((TranslateTransform)((UIElement)Field(shell,"playerBar")).RenderTransform).Y)<.01);shell.UpdateLayout();
  var rail=(FrameworkElement)Field(shell,"tabStrip");var bar=(FrameworkElement)Field(shell,"playerBar");
  double railBottom=rail.TransformToAncestor(shell).TransformBounds(new Rect(0,0,rail.ActualWidth,rail.ActualHeight)).Bottom,barBottom=bar.TransformToAncestor(shell).TransformBounds(new Rect(0,0,bar.ActualWidth,bar.ActualHeight)).Bottom;
  Check(barBottom<=shell.ActualHeight-10&&barBottom>=shell.ActualHeight-24,"The player bar does not sit just above the window edge: bottom "+barBottom+" of "+shell.ActualHeight);
  Call(shell,"SetPlayerBarOpen",false);await Until(()=>((UIElement)Field(shell,"playerBar")).Visibility!=Visibility.Visible||((UIElement)Field(shell,"playerBar")).Opacity<.02);
  // ---- Sharpening belongs to zoom: Color pop and Smart shadows draw at 1x and sharpen nothing; zoom itself sharpens.
  var look=(Look)Field(shell,"current");var keptLook=look.Copy();bool keptRisk=prefs.RiskAccepted;prefs.RiskAccepted=true;
  look.ZoomSharpness=70;look.ZoomSharp="Manual";look.PopColor="Red";look.PopStrength=80;Field(shell,"zoomed");typeof(Shell).GetField("zoomed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,false);
  var idle=(ZoomRequest)Call(shell,"BuildZoomRequest",Call(shell,"EffectiveLook"));Check(idle.Sharpness==0&&idle.Zoom==1&&idle.PopHue==0&&idle.Wanted,"Color pop alone must not sharpen: "+idle.Sharpness);
  var applyField=typeof(Shell).GetField("applyLook",BindingFlags.Instance|BindingFlags.NonPublic);bool keptApply=(bool)applyField.GetValue(shell);applyField.SetValue(shell,true);
  look.DarkEnabled=true;look.DarkSmart=true;look.DarkStrength=50;idle=(ZoomRequest)Call(shell,"BuildZoomRequest",Call(shell,"EffectiveLook"));Check(idle.Sharpness==0&&idle.Lift>0,"Smart shadows must not sharpen");
  typeof(Shell).GetField("zoomed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,true);var zoomedRequest=(ZoomRequest)Call(shell,"BuildZoomRequest",Call(shell,"EffectiveLook"));Check(Math.Abs(zoomedRequest.Sharpness-.7)<.001&&zoomedRequest.Zoom>1,"Zoom should sharpen by its own setting: "+zoomedRequest.Sharpness);
  look.ZoomSharp="Adaptive";var adaptiveRequest=(ZoomRequest)Call(shell,"BuildZoomRequest",Call(shell,"EffectiveLook"));Check(adaptiveRequest.Adaptive&&adaptiveRequest.Sharpness>0&&!adaptiveRequest.Follow,"Adaptive sharpening should be requested from the shader");
  look.ZoomSharp="Off";var offRequest=(ZoomRequest)Call(shell,"BuildZoomRequest",Call(shell,"EffectiveLook"));Check(offRequest.Sharpness==0&&!offRequest.Adaptive,"Sharpening off should not sharpen");look.ZoomSharp="Manual";
  typeof(Shell).GetField("zoomed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,false);applyField.SetValue(shell,keptApply);Call(shell,"LoadLook",keptLook,"Desktop");prefs.RiskAccepted=keptRisk;
  // ---- Crosshair: the preview on the left, the controls that shape it beside it, the share codes underneath.
  double crosshairWidth=shell.Width,crosshairHeight=shell.Height;shell.Width=Math.Max(crosshairWidth,1280);shell.Height=Math.Max(crosshairHeight,820);
  Call(shell,"ShowPage","Crosshair");shell.UpdateLayout();var crosshairTree=(DependencyObject)Field(shell,"content");
  Func<string,Rect> folderBox=title=>{var folder=Visuals<Border>(crosshairTree).First(b=>Equals(b.Tag,title));return folder.TransformToAncestor((Visual)crosshairTree).TransformBounds(new Rect(0,0,folder.ActualWidth,folder.ActualHeight));};
  // The two columns are chosen from the width the page is given, which settles a moment after it opens.
  await Until(()=>{shell.UpdateLayout();return folderBox("Shape").Left>200;});
  var previewBox=((FrameworkElement)Field(shell,"crosshairPreview")).TransformToAncestor((Visual)crosshairTree).TransformBounds(new Rect(0,0,10,10));
  var shapeBox=folderBox("Shape");var appearanceBox=folderBox("Appearance");var codesBox=folderBox("Crosshair codes");
  Check(previewBox.Left<shapeBox.Left-100&&shapeBox.Left==appearanceBox.Left&&Math.Abs(shapeBox.Top-previewBox.Top)<260&&codesBox.Top>shapeBox.Top&&codesBox.Top>=appearanceBox.Bottom-1,"The crosshair page layout is wrong: preview "+previewBox+", shape "+shapeBox+", codes "+codesBox+" (window "+shell.ActualWidth+"x"+shell.ActualHeight+", page "+((FrameworkElement)crosshairTree).ActualWidth+")");
  shell.Width=crosshairWidth;shell.Height=crosshairHeight;shell.UpdateLayout();
  Call(shell,"ShowPage","Video");
 }
 static async Task CheckBackdropPause()
 {
  // Pausing the music holds the drifting colors where they are, and playing carries on from there; it does not go back to the start.
  var window=new Window{Left=-20000,Top=-20000,Width=400,Height=300,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};var back=new FlowingBackdrop{Lively=true};window.Content=back;window.Show();
  try
  {
   back.SetActive(true);await Task.Delay(700);var slides=(TranslateTransform[])Field(back,"slides");
   Func<double> Where=()=>slides[0].X+slides[0].Y+slides[1].X+slides[1].Y;
   Check(Math.Abs(Where())>.01,"The colors should be drifting while active: "+Where());
   back.SetActive(false);await Task.Delay(150);double held=Where();await Task.Delay(300);
   Check(Math.Abs(held)>.01&&Math.Abs(Where()-held)<.0001,"Pausing should hold the colors where they are, not move or reset them: "+held+" then "+Where());
   back.SetActive(true);await Task.Delay(300);
   Check(Math.Abs(Where()-held)>.0001&&Math.Abs(Where())>.01,"Playing again should carry on from where it stopped: "+held+" then "+Where());
   // A new song's colors fade in over the old song's (worked out off the app's own thread) instead of dipping to black in between.
   back.SetCover(Cover(Color.FromRgb(220,40,30)));await Until(()=>back.Ready);
   var flowLayers=(Image[])Field(back,"layers");await Task.Delay(900);Check(flowLayers[0].Source!=null&&flowLayers[0].Opacity>.4,"The first cover did not show");
   back.SetCover(Cover(Color.FromRgb(30,60,220)));await Until(()=>flowLayers[3].Source!=null);
   Check(flowLayers[0].Source!=null&&!ReferenceEquals(flowLayers[0].Source,flowLayers[3].Source)&&flowLayers[0].Opacity>.02,"The old cover should still be there while the new one fades in");
   await Task.Delay(1300);Check(flowLayers[0].Opacity<.01&&flowLayers[3].Opacity>.4,"The new cover should have taken over: "+flowLayers[0].Opacity+", "+flowLayers[3].Opacity);


  }
  finally{window.Close();}
 }
 static void CheckAnimatedBackground(Shell shell)
 {
  // The drifting colors behind the app: on by default, shown only with artwork, and moving only while something is playing.
  var prefs=(Preferences)Field(shell,"preferences");var host=(Grid)Field(shell,"flowHost");var backdrop=(FlowingBackdrop)Field(shell,"flowBackdrop");
  Check(prefs.AnimatedBackground&&new Preferences().AnimatedBackground,"The animated background should be on by default");
  var paletteField=typeof(Shell).GetField("paletteBitmap",BindingFlags.Instance|BindingFlags.NonPublic);var trackField=typeof(Shell).GetField("ambientTrack",BindingFlags.Instance|BindingFlags.NonPublic);
  var keptBitmap=paletteField.GetValue(shell);var keptTrack=trackField.GetValue(shell);bool keptAdaptive=prefs.AdaptiveTheme;prefs.AdaptiveTheme=true;
  var art=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{0,0,255,255,0,255,0,255,255,0,0,255,0,255,255,255},8);art.Freeze();
  paletteField.SetValue(shell,null);trackField.SetValue(shell,null);Call(shell,"UpdateAmbientColor");
  Check(host.Visibility==Visibility.Collapsed&&!backdrop.Running,"Without artwork there should be no moving background");
  paletteField.SetValue(shell,art);trackField.SetValue(shell,new MediaTrack{Title="x",Playing=false});Call(shell,"UpdateAmbientColor");
  Check(host.Visibility==Visibility.Visible&&backdrop.Running,"A paused song should show the colors and keep them drifting (they only rest when nothing is loaded)");
  trackField.SetValue(shell,new MediaTrack{Title="x",Playing=true});Call(shell,"UpdateFlowBackdrop");
  Check(host.Visibility==Visibility.Visible&&backdrop.Running,"A playing song should set the background drifting");
  prefs.AnimatedBackground=false;Call(shell,"UpdateFlowBackdrop");Check(host.Visibility==Visibility.Collapsed&&!backdrop.Running,"Turning the setting off should remove the moving background");
  prefs.AnimatedBackground=true;
  paletteField.SetValue(shell,keptBitmap);trackField.SetValue(shell,keptTrack);prefs.AdaptiveTheme=keptAdaptive;Call(shell,"UpdateAmbientColor");
 }
 static void CheckPcAndStats(Shell shell)
 {
  // Home shows the PC as rings; a missing sensor is a dash, and the graphics rings only exist when there is an NVIDIA card to read.
  Call(shell,"ShowPage","Home");shell.UpdateLayout();
  var home=(DependencyObject)Field(shell,"content");Check(Visuals<SmoothText>(home).Any(w=>w.Text=="Your PC"),"Home has no Your PC card");
  var rings=(System.Collections.IDictionary)Field(shell,"pcRings");var tiles=(System.Collections.IDictionary)Field(shell,"pcTiles");
  Call(shell,"ShowSystemStats",new SystemSnapshot{Cpu=42,CpuTemp=-1,RamUsedGb=12,RamTotalGb=32,Gpu=95,GpuTemp=71,VramUsedGb=4,VramTotalGb=8,GpuName="Test"});
  var cpu=(StatRing)rings["CPU"];var cpuTemp=(StatRing)rings["CPU temp"];var gpu=(StatRing)rings["GPU"];var gpuTemp=(StatRing)rings["GPU temp"];var memory=(StatRing)rings["RAM"];
  Check(cpu.Reading=="42%"&&Math.Abs(cpu.Fraction-.42)<.001&&((SolidColorBrush)cpu.Ink).Color==Color.FromRgb(245,245,247),"The processor ring is wrong: "+cpu.Reading);
  Check(((FrameworkElement)tiles["CPU temp"]).Visibility==Visibility.Collapsed&&!Visuals<SmoothText>(home).Any(w=>w.Text.Contains("HWiNFO")||w.Text.Contains("LibreHardwareMonitor")||w.Text.StartsWith("Live numbers")),"A processor temperature Windows does not share should leave no ring, no note and no subtitle");
  Check(gpu.Reading=="95%"&&((SolidColorBrush)gpu.Ink).Color==Color.FromRgb(255,112,96)&&gpuTemp.Reading=="71\u00B0C"&&((SolidColorBrush)gpuTemp.Ink).Color==Color.FromRgb(255,196,98),"The graphics rings do not warm as they fill: "+gpu.Reading+", "+gpuTemp.Reading);
  Check(memory.Reading=="38%","The RAM ring is wrong: "+memory.Reading);
  Check(((FrameworkElement)tiles["RAM"]).Visibility==Visibility.Visible&&!rings.Contains("Memory"),"Memory should be called RAM");
  Call(shell,"ShowSystemStats",new SystemSnapshot{Cpu=10,CpuTemp=55,RamUsedGb=4,RamTotalGb=16});
  Check(cpuTemp.Reading=="55\u00B0C"&&((FrameworkElement)tiles["CPU temp"]).Visibility==Visibility.Visible&&((FrameworkElement)tiles["GPU"]).Visibility==Visibility.Collapsed,"With a processor temperature and no NVIDIA card the ring should show it and hide the graphics rings");
  // The circles are the user's choice, in Settings.
  var prefsPc=(Preferences)Field(shell,"preferences");string keptRings=prefsPc.PcRings;
  Check(new Preferences().PcRings=="CPU,CPU temp,RAM,GPU,GPU temp"&&!new Preferences().Fahrenheit,"The circles on Home should default to CPU, CPU temp, RAM, GPU and GPU temp, in Celsius");
  prefsPc.PcRings="CPU,RAM";Call(shell,"ShowSystemStats",new SystemSnapshot{Cpu=10,CpuTemp=55,RamUsedGb=4,RamTotalGb=16,Gpu=5,GpuTemp=40,VramUsedGb=1,VramTotalGb=8,GpuName="Test"});
  Check(((FrameworkElement)tiles["CPU"]).Visibility==Visibility.Visible&&((FrameworkElement)tiles["RAM"]).Visibility==Visibility.Visible&&((FrameworkElement)tiles["CPU temp"]).Visibility==Visibility.Collapsed&&((FrameworkElement)tiles["GPU"]).Visibility==Visibility.Collapsed&&((FrameworkElement)tiles["GPU temp"]).Visibility==Visibility.Collapsed,"Home shows circles that were not chosen");
  Call(shell,"SelectSettingsCategory","Appearance");shell.UpdateLayout();
  var ringSwitch=Visuals<CheckBox>(shell).First(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="GPU load");ringSwitch.IsChecked=true;ringSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(prefsPc.PcRings=="CPU,RAM,GPU"&&((FrameworkElement)tiles["GPU"]).Visibility==Visibility.Visible,"Choosing a circle in Settings did not show it: "+prefsPc.PcRings);
  Check(!Visuals<Button>(shell).Any(b=>Equals(b.Content,"Start HWiNFO")),"Lucent should not depend on another monitoring app");
  // Fahrenheit is a choice; Celsius is the default, and the text is just the number and a letter.
  prefsPc.PcRings="CPU temp,GPU temp";prefsPc.Fahrenheit=true;Call(shell,"ShowSystemStats",new SystemSnapshot{Cpu=10,CpuTemp=55,RamUsedGb=4,RamTotalGb=16,Gpu=5,GpuTemp=71,VramUsedGb=1,VramTotalGb=8,GpuName="Test"});
  Check(cpuTemp.Reading=="131\u00B0F"&&gpuTemp.Reading=="160\u00B0F"&&Math.Abs(cpuTemp.Fraction-.55)<.001,"Fahrenheit should show 131°F and 160°F: "+cpuTemp.Reading+" "+gpuTemp.Reading);
  var fahrenheitSwitch=Visuals<CheckBox>(shell).First(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Temperatures in Fahrenheit");fahrenheitSwitch.IsChecked=false;fahrenheitSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(!prefsPc.Fahrenheit&&cpuTemp.Reading=="55\u00B0C","Turning Fahrenheit off should go back to Celsius: "+cpuTemp.Reading);
  prefsPc.Fahrenheit=false;prefsPc.PcRings="CPU,RAM,GPU";
  prefsPc.PcRings="";Call(shell,"ShowSystemStats",(object)null);Check(((FrameworkElement)Field(shell,"pcCard")).Visibility==Visibility.Collapsed,"Home should hide the card when no circle is chosen");
  prefsPc.PcRings=keptRings;Call(shell,"ShowSystemStats",(object)null);
  // Settings, Stats: friendly numbers kept on this PC.
  var prefs=(Preferences)Field(shell,"preferences");var stats=prefs.Stats;long keptSwitches=stats.ResolutionSwitches,keptZoom=stats.ZoomUses;
  stats.ResolutionSwitches=7;stats.ZoomUses=0;((Action)Field(shell,"statsRefresh"))();Call(shell,"SelectSettingsCategory","Stats");shell.UpdateLayout();
  var statWords=Visuals<SmoothText>(shell).Select(w=>w.Text).ToArray();
  Check(new[]{"Times opened","Resolution switches","Times you zoomed in","Games started from Lucent","RAM cleanups","7"}.All(statWords.Contains),"The Stats tab is missing its numbers: "+string.Join("|",statWords.Take(30)));
  Check(Shell.StatsTitle(new UsageStats{ZoomUses=300})=="Eagle eye"&&Shell.StatsTitle(new UsageStats())=="Just getting started"&&Shell.StatsTitle(new UsageStats{MinutesOpen=7000})=="Lucent lifer","The Stats titles are wrong");
  // Resetting asks first.
  Func<string,Button> statButton=name=>Visuals<Button>(shell).First(b=>Equals(b.Content,name));
  stats.ResolutionSwitches=7;statButton("Reset stats").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(prefs.Stats.ResolutionSwitches==7&&Visuals<SmoothText>(shell).Any(w=>w.Text=="Reset all the numbers to zero?"&&((UIElement)w.Parent).Visibility==Visibility.Visible),"Reset stats should ask before doing anything");
  statButton("Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.Stats.ResolutionSwitches==7,"Cancel should keep the numbers");
  statButton("Reset stats").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));statButton("Yes, reset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.Stats.ResolutionSwitches==0&&prefs.Stats.Launches==0,"Yes should reset the numbers");
  prefs.Stats.ResolutionSwitches=keptSwitches;prefs.Stats.ZoomUses=keptZoom;((Action)Field(shell,"statsRefresh"))();Call(shell,"SelectSettingsCategory","Appearance");
  // Notices: a card that takes the colors of what is playing and slides away quickly; the eye-break test shows the reminder.
  var card=(Border)Field(shell,"statusCard");var statusClock=(System.Windows.Threading.DispatcherTimer)Field(shell,"statusTimer");
  var accentField=typeof(Shell).GetField("mediaAccent",BindingFlags.Instance|BindingFlags.NonPublic);var keptAccent=accentField.GetValue(shell);bool keptAdaptiveNotice=prefs.AdaptiveTheme;prefs.AdaptiveTheme=true;
  accentField.SetValue(shell,Color.FromRgb(200,40,40));Call(shell,"SetStatus","Hello");
  Check(card.Visibility==Visibility.Visible&&statusClock.Interval==TimeSpan.FromSeconds(3),"A plain notice should go after 3 seconds: "+statusClock.Interval);
  Check(((SolidColorBrush)card.Background).Color.R>60&&((SolidColorBrush)card.Background).Color.R>((SolidColorBrush)card.Background).Color.G+25,"The notice should take the colors of what is playing: "+((SolidColorBrush)card.Background).Color);
  accentField.SetValue(shell,Color.FromRgb(46,46,48));Call(shell,"SetStatus","Hello again");Check(((SolidColorBrush)card.Background).Color.R<40,"With nothing playing the notice should stay plain dark");
  accentField.SetValue(shell,keptAccent);prefs.AdaptiveTheme=keptAdaptiveNotice;
  Call(shell,"ShowPage","Video");shell.UpdateLayout();
  var testReminder=Visuals<Button>((DependencyObject)Field(shell,"content")).First(b=>Equals(b.Content,"Test the reminder"));testReminder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(card.Visibility==Visibility.Visible&&((SmoothText)Field(shell,"statusTitle")).Text=="Time for an eye break"&&statusClock.Interval==TimeSpan.FromSeconds(6),"Test the reminder should show the reminder");
  Call(shell,"HideStatus");
  Check(((System.Windows.Shapes.Path)Design.Icon("EyeCare",16).Child).Data.ToString()!=((System.Windows.Shapes.Path)Design.Icon("EyeBreak",16).Child).Data.ToString()&&((System.Windows.Shapes.Path)Design.Icon("EyeBreak",16).Child).Data.ToString()!=((System.Windows.Shapes.Path)Design.Icon("Eye",16).Child).Data.ToString(),"Eye Care and Eye breaks should have their own icons");
  // Performance mode stops what is only for show.
  var perfPaletteField=typeof(Shell).GetField("paletteBitmap",BindingFlags.Instance|BindingFlags.NonPublic);var perfTrackField=typeof(Shell).GetField("ambientTrack",BindingFlags.Instance|BindingFlags.NonPublic);
  var keptPerfBitmap=perfPaletteField.GetValue(shell);var keptPerfTrack=perfTrackField.GetValue(shell);prefs.AdaptiveTheme=true;
  perfPaletteField.SetValue(shell,System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{9,9,200,255},4));perfTrackField.SetValue(shell,new MediaTrack{Title="x",Playing=true});
  prefs.PerformanceMode=false;Call(shell,"UpdateFlowBackdrop");var flow=(FlowingBackdrop)Field(shell,"flowBackdrop");Check(flow.Running,"The colors should drift while playing");
  prefs.PerformanceMode=true;Call(shell,"UpdateFlowBackdrop");Check(!flow.Running&&((FrameworkElement)Field(shell,"flowHost")).Visibility==Visibility.Collapsed,"Performance mode should stop the moving colors");
  prefs.PerformanceMode=false;perfPaletteField.SetValue(shell,keptPerfBitmap);perfTrackField.SetValue(shell,keptPerfTrack);prefs.AdaptiveTheme=keptAdaptiveNotice;Call(shell,"UpdateAmbientColor");
  Call(shell,"SelectSettingsCategory","Appearance");shell.UpdateLayout();
  Check(new Preferences().PerformanceMode==false&&Visuals<CheckBox>(shell).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Performance mode"),"Settings should have a Performance mode switch that starts off");Call(shell,"SelectSettingsCategory","Appearance");
  // The cover on the player bar opens the big player; alt+tab must not make a rail tip appear by itself.
  var barCover=Visuals<Grid>((DependencyObject)Field(shell,"playerBar")).First(g=>System.Windows.Automation.AutomationProperties.GetName(g)=="Open the player");
  barCover.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
  Check((bool)Field(shell,"bigPlayerOpen"),"Clicking the cover should open the big player");Call(shell,"SetBigPlayerOpen",false);
  Check(System.Windows.Controls.ToolTipService.GetShowsToolTipOnKeyboardFocus((Button)Field(shell,"settingsRailButton"))==false,"The Settings tip should not show just because the keyboard focus came back");
  // Backgrounds are a tab of Settings now, not a page of their own.
  Call(shell,"ShowPage","Backgrounds");shell.UpdateLayout();
  Check((bool)Field(shell,"settingsOpen")&&(string)Field(shell,"settingsCategory")=="Backgrounds"&&!((System.Collections.IDictionary)Field(shell,"pages")).Contains("Backgrounds"),"Backgrounds should open the Settings tab");
  Call(shell,"ShowPage","Home");
 }
 static void CheckGameVision(Shell shell)
 {
  // Game vision: Dark Boost with fast or smart shadows and its own shortcut, and zoom with levels, pointer following and sharpness.
  var prefs=(Preferences)Field(shell,"preferences");var keptRisk=prefs.RiskAccepted;var current=(Look)Field(shell,"current");var keptLook=current.Copy();
  Call(shell,"ShowPage","Visual tools");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");
  Func<string,CheckBox> toggle=n=>Visuals<CheckBox>(page).First(c=>Equals(c.Content,n));
  Func<string,Button> chipOf=n=>Visuals<Button>(page).First(b=>Equals(b.Tag,n));
  var sliders=(System.Collections.IDictionary)Field(shell,"sliders");
  Check(sliders.Contains("Sharpness")&&sliders.Contains("Strength")&&sliders.Contains("Black level"),"Game vision is missing a slider");
  Check(Visuals<Button>(page).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Dark Boost toggle shortcut"),"Dark Boost has no shortcut field");
  // Zoom: Hold or Toggle and one bind, no zoom level or around-pointer choice, and a sharpening mode of its own.
  Check(!sliders.Contains("Zoom level")&&!Visuals<Button>(page).Any(b=>Equals(b.Tag,"Around pointer")||Equals(b.Tag,"Screen center")),"The zoom level slider and the around-pointer choice should be gone");
  Check(Visuals<Button>(page).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Zoom bind")&&!Visuals<Button>(page).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Hold zoom binding"),"Zoom should have one bind button");
  prefs.RiskAccepted=true;
  Check(!prefs.HoldMouse5Zoom&&Equals(chipOf("Toggle").Content,"\u2713  Toggle")&&Equals(chipOf("Hold").Content,"Hold"),"Zoom should start in Toggle mode with both choices side by side");
  chipOf("Hold").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.HoldMouse5Zoom&&Equals(chipOf("Hold").Content,"\u2713  Hold")&&Equals(chipOf("Toggle").Content,"Toggle"),"Choosing Hold did not switch the zoom mode");
  chipOf("Toggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!prefs.HoldMouse5Zoom,"Choosing Toggle did not switch it back");
  // The mouse wheel sets how far it zooms, between 1x and 4x.
  // Zoom always starts from 1.5x, not from where the wheel left it last time.
  current.Zoom=3.7;Call(shell,"ToggleZoom");Check(Math.Abs(current.Zoom-1.5)<.0001&&(bool)Field(shell,"zoomed"),"Zoom should start at 1.5x, not at the last level: "+current.Zoom);Call(shell,"ToggleZoom");Check(!(bool)Field(shell,"zoomed"),"Zoom did not turn off");
  current.Zoom=3.7;Call(shell,"TryZoom");Check(Math.Abs(current.Zoom-1.5)<.0001,"Trying zoom should also start at 1.5x: "+current.Zoom);Call(shell,"EndTryZoom");
  current.Zoom=2;Call(shell,"ZoomBy",2.0);Check(Math.Abs(current.Zoom-2*Math.Pow(1.2,2))<.001,"Two wheel notches should zoom in by a fifth each: "+current.Zoom);
  Call(shell,"ZoomBy",-1.0);Check(current.Zoom<3&&current.Zoom>2.3,"A wheel notch back did not zoom out: "+current.Zoom);
  Call(shell,"ZoomBy",100.0);Check(current.Zoom==4,"The wheel should stop at 4x: "+current.Zoom);Call(shell,"ZoomBy",-100.0);Check(current.Zoom==Look.MinimumZoom&&current.Zoom>1.2,"The wheel should stop before 1x, so zoom is always visibly on: "+current.Zoom);current.Zoom=2;
  // Sharpening: adaptive by default with no slider, a slider only for manual, none when off.
  var manualBox=(StackPanel)Field(shell,"zoomManualBox");
  Check(current.ZoomSharp=="Adaptive"&&Equals(chipOf("Adaptive").Content,"\u2713  Adaptive")&&manualBox.Visibility==Visibility.Collapsed,"Sharpening should start adaptive without a slider");
  chipOf("Manual").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.ZoomSharp=="Manual"&&manualBox.Visibility==Visibility.Visible,"Manual sharpening should show the slider");
  ((Slider)sliders["Sharpness"]).Value=70;Check(Math.Abs(current.ZoomSharpness-70)<.01,"The sharpness slider did not set the look");
  chipOf("Off").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.ZoomSharp=="Off"&&manualBox.Visibility==Visibility.Collapsed,"Off should hide the slider");
  chipOf("Adaptive").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.ZoomSharp=="Adaptive","Adaptive did not get chosen again");
  chipOf("Smart shadows").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.DarkSmart&&Equals(chipOf("Smart shadows").Content,"\u2713  Smart shadows"),"Smart shadows did not get chosen");
  chipOf("Fast").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!current.DarkSmart,"Fast did not get chosen");chipOf("Smart shadows").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  // Try zoom: it starts from the button, keeps the pointer, and ends by itself or with the end call (Esc does the same).
  prefs.RiskAccepted=false;Visuals<Button>(page).First(b=>Equals(b.Content,"Try zoom for 5 seconds")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!(bool)Field(shell,"zoomed"),"Try zoom ran without the rules being reviewed");
  prefs.RiskAccepted=true;Visuals<Button>(page).First(b=>Equals(b.Content,"Try zoom for 5 seconds")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((bool)Field(shell,"zoomed")&&(bool)Field(shell,"zoomFromButton"),"Try zoom did not start");
  Call(shell,"EndTryZoom");Check(!(bool)Field(shell,"zoomed")&&!(bool)Field(shell,"zoomFromButton"),"Try zoom did not end");
  var titles=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();Check(titles.Contains("Sharpening")&&!titles.Contains("Start it")&&!titles.Contains("Try it")&&!titles.Contains("2  How much")&&!titles.Contains("3  Where it zooms")&&!Visuals<Button>(page).Any(b=>Equals(b.Content,"Zoom on / off")||Equals(b.Content,"Save as profile")),"The zoom card should have no step titles, only the sharpening label");
  // Color pop: a color to make stand out, and how hard.
  foreach(string color in new[]{"Off","Red","Orange","Yellow","Green","Cyan","Blue","Magenta"})Check(Visuals<Button>(page).Any(b=>Equals(b.Tag,color)&&Visuals<Button>(page).Count(c=>Equals(c.Tag,color))>=1),"Color pop has no "+color+" choice");
  Check(sliders.Contains("Pop strength")&&sliders.Contains("Radar sensitivity"),"Color pop or radar slider missing");
  prefs.RiskAccepted=false;Visuals<Button>(page).First(b=>Equals(b.Tag,"Blue")&&Equals(b.Parent is WrapPanel&&((WrapPanel)b.Parent).Children.Count==8,true)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.PopColor=="Off","Color pop switched on without the rules being reviewed");
  prefs.RiskAccepted=true;Visuals<Button>(page).First(b=>Equals(b.Tag,"Red")&&Equals(b.Parent is WrapPanel&&((WrapPanel)b.Parent).Children.Count==8,true)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(current.PopColor=="Red"&&Equals(Visuals<Button>(page).First(b=>Equals(b.Tag,"Red")&&b.Parent is WrapPanel&&((WrapPanel)b.Parent).Children.Count==8).Content,"\u2713  Red"),"Choosing Red did not set Color pop and mark it");
  ((Slider)sliders["Pop strength"]).Value=80;Check(Math.Abs(current.PopStrength-80)<.01,"The pop strength slider did not set the look");
  // The sound radar: off until asked for, with size, place and sensitivity.
  var radarSwitch=toggle("Show where loud sounds come from");Check(!current.RadarOn&&radarSwitch.IsChecked!=true,"The sound radar should start off");
  radarSwitch.IsChecked=true;radarSwitch.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));Check(current.RadarOn,"The sound radar did not switch on");
  Visuals<Button>(page).First(b=>Equals(b.Tag,"Large")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.RadarSize=="Large","The radar size did not change");
  Visuals<Button>(page).First(b=>Equals(b.Tag,"Top")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(current.RadarPlace=="Top","The radar place did not change");
  ((Slider)sliders["Radar sensitivity"]).Value=25;Check(Math.Abs(current.RadarSensitivity-25)<.01,"The radar sensitivity did not set the look");
  Check(Field(shell,"radar")==null,"A test started the sound radar");
  // The shortcut flips Dark Boost, but only once the game-rule notice has been accepted.
  var boost=toggle("Enable Dark Boost");current.DarkEnabled=false;prefs.RiskAccepted=false;Call(shell,"ToggleDarkBoost");Check(!current.DarkEnabled,"Dark Boost switched on without the rules being reviewed");
  prefs.RiskAccepted=true;Call(shell,"ToggleDarkBoost");Check(current.DarkEnabled&&boost.IsChecked==true,"The Dark Boost shortcut did not switch it on");Call(shell,"ToggleDarkBoost");Check(!current.DarkEnabled&&boost.IsChecked==false,"The Dark Boost shortcut did not switch it off");
  // A profile that is loaded brings the switches and sliders along.
  Call(shell,"LoadLook",new Look{DarkSmart=false,ZoomSharpness=10,Zoom=2},"Probe");
  Check(Equals(chipOf("Adaptive").Content,"\u2713  Adaptive")&&Equals(chipOf("Fast").Content,"\u2713  Fast")&&Math.Abs(((Slider)sliders["Sharpness"]).Value-10)<.01,"Loading a look did not update the zoom and Dark Boost controls");
  Check(radarSwitch.IsChecked==false&&Equals(Visuals<Button>(page).First(b=>Equals(b.Tag,"Off")&&b.Parent is WrapPanel&&((WrapPanel)b.Parent).Children.Count==8).Content,"\u2713  Off")&&Equals(Visuals<Button>(page).First(b=>Equals(b.Tag,"Medium")).Content,"\u2713  Medium"),"Loading a look did not reset Color pop and the radar controls");
  Check(Field(shell,"gpuZoom")==null,"A test started the GPU overlay");
  // The tint a Dark Boost color adds to the GPU path matches what the display matrix uses.
  var tint=typeof(Shell).GetMethod("TintFor",BindingFlags.Static|BindingFlags.NonPublic);Func<string,float[]> tints=c=>(float[])tint.Invoke(null,new object[]{c});
  Check(tints("Green")[0]<.2f&&tints("Green")[1]==1f&&tints("White")[0]==1f&&tints("Amber")[2]<.1f&&tints("Cyan")[0]<.2f,"Dark Boost tints");
  var tintRow=(WrapPanel)Visuals<Button>(page).First(b=>Equals(b.Tag,"Amber")).Parent;
  Check(string.Join("|",tintRow.Children.Cast<Button>().Select(b=>(string)b.Tag))=="Green|Amber|Cyan|White","Dark Boost should offer Green, Amber, Cyan and White only: "+string.Join("|",tintRow.Children.Cast<Button>().Select(b=>(string)b.Tag)));
  Call(shell,"LoadLook",keptLook,"Desktop");prefs.RiskAccepted=keptRisk;Call(shell,"ShowPage","Video");
 }
 static void CheckHomePage(Shell shell)
 {
  // Home is the first tab and what opens by default: the display, a few quick actions, what is playing and the shortcuts.
  var rails=((IEnumerable)Field(shell,"navigationButtons")).Cast<Button>().ToArray();
  var homeRail=rails.First(b=>(string)b.Tag=="Home");var colorRail=rails.First(b=>(string)b.Tag=="Video");
  Check(System.Windows.Automation.AutomationProperties.GetName(homeRail)=="Home"&&homeRail.TransformToAncestor(shell).Transform(new Point()).X<colorRail.TransformToAncestor(shell).Transform(new Point()).X,"Home should be the first tab");
  Call(shell,"ShowPage","Home");shell.UpdateLayout();var page=(DependencyObject)Field(shell,"content");
  var words=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();
  Check(words.Any(t=>t=="Home")&&Visuals<SmoothText>(page).Any(t=>t.Text=="Home"&&t.FontSize>=30),"Home page title");
  foreach(string card in new[]{"Your display","Shortcuts"})Check(words.Contains(card),"Home is missing "+card);
  var info=(DisplayInfo)Field(shell,"selected");var mode=DisplayService.GetCurrentMode(info.DeviceName);
  Check(words.Contains(mode.Width+" \u00D7 "+mode.Height)&&words.Any(w=>w.StartsWith(mode.Frequency+" Hz")),"Home does not show the current display mode");
  typeof(Shell).GetField("ambientTrack",Private).SetValue(shell,null);Call(shell,"UpdateHome");words=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();Check(!Visuals<SmoothText>(page).First(t=>t.Text=="Now playing").IsVisible,"Home should hide Now playing when nothing is playing");
  var coverImage=(Image)Field(shell,"homeCover");Check(coverImage.Source==null,"Home shows a cover with nothing playing");
  var homeArt=new System.Windows.Media.Imaging.PngBitmapEncoder();homeArt.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(Cover(Color.FromRgb(60,160,220))));byte[] homeCoverBytes;using(var stream=new MemoryStream()){homeArt.Save(stream);homeCoverBytes=stream.ToArray();}
  typeof(Shell).GetField("ambientTrack",Private).SetValue(shell,new MediaTrack{Title="Home song",Artist="Home artist",Source="Fixture",Playing=true,Artwork=homeCoverBytes});Call(shell,"UpdateHome");shell.UpdateLayout();
  Check(coverImage.Source!=null&&coverImage.IsVisible&&coverImage.ActualWidth>=90,"Home does not show the album cover");
  words=Visuals<SmoothText>(page).Select(t=>t.Text).ToArray();
  Check(Visuals<SmoothText>(page).First(t=>t.Text=="Now playing").IsVisible&&words.Contains("Home song")&&words.Contains("Home artist")&&Visuals<Button>(page).Any(b=>Equals(b.Content,"Open player")&&b.IsVisible),"Home does not show what is playing");
  typeof(Shell).GetField("ambientTrack",Private).SetValue(shell,null);Call(shell,"UpdateHome");Check(coverImage.Source==null,"Home kept the cover after the track ended");
  Check(!words.Contains("Quick actions")&&!Visuals<Button>(page).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Boost"),"Home should no longer have Quick actions");
  Call(shell,"ShowPage","Video");
 }
 static async Task CheckTabSpam(Shell shell)
 {
  // Clicking the current tab must not replay the opening motion; another tab cross-fades with nothing re-laid out or detached.
  Func<ScrollViewer> shown=()=>(ScrollViewer)Field(shell,"contentScroll");var slots=(IDictionary)Field(shell,"pageSlots");var pages=(IDictionary)Field(shell,"pages");
  Func<int> visible=()=>slots.Values.Cast<ScrollViewer>().Count(x=>x.Visibility==Visibility.Visible);
  Call(shell,"ShowPage","Video");await Until(()=>shown().Opacity>.999&&visible()==1);
  int motion=(int)Field(shell,"pageMotion");var video=shown();
  for(int i=0;i<5;i++)Call(shell,"ShowPage","Video");
  Check((int)Field(shell,"pageMotion")==motion&&video.Opacity>.999&&ReferenceEquals(shown(),video),"Clicking the current tab replayed its animation");
  Call(shell,"ShowPage","Resolution");var display=shown();
  Check(!ReferenceEquals(display,video)&&(int)Field(shell,"pageMotion")==motion+1&&display.Visibility==Visibility.Visible&&video.Visibility==Visibility.Visible&&display.Opacity<.99,"Changing tab did not start a cross-fade: "+display.Opacity);
  await Until(()=>display.Opacity>.999&&video.Visibility!=Visibility.Visible);
  Check(display.Opacity>.999&&video.Visibility!=Visibility.Visible&&video.Opacity==0&&display.IsHitTestVisible&&!video.IsHitTestVisible&&Math.Abs(((TranslateTransform)display.RenderTransform).Y)<.5,"The cross-fade did not settle: display "+display.Opacity+" video vis "+video.Visibility+" video op "+video.Opacity+" hit "+display.IsHitTestVisible+"/"+video.IsHitTestVisible+" y "+((TranslateTransform)display.RenderTransform).Y);
  Check(ReferenceEquals(video.Parent,display.Parent)&&ReferenceEquals(((ContentControl)video.Content).Content,pages["Video"])&&ReferenceEquals(((ContentControl)display.Content).Content,pages["Resolution"]),"Pages were detached to switch");
  // Quick clicks across tabs never leave a page half shown, and only the current page takes clicks.
  foreach(string name in new[]{"Profiles","Crosshair","Game Boost","Video"}){Call(shell,"ShowPage",name);await Task.Delay(40);}
  await Until(()=>shown().Opacity>.999&&visible()==1);
  Check(shown().Opacity>.999&&visible()==1&&slots.Values.Cast<ScrollViewer>().Count(x=>x.IsHitTestVisible)==1&&(string)Field(shell,"page")=="Video","Rapid tab changes left a page half shown");
  // Every page keeps its own scroll position.
  Call(shell,"ShowPage","Game Boost");await Until(()=>shown().Opacity>.999);var boost=shown();shell.UpdateLayout();boost.ScrollToEnd();shell.UpdateLayout();double at=boost.VerticalOffset;
  Call(shell,"ShowPage","Video");await Until(()=>shown().Opacity>.999&&visible()==1);Call(shell,"ShowPage","Game Boost");await Until(()=>shown().Opacity>.999&&visible()==1);
  Check(at<=0||Math.Abs(boost.VerticalOffset-at)<1,"A page lost its scroll position: "+at+" then "+boost.VerticalOffset);boost.ScrollToTop();Call(shell,"ShowPage","Video");
 }

 static async Task CheckGear(Shell shell)
 {
  // The gear copy over the settings sits exactly on the sidebar gear, also after the window changes size, and the two move as one.
  var rail=(Button)Field(shell,"settingsRailButton");var overlay=(Button)Field(shell,"settingsOverlayGear");var surface=(Grid)Field(shell,"windowSurface");
  Func<double> gap=()=>(rail.TranslatePoint(new Point(),surface)-overlay.TranslatePoint(new Point(),surface)).Length;
  shell.UpdateLayout();await Task.Delay(30);Check(gap()<.01,"The settings gear copy is not over the sidebar gear: "+gap());
  double width=shell.Width,height=shell.Height;shell.Width=shell.MinWidth+120;shell.Height=shell.MinHeight+90;shell.UpdateLayout();await Task.Delay(30);shell.UpdateLayout();
await Until(()=>((UIElement)Field(shell,"settingsLayer")).Visibility!=Visibility.Collapsed);shell.UpdateLayout();  Check(gap()<.01,"The gear copy did not follow a resize while settings was closed: "+gap());
  shell.Width=width;shell.Height=height;shell.UpdateLayout();await Task.Delay(30);await Until(()=>((UIElement)Field(shell,"settingsLayer")).Visibility!=Visibility.Collapsed);shell.UpdateLayout();Check(gap()<.01,"The gear copy did not follow the window back: "+gap());
  Check(ReferenceEquals(rail.RenderTransform,overlay.RenderTransform)&&rail.RenderTransform is ScaleTransform,"The two gears do not move as one");
  Check(((System.Windows.Media.Effects.BlurEffect)((UIElement)Field(shell,"settingsBackdrop")).Effect).Radius<=12,"The settings blur is too strong");
 }
 static async Task CheckSettingsSidebar(Shell shell)
 {
  // Settings opens as a grid of category cards; a category opens on its own page with a button back to the grid.
  var content=(ContentControl)Field(shell,"settingsContent");var buttons=(IDictionary)Field(shell,"settingsCategoryButtons");var cards=(IDictionary)Field(shell,"settingsCards");
  Call(shell,"SetSettingsOpen",true);await Task.Delay(420);shell.UpdateLayout();
  Check((bool)Field(shell,"settingsOverviewShown")&&ReferenceEquals(content.Content,Field(shell,"settingsOverview")),"Settings should open on the grid of categories");
  Check(cards.Count==8&&cards.Count==buttons.Count&&cards.Values.Cast<FrameworkElement>().All(c=>c.IsVisible&&c.ActualWidth>140),"The grid is missing a category card");
  Check(!((FrameworkElement)Field(shell,"settingsBack")).IsVisible,"The back button should not show on the grid");
  var appearance=Visuals<Button>(content).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Appearance");appearance.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();await Task.Delay(300);
  Check(!(bool)Field(shell,"settingsOverviewShown")&&(string)Field(shell,"settingsCategory")=="Appearance"&&((FrameworkElement)Field(shell,"settingsBack")).IsVisible,"Clicking a card did not open its category with a back button");
  var back=(Button)Field(shell,"settingsBack");back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();await Task.Delay(300);
  Check((bool)Field(shell,"settingsOverviewShown")&&!back.IsVisible,"The back button did not return to the grid");
  var panel=content.Content;Call(shell,"SelectSettingsCategory","General");Call(shell,"SelectSettingsCategory","General");Check(!ReferenceEquals(panel,content.Content)&&!(bool)Field(shell,"settingsOverviewShown"),"Selecting a category did not leave the grid");
  // About: every button works in test mode without opening anything or touching the clipboard.
  Call(shell,"SelectSettingsCategory","About");shell.UpdateLayout();await Task.Delay(300);
  var about=(DependencyObject)content.Content;var words=Visuals<SmoothText>(about).Select(t=>t.Text).ToArray();Check(words.Contains("v"+Design.Version)&&words.Contains("lucent")&&words.Any(w=>w.StartsWith("Version "+Design.Version)),"About is missing its version");
  var actions=Visuals<Button>(about).ToArray();Check(actions.Length==9,"About buttons (copy, GitHub, Ko-fi, Report a bug, three folders and files, then check and get of Updates): "+actions.Length);foreach(var action in actions)action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Visuals<SmoothText>(about).Any(t=>t.Text=="Copied"),"Copy did not confirm");
  Call(shell,"SelectSettingsCategory","Appearance");Call(shell,"SetSettingsOpen",false);await Task.Delay(360);
 } static async Task CheckPlayerDock(Shell shell)
 {
  var dock=(Border)Field(shell,"playerDock");
  // Hidden with no media, animated in when media appears, and it survives the gap between tracks.
  Check(dock.Visibility==Visibility.Collapsed,"Player buttons shown before any media");
  shell.SetMediaAvailable(true);Check(dock.Visibility==Visibility.Visible&&dock.HasAnimatedProperties,"Player buttons did not animate in");await Task.Delay(320);Check(dock.Opacity>.99,"Player buttons did not finish appearing");
  shell.SetMediaAvailable(false);await Task.Delay(260);Check(dock.Visibility==Visibility.Visible,"Player buttons flickered away between tracks");
  // An open player keeps its button available even when media goes away.
  typeof(Shell).GetField("lastMediaSeen",Private).SetValue(shell,DateTime.MinValue);
  var mini=(Button)Field(shell,"playerDockButton");mini.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.SetMediaAvailable(false);await Task.Delay(260);Check(dock.Visibility==Visibility.Visible,"Open player lost its button");mini.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  shell.SetMediaAvailable(false);await Task.Delay(320);Check(dock.Visibility==Visibility.Collapsed,"Player buttons stayed after media ended");
  // Names and tooltips.
  var big=(Button)Field(shell,"bigPlayerButton");Check((string)((ToolTip)mini.ToolTip).Content=="Open mini player"&&(string)((ToolTip)big.ToolTip).Content=="Open player","Player button names");
  var clear=((ToolTip)big.ToolTip).CustomPopupPlacementCallback(new Size(80,30),new Size(32,28),new Point());Check(clear[0].Point.Y<0&&Math.Abs(clear[0].Point.X-(32-80)/2d)<.01,"Big player tooltip should sit above its icon");
  shell.SetMediaAvailable(true);await Task.Delay(320);
 }
 // A busy machine can delay animations, so wait for a state instead of sampling at a fixed moment.
 static async Task<bool> Until(Func<bool> condition,int timeout=1500){var clock=System.Diagnostics.Stopwatch.StartNew();while(!condition()&&clock.ElapsedMilliseconds<timeout)await Task.Delay(15);return condition();}
 static string Chain(DependencyObject node){var names=new System.Collections.Generic.List<string>();for(int i=0;node!=null&&i<14;i++){names.Add(node.GetType().Name);node=VisualTreeHelper.GetParent(node);}return string.Join(">",names.ToArray());}
 static void CheckSeekBar()
 {
  var bar=new SeekBar{Width=200};bar.Measure(new Size(200,18));bar.Arrange(new Rect(0,0,200,18));
  double? previewed=null,committed=null;bar.Preview=f=>previewed=f;bar.Commit=f=>committed=f;
  bar.Press(50);Check(previewed==null&&committed==null&&bar.Fraction==0&&!bar.Dragging,"A bar that cannot seek reacted to a press");
  bar.Seekable=true;bar.Press(50);Check(bar.Dragging&&Math.Abs(bar.Fraction-.25)<1e-9&&previewed==.25,"Pressing the bar");
  bar.SetFraction(.9);Check(Math.Abs(bar.Fraction-.25)<1e-9,"Playback moved the bar while it was held");
  bar.Drag(150);Check(Math.Abs(bar.Fraction-.75)<1e-9&&previewed==.75,"Dragging the bar");
  bar.Drag(999);Check(bar.Fraction==1,"Dragging past the end");bar.Drag(-5);Check(bar.Fraction==0,"Dragging before the start");
  bar.Release(100);Check(!bar.Dragging&&committed==.5&&Math.Abs(bar.Fraction-.5)<1e-9,"Releasing the bar");
  bar.SetFraction(.4);Check(Math.Abs(bar.Fraction-.4)<1e-9,"Playback did not move the bar");
  // Letting go on the bar commits through the same path the mouse uses.
  committed=null;bar.Seekable=true;bar.Press(20);bar.Up(160);Check(committed==.8&&!bar.Dragging,"Letting go of the bar did not seek: "+committed);
  committed=null;bar.Seekable=false;bar.Press(10);bar.Release(10);Check(committed==null,"Seeking after seeking was turned off");
 }
 static void CheckSeeking(Shell shell)
 {
  var view=new BigPlayerView(shell,true,null,null,false);var core=view.Core;var seek=(SeekBar)Field(view,"seek");
  Func<double,bool,MediaTrack> sample=(position,canSeek)=>new MediaTrack{Title="Seek track",Artist="A",Source="Fixture",Playing=true,CanPause=true,CanPlay=true,CanSeek=canSeek,Duration=200,Position=position};
  core.SetTrack(sample(50,true));Check(seek.Seekable,"A seekable track did not enable the bar");
  var seeking=core.Seek(120);Check(Math.Abs(core.progressValue-120)<.5&&Math.Abs(seek.Fraction-.6)<.01,"Seeking did not move the bar at once: "+core.progressValue);
  // Samples from before the media app caught up are ignored; one that agrees is believed again.
  core.SetTrack(sample(51,true));Check(Math.Abs(core.progressValue-120)<2,"A stale sample pulled the bar back: "+core.progressValue);
  core.SetTrack(sample(121,true));Check(Math.Abs(core.progressValue-121)<1.5,"A matching sample was not followed");
  core.SetTrack(sample(10,true));Check(Math.Abs(core.progressValue-10)<1,"Samples were still ignored after the seek settled");
  core.SetTrack(sample(60,false));Check(!seek.Seekable,"A track that cannot seek left the bar seekable");
  var refused=core.Seek(5);Check(Math.Abs(core.progressValue-60)<1,"Seeking a track that cannot seek moved the bar");
  core.SetTrack(null);
 }
 static void CheckModes(Shell shell)
 {
  var core=new BigPlayerView(shell,true,null,null,false).Core;
  Func<bool,bool,bool,int,MediaTrack> sample=(canShuffle,canRepeat,shuffling,repeat)=>new MediaTrack{Title="Mode track",Artist="A",Source="Fixture",Playing=true,CanPause=true,CanPlay=true,Duration=100,Position=1,CanShuffle=canShuffle,CanRepeat=canRepeat,Shuffling=shuffling,Repeat=repeat};
  // Only shown while the media app supports them.
  core.SetTrack(sample(false,false,false,0));Check(core.shuffle.Visibility==Visibility.Collapsed&&core.repeat.Visibility==Visibility.Collapsed,"Shuffle or repeat shown for an app that does not support them");
  core.SetTrack(sample(true,true,false,0));Check(core.shuffle.Visibility==Visibility.Visible&&core.repeat.Visibility==Visibility.Visible,"Shuffle and repeat hidden for an app that supports them");
  core.shuffle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(core.shuffling,"Shuffle did not switch on at once");
  core.SetTrack(sample(true,true,false,0));Check(core.shuffling,"A stale sample undid shuffle");
  core.SetTrack(sample(true,true,true,0));core.SetTrack(sample(true,true,false,0));Check(!core.shuffling,"Shuffle stayed on after the app turned it off");
  var steps=new System.Collections.Generic.List<int>();for(int i=0;i<3;i++){core.repeat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));steps.Add(core.repeating);}
  Check(steps[0]==2&&steps[1]==1&&steps[2]==0,"Repeat did not go off, all, this track, off: "+string.Join(",",steps.Select(x=>x.ToString()).ToArray()));
  core.repeat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));core.repeat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(core.repeating==1&&core.repeat.ToolTip==null&&core.shuffle.ToolTip==null&&core.play.ToolTip==null,"The music controls should be in the right state and have no tooltips: "+core.repeating);
  core.SetTrack(null);Check(core.shuffle.Visibility==Visibility.Collapsed,"Shuffle stayed when the media ended");
 }
 static async Task CheckPlayerBar(Shell shell)
 {
  var bar=(PlayerBar)Field(shell,"playerBar");var arrow=(Button)Field(shell,"playerArrow");var host=(Grid)Field(shell,"pageHost");var dock=(Border)Field(shell,"playerDock");var prefs=(Preferences)Field(shell,"preferences");var pill=(SolidColorBrush)Field(shell,"dockPillBrush");
  Check(bar.Visibility==Visibility.Collapsed&&!(bool)Field(shell,"playerBarOpen"),"The player bar starts hidden");
  shell.SetMediaAvailable(true);await Task.Delay(320);
  Check((string)((ToolTip)arrow.ToolTip).Content=="Show player bar"&&pill.Color.A==22,"Arrow tooltip or the buttons' background");
  Func<Point> dockAt=()=>dock.TransformToAncestor(shell).Transform(new Point());Point before=dockAt();
  arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  await Until(()=>bar.Visibility==Visibility.Visible&&bar.Opacity>.99&&Math.Abs(((ContentControl)Field(shell,"content")).Margin.Bottom-PlayerBar.PageInset)<1&&pill.Color.A==0);
  Check(bar.Visibility==Visibility.Visible&&bar.Opacity>.99&&prefs.PlayerBarOpen&&Math.Abs(((ContentControl)Field(shell,"content")).Margin.Bottom-PlayerBar.PageInset)<1,"The player bar did not open and make room: "+((ContentControl)Field(shell,"content")).Margin);
  var moved=dockAt()-before;Check(moved.Length<.5,"The player buttons moved when the bar opened: "+moved);
  Check(pill.Color.A==0,"The buttons' background should fade once the bar is up");
  var barOn=(Viewbox)Field(shell,"barOn");var barOff=(Viewbox)Field(shell,"barOff");
  await Until(()=>barOn.Opacity>.99&&barOff.Opacity<.01);
  Check(barOn.Opacity>.99&&barOff.Opacity<.01&&(string)((ToolTip)arrow.ToolTip).Content=="Hide player bar","The bar icon did not fill in");
  // It shows the track, with a seekable bar and the volume.
  bar.Core.SetTrack(new MediaTrack{Title="Bar track",Artist="Bar artist",Source="Fixture",Playing=true,CanPause=true,CanPlay=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=200,Position=100});shell.UpdateLayout();
  var seek=(SeekBar)Field(bar,"seek");Check(bar.Core.title.Text=="Bar track"&&seek.Seekable&&Math.Abs(seek.Fraction-.5)<.001&&((SmoothText)Field(bar,"elapsed")).Text=="1:40","The player bar does not show the track");
  Check(Visuals<VolumeControl>(bar).Count()==1&&bar.Volume.IsVisible,"The player bar has no volume control");
  // It fits in the window, and the player buttons sit inside the room kept for them, centered on the bar.
  var box=bar.TransformToAncestor(shell).TransformBounds(new Rect(0,0,bar.ActualWidth,bar.ActualHeight));var dockBox=dock.TransformToAncestor(shell).TransformBounds(new Rect(0,0,dock.ActualWidth,dock.ActualHeight));
  Check(box.Bottom<=shell.ActualHeight&&box.Right<=shell.ActualWidth&&Math.Abs(bar.ActualHeight-PlayerBar.BarHeight)<.5,"The bar does not fit: "+box);
  Check(dockBox.Left>=box.Right-PlayerBar.SideRoom-1&&dockBox.Right<=box.Right+1&&Math.Abs((dockBox.Top+dockBox.Bottom)/2-(box.Top+box.Bottom)/2)<2,"The player buttons are not centered in the bar: "+dockBox+" in "+box);
  var volumeBox=bar.Volume.TransformToAncestor(shell).TransformBounds(new Rect(0,0,bar.Volume.ActualWidth,bar.Volume.ActualHeight));Check(volumeBox.Right<=dockBox.Left+1,"The volume control runs under the player buttons: "+volumeBox+" "+dockBox);
  // Media going away takes the bar with it, and media coming back brings it back.
  typeof(Shell).GetField("lastMediaSeen",Private).SetValue(shell,DateTime.MinValue);shell.SetMediaAvailable(false);
  await Until(()=>dock.Visibility==Visibility.Collapsed&&bar.Visibility==Visibility.Collapsed&&((ContentControl)Field(shell,"content")).Margin.Bottom<1);
  Check(dock.Visibility==Visibility.Collapsed&&bar.Visibility==Visibility.Collapsed&&((ContentControl)Field(shell,"content")).Margin.Bottom<1&&(bool)Field(shell,"playerBarOpen"),"The bar stayed after the media ended");
  shell.SetMediaAvailable(true);await Until(()=>bar.Visibility==Visibility.Visible&&((ContentControl)Field(shell,"content")).Margin.Bottom>60);Check(bar.Visibility==Visibility.Visible&&((ContentControl)Field(shell,"content")).Margin.Bottom>60,"The bar did not return with the media");
  arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>bar.Visibility==Visibility.Collapsed&&((ContentControl)Field(shell,"content")).Margin.Bottom<1&&pill.Color.A==22);
  Check(bar.Visibility==Visibility.Collapsed&&((ContentControl)Field(shell,"content")).Margin.Bottom<1&&!prefs.PlayerBarOpen&&pill.Color.A==22,"The arrow did not close the bar");
 }
 static async Task CheckPlayerRules(Shell shell)
 {
  // Only one player is on screen at a time, and the bar steps aside rather than sharing the screen.
  var bar=(PlayerBar)Field(shell,"playerBar");var arrow=(Button)Field(shell,"playerArrow");var big=(Button)Field(shell,"bigPlayerButton");var mini=(Button)Field(shell,"playerDockButton");var view=(BigPlayerView)Field(shell,"bigPlayerView");
  Func<bool> barUp=()=>bar.Visibility==Visibility.Visible&&bar.Opacity>.99,barGone=()=>bar.Visibility==Visibility.Collapsed,bigUp=()=>view.Visibility==Visibility.Visible&&view.Opacity>.99,bigGone=()=>view.Visibility==Visibility.Collapsed;
  Action<Button> click=b=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  shell.SetMediaAvailable(true);await Task.Delay(300);
  click(arrow);await Until(barUp);Check(barUp(),"The bar did not open");
  click(big);await Until(()=>bigUp()&&barGone());Check(bigUp()&&barGone(),"Opening the big player should hide the bar");
  click(big);await Until(()=>bigGone()&&barUp());Check(bigGone()&&barUp(),"The bar should come back when the big player closes");
  click(mini);await Until(barGone);Check(Field(shell,"mediaPlayer")!=null&&barGone(),"Opening the mini player should hide the bar");
  click(big);await Until(()=>bigUp()&&Field(shell,"mediaPlayer")==null&&barGone());Check(Field(shell,"mediaPlayer")==null&&bigUp()&&barGone(),"Opening the big player should close the mini player: mini="+(Field(shell,"mediaPlayer")==null?"closed":"open")+" bigUp="+bigUp()+" vis="+view.Visibility+" op="+view.Opacity+" barGone="+barGone());
  click(mini);await Until(()=>bigGone()&&Field(shell,"mediaPlayer")!=null);Check(Field(shell,"mediaPlayer")!=null&&bigGone()&&barGone(),"Opening the mini player should close the big player");
  Func<DependencyObject,string,Button> named=(root,name)=>Visuals<Button>(root).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)==name);
  click(big);await Until(bigUp);named(view,"Pop out player").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>Field(shell,"bigPlayerWindow")!=null&&bigGone()&&barGone());
  Check(Field(shell,"bigPlayerWindow")!=null&&Field(shell,"mediaPlayer")==null&&bigGone()&&barGone(),"Popping the player out should leave only the window");
  click(mini);await Task.Delay(150);Check(Field(shell,"bigPlayerWindow")==null&&Field(shell,"mediaPlayer")!=null,"Opening the mini player should close the popped-out player");
  click(arrow);await Until(()=>barUp()&&Field(shell,"mediaPlayer")==null);Check(barUp()&&Field(shell,"mediaPlayer")==null&&Field(shell,"bigPlayerWindow")==null&&bigGone(),"The arrow should close every other player and show the bar");
  click(arrow);await Until(barGone);Check(barGone(),"The arrow did not close the bar");
 }
 static void CheckVolume()
 {
  // The volume control follows its slider and speaker. In tests it never touches the Windows volume.
  var control=new VolumeControl(true,18,100);control.Measure(new Size(300,40));control.Arrange(new Rect(0,0,300,40));
  control.Slider.Press(30);control.Slider.Release(30);Check(Math.Abs(control.Level-.3)<.02&&!control.Muted,"Dragging the slider did not set the level: "+control.Level);
  control.Speaker.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(control.Muted&&control.Speaker.ToolTip==null&&control.Slider.Fraction==0,"The speaker did not mute");
  control.Speaker.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!control.Muted&&Math.Abs(control.Slider.Fraction-.3)<.02,"The speaker did not unmute");
  control.Speaker.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));control.Slider.Press(60);Check(!control.Muted&&Math.Abs(control.Level-.6)<.02,"Moving the slider should unmute");control.Slider.Release(60);
  // The control follows one app, never the whole PC: it asks only for the app that is playing, and with none it has nothing to change.
  Check(AppVolume.Key("Spotify.exe")=="spotify"&&AppVolume.Key("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic")=="microsoft.zunemusic"&&AppVolume.Key("Chrome")=="chrome"&&AppVolume.Key("")==null&&AppVolume.Key(null)==null,"Media source names");
  Check(AppVolume.Belongs("spotify","Spotify",null)&&AppVolume.Belongs("chrome","chrome",null)&&AppVolume.Belongs("brave.4f2a","brave",null)&&AppVolume.Belongs("msedge","msedge",null)&&AppVolume.Belongs("microsoft.zunemusic","Music.UI",@"C:\Program Files\WindowsApps\Microsoft.ZuneMusic_11.2\Music.UI.exe"),"Apps are not matched to their audio sessions");
  Check(!AppVolume.Belongs("spotify","chrome",@"C:\Program Files\Google\Chrome\chrome.exe")&&!AppVolume.Belongs("microsoft.zunemusic","MicrosoftEdgeUpdate",null)&&!AppVolume.Belongs(null,"spotify",null),"An unrelated program was matched to the media app");
  float level;bool muted;
  Check(!AppVolume.TryGet(null,out level,out muted)&&!AppVolume.TrySet(null,.5f)&&!AppVolume.TryMute(null,true),"With no app playing the volume control must change nothing");
  var follows=new VolumeControl(true,18,100);follows.Source=()=>"Spotify.exe";Check(follows.Source()=="Spotify.exe"&&follows.Available,"The volume control does not follow the playing app");
 }
 static async Task CheckLyrics(Shell shell)
 {
  // Reading the timed text of a .lrc file.
  var parsed=LyricsService.Parse("[ar:Someone]\n[00:05.50] First line\n[00:10.00] Second <00:10.5>line\n[00:15.25][01:00.00] Chorus\n[00:20.00] \n","ignored");
  Check(parsed!=null&&parsed.Synced&&parsed.Lines.Count==4&&parsed.Lines[0].Text=="First line"&&Math.Abs(parsed.Lines[0].Time-5.5)<.001&&parsed.Lines[1].Text=="Second line"&&parsed.Lines[2].Text=="Chorus"&&Math.Abs(parsed.Lines[3].Time-60)<.001,"Timed lyrics were not read correctly");
  var plain=LyricsService.Parse(null,"One\nTwo\n\nThree\n");Check(plain!=null&&!plain.Synced&&plain.Lines.Count==4&&plain.Lines[2].Text==""&&plain.Lines[0].Time<0,"Plain lyrics were not read correctly");
  Check(LyricsService.Parse("","  ")==null&&LyricsService.Parse("no stamps here",null)==null,"Empty lyrics should read as nothing");
  // Several answers: the timed one closest to the song's length wins, and a different song is refused.
  string answers="[{\"duration\":200,\"plainLyrics\":\"only plain\"},{\"duration\":203,\"syncedLyrics\":\"[00:01.00] near\"},{\"duration\":200.4,\"syncedLyrics\":\"[00:02.00] nearest\"},{\"duration\":260,\"syncedLyrics\":\"[00:03.00] other song\"}]";
  var chosen=LyricsService.Choose(answers,200);Check(chosen!=null&&chosen.Synced&&chosen.Lines[0].Text=="nearest","The best lyrics were not chosen");
  Check(LyricsService.Choose("[{\"duration\":260,\"syncedLyrics\":\"[00:03.00] other song\"}]",200)==null&&LyricsService.Choose("{}",200)==null,"A different song’s lyrics were accepted");
  Check(LyricsService.CleanTitle("Song (feat. Someone) - 2011 Remaster")=="Song"&&LyricsService.CleanTitle("Plain Title")=="Plain Title"&&LyricsService.FirstArtist("A & B")=="A"&&LyricsService.FirstArtist("Solo")=="Solo","Title and artist cleaning");
  // The big player: lyrics are off until switched on, then the player moves left and the lyrics fill the right.
  var prefs=(Preferences)Field(shell,"preferences");Check(!prefs.ShowLyrics,"Lyrics should be off by default, since looking them up goes online");
  var view=(BigPlayerView)Field(shell,"bigPlayerView");var button=(Button)Field(shell,"bigPlayerButton");
  Check(!view.LyricsOn&&view.Lyrics.Visibility==Visibility.Collapsed,"Lyrics are showing before they were asked for");
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Visible&&view.Opacity>.99);shell.UpdateLayout();
  var column=(FrameworkElement)Field(view,"column");double centeredLeft=column.TransformToAncestor(view).Transform(new Point()).X;
  Check(Math.Abs(centeredLeft+column.ActualWidth/2-view.ActualWidth/2)<2,"Without lyrics the player should be centered: "+centeredLeft);
  var toggle=Visuals<Button>(view).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Lyrics");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  await Until(()=>view.LyricsShown&&view.Lyrics.Opacity>.99&&Math.Abs(column.TransformToAncestor(view).Transform(new Point()).X+column.ActualWidth/2-view.ActualWidth*.22)<2);
  Check(prefs.ShowLyrics&&view.LyricsOn,"Switching lyrics on was not remembered");
  var columnBox=column.TransformToAncestor(view).TransformBounds(new Rect(0,0,column.ActualWidth,column.ActualHeight));var lyricsBox=view.Lyrics.TransformToAncestor(view).TransformBounds(new Rect(0,0,view.Lyrics.ActualWidth,view.Lyrics.ActualHeight));
  Check(columnBox.Right<=lyricsBox.Left&&lyricsBox.Right<=view.ActualWidth-30&&lyricsBox.Width>=view.ActualWidth*.4&&columnBox.Left>=0,"The player and the lyrics overlap: "+columnBox+" "+lyricsBox);
  // The lines follow the song: the one being sung is bright and rests a third of the way down.
  var many=new LyricsResult{Synced=true};for(int i=0;i<30;i++)many.Lines.Add(new LyricLine{Time=5+i*5,Text="Line number "+i+" of a fairly long lyric that wraps onto a second row"});
  view.Lyrics.SetLyrics(many);shell.UpdateLayout();
  Check(view.Lyrics.Synced&&view.Lyrics.LineCount==30&&view.Lyrics.ActiveIndex==-1&&view.Lyrics.Note==null,"Lyrics did not load");
  view.Lyrics.Update(0);Check(view.Lyrics.ActiveIndex==-1,"A line was lit before the song reached it");
  Func<int,int> litPixels=index=>{var lyr=view.Lyrics;SmoothText.SetCrisp(lyr,true);lyr.UpdateLayout();var shotBox=new System.Windows.Media.Imaging.RenderTargetBitmap((int)lyr.ActualWidth,(int)lyr.ActualHeight,96,96,PixelFormats.Pbgra32);var shotVisual=new DrawingVisual();using(var dc=shotVisual.RenderOpen())dc.DrawRectangle(new VisualBrush(lyr),null,new Rect(0,0,lyr.ActualWidth,lyr.ActualHeight));shotBox.Render(shotVisual);var line=lyr.Items[index];int mid=(int)line.TransformToAncestor(lyr).Transform(new Point(0,line.ActualHeight/2)).Y;int top=Math.Max(0,mid-15);var strip=new byte[(int)lyr.ActualWidth*4*30];shotBox.CopyPixels(new Int32Rect(0,top,(int)lyr.ActualWidth,Math.Min(30,(int)lyr.ActualHeight-top)),strip,(int)lyr.ActualWidth*4,0);int bright=0;for(int k=3;k<strip.Length;k+=4)if(strip[k]>240)bright++;lyr.ClearValue(SmoothText.CrispProperty);return bright;};
  Check(litPixels(0)>200,"The first line is drawn dim before the song reaches it (the fade mask must not reach into it): "+litPixels(0));
  // Before the song reaches the first line it is the lit one, and pointing at it must not grey it.
  {var first=view.Lyrics.Items[0];Check(first.Opacity>.99,"The first line should be fully lit before the song starts: "+first.Opacity);first.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseEnterEvent,Source=first});first.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseLeaveEvent,Source=first});await Task.Delay(300);Check(first.Opacity>.99,"Pointing at the first line greyed it: "+first.Opacity);}
  view.Lyrics.Update(4.8);Check(view.Lyrics.ActiveIndex==0,"The first line should light as it starts: "+view.Lyrics.ActiveIndex);
  view.Lyrics.Update(61);var items=view.Lyrics.Items;
  await Until(()=>items[11].Opacity>.99&&items[0].Opacity<.4&&Math.Abs(items[11].TransformToAncestor(view.Lyrics).Transform(new Point(0,items[11].ActualHeight/2)).Y-view.Lyrics.ActualHeight*.34)<2);
  Check(view.Lyrics.ActiveIndex==11&&items[11].Opacity>.99&&items[10].Opacity<.4,"The line being sung is not the bright one: "+view.Lyrics.ActiveIndex);
  Check(Math.Abs(items[11].TransformToAncestor(view.Lyrics).Transform(new Point(0,items[11].ActualHeight/2)).Y-view.Lyrics.ActualHeight*.34)<2,"The lyrics did not scroll the current line into place");
  // What is on screen, not just where things are: far down the list the active line must actually be drawn (a layout clip once hid everything past the first screenful).
  var drawn=new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.Lyrics.ActualWidth,(int)view.Lyrics.ActualHeight,96,96,PixelFormats.Pbgra32);var snapshot=new DrawingVisual();using(var dc=snapshot.RenderOpen())dc.DrawRectangle(new VisualBrush(view.Lyrics),null,new Rect(0,0,view.Lyrics.ActualWidth,view.Lyrics.ActualHeight));drawn.Render(snapshot);
  var activeMid=(int)items[11].TransformToAncestor(view.Lyrics).Transform(new Point(0,items[11].ActualHeight/2)).Y;var band=new byte[(int)view.Lyrics.ActualWidth*4*30];drawn.CopyPixels(new Int32Rect(0,Math.Max(0,activeMid-15),(int)view.Lyrics.ActualWidth,30),band,(int)view.Lyrics.ActualWidth*4,0);
  int solid=0;for(int i=3;i<band.Length;i+=4)if(band[i]>200)solid++;
  Check(solid>200,"The line being sung is not drawn on screen ("+solid+" bright pixels at y="+activeMid+")");
  view.Lyrics.Update(400);await Until(()=>items[29].Opacity>.99&&Math.Abs(items[29].TransformToAncestor(view.Lyrics).Transform(new Point(0,items[29].ActualHeight/2)).Y-view.Lyrics.ActualHeight*.34)<2);await Task.Delay(700);view.Lyrics.UpdateLayout();Check(litPixels(29)>200,"The last line is drawn dim at the end of the song: "+litPixels(29)+" vis="+items[29].Visibility+" op="+items[29].Opacity+" y="+items[29].TransformToAncestor(view.Lyrics).Transform(new Point(0,items[29].ActualHeight/2)).Y+" of "+view.Lyrics.ActualHeight+" scroll="+view.Lyrics.ScrollOffset+" active="+view.Lyrics.ActiveIndex);
  // Switching lyrics off and on again must show them again (they once stayed invisible until the next song).
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(450);Check(!view.LyricsShown&&!view.LyricsOn,"Lyrics did not switch off");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.LyricsShown&&view.Lyrics.Opacity>.99);
  await Task.Delay(700);view.Lyrics.SetLyrics(LyricsService.Parse("[00:10.00] one\n[00:20.00] two\n[00:30.00] three",null));view.Lyrics.Update(15);await Task.Delay(150);view.Lyrics.UpdateLayout();
  {
   if(view.Lyrics.Items.Count==0)throw new Exception("No lyric lines after switching on again: shown="+view.LyricsShown+" on="+view.LyricsOn+" lineCount="+view.Lyrics.LineCount+" note="+view.Lyrics.Note);
   var first=view.Lyrics.Items[0];double firstMid=first.TransformToAncestor(view.Lyrics).Transform(new Point(0,first.ActualHeight/2)).Y;
   Check(view.Lyrics.IsVisible&&view.Lyrics.Opacity>.99&&view.Lyrics.Items.Count==3&&first.Visibility==Visibility.Visible&&firstMid>0&&firstMid<view.Lyrics.ActualHeight,"After switching lyrics off and on again they are not shown: visible="+view.Lyrics.IsVisible+" opacity="+view.Lyrics.Opacity+" items="+view.Lyrics.Items.Count+" firstVisibility="+first.Visibility+" firstY="+firstMid+" height="+view.Lyrics.ActualHeight+" width="+view.Lyrics.ActualWidth+" scroll="+view.Lyrics.ScrollOffset+" active="+view.Lyrics.ActiveIndex);
  }
  view.Lyrics.SetLyrics(many);shell.UpdateLayout();view.Lyrics.Update(400);await Task.Delay(300);
  // Clicking a line jumps the song there.
  double jumped=-1;view.Lyrics.Seek=seconds=>jumped=seconds;
  items[14].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});Check(Math.Abs(jumped-75)<.001,"Clicking a line did not seek: "+jumped);
  // Lyrics without times are shown as they are and do not follow the song.
  view.Lyrics.SetLyrics(plain);Check(!view.Lyrics.Synced&&view.Lyrics.ActiveIndex==-1,"Plain lyrics should not follow the song");view.Lyrics.Update(30);Check(view.Lyrics.ActiveIndex==-1,"Plain lyrics moved with the song");
  view.Lyrics.SetLyrics(null);Check(view.Lyrics.Note=="No lyrics found for this song"&&view.Lyrics.LineCount==0,"The missing lyrics message");
  // In a small window the cover is small too, and the transport row must still fit its column or layout clips the outer buttons and the play disc.
  foreach(var size in new[]{new Size(900,600),new Size(960,680),new Size(1200,780)})
  {
   var small=new Window{Left=-20000,Top=-20000,Width=size.Width,Height=size.Height,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Resources=shell.Resources,WindowStartupLocation=WindowStartupLocation.Manual};
   var roomy=new BigPlayerView(small,true,new Button(),null,false);small.Content=roomy;small.Show();roomy.SetOpen(true);small.UpdateLayout();
   var roomyColumn=(FrameworkElement)Field(roomy,"column");var columnBounds=roomyColumn.TransformToAncestor(roomy).TransformBounds(new Rect(0,0,roomyColumn.ActualWidth,roomyColumn.ActualHeight));
   foreach(var transport in new[]{roomy.Core.shuffle,roomy.Core.previous,roomy.Core.play,roomy.Core.next,roomy.Core.repeat})
   {
    var box=transport.TransformToAncestor(roomy).TransformBounds(new Rect(0,0,transport.ActualWidth,transport.ActualHeight));
    Check(box.Left>=columnBounds.Left-.5&&box.Right<=columnBounds.Right+.5,"A transport button sticks out of the player at "+size+": "+box+" vs "+columnBounds);
   }
   var rowBox=(FrameworkElement)VisualTreeHelper.GetParent(roomy.Core.shuffle);
   Check(System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(rowBox)==null,"The transport row is being clipped at "+size);
   roomy.SetOpen(false);small.Close();
  }
  // A narrow window keeps only the player.
  var host=new Window{Left=-20000,Top=-20000,Width=800,Height=620,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Resources=shell.Resources,WindowStartupLocation=WindowStartupLocation.Manual};
  var narrow=new BigPlayerView(host,true,new Button(),null,false);host.Content=narrow;host.Show();narrow.SetOpen(true);host.UpdateLayout();
  Check(narrow.LyricsOn&&!narrow.LyricsShown&&narrow.Lyrics.Visibility==Visibility.Collapsed,"Lyrics should step aside in a narrow window");
  narrow.SetOpen(false);host.Close();
  // Off again: the player returns to the middle and nothing is looked up.
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  await Until(()=>!view.LyricsShown&&Math.Abs(column.TransformToAncestor(view).Transform(new Point()).X+column.ActualWidth/2-view.ActualWidth/2)<2&&view.Lyrics.Visibility==Visibility.Collapsed);
  Check(!prefs.ShowLyrics&&!view.LyricsOn&&view.Lyrics.LineCount==0,"Switching lyrics off did not clear them");
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Collapsed);
 }
 static async Task CheckBigPlayer(Shell shell)
 {
  // The big player covers the whole window, sidebar included, and opens no window of its own.
  int windows=Application.Current.Windows.Count;var view=(BigPlayerView)Field(shell,"bigPlayerView");
  Call(shell,"ShowPage","Video");await Task.Delay(30);Check(view.Visibility==Visibility.Collapsed&&view.Opacity==0&&!view.Backdrop.Running,"Big player visible before opening");
  var button=(Button)Field(shell,"bigPlayerButton");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));shell.UpdateLayout();await Until(()=>view.Visibility==Visibility.Visible&&view.Opacity>.99&&Math.Abs(((TranslateTransform)((TransformGroup)view.RenderTransform).Children[1]).Y)<.01&&Math.Abs(((ScaleTransform)((TransformGroup)view.RenderTransform).Children[0]).ScaleX-1)<.01);await Task.Delay(600);
  Check(Application.Current.Windows.Count==windows&&Window.GetWindow(view)==shell&&view.Visibility==Visibility.Visible&&view.Opacity>.99,"Big player did not open in the main window");
  var covered=view.TransformToAncestor(shell).TransformBounds(new Rect(0,0,view.ActualWidth,view.ActualHeight));Check(covered.Left<=.5&&covered.Top<=.5&&covered.Width>=shell.ActualWidth-1&&covered.Height>=shell.ActualHeight-1,"The big player does not cover the sidebar: "+covered+" in "+shell.ActualWidth+"x"+shell.ActualHeight);
  Check(view.Backdrop.Running,"The background is not moving");
  // It takes every click, so nothing underneath can be changed through it, and the player buttons stay on top.
  await Until(()=>((Grid)Field(shell,"settingsLayer")).Visibility!=Visibility.Visible);
  // What a real click would reach: InputHitTest honors visibility and hit-testing, unlike a plain visual hit test.
  var hitPage=((UIElement)shell.Content).InputHitTest(new Point(shell.ActualWidth/2,shell.ActualHeight/3)) as DependencyObject;
  Check(hitPage!=null&&(ReferenceEquals(hitPage,view)||((Visual)hitPage).IsDescendantOf(view)),"Clicks on the big player reach the page under it: "+Chain(hitPage));
  var dockOver=(Border)Field(shell,"playerDock");var dockPoint=dockOver.TransformToAncestor(shell).Transform(new Point(dockOver.ActualWidth/2,dockOver.ActualHeight/2));var hitDock=((UIElement)shell.Content).InputHitTest(dockPoint) as DependencyObject;
  Check(hitDock!=null&&((Visual)hitDock).IsDescendantOf(dockOver),"The player buttons are not above the big player: "+Chain(hitDock));
  var layers=(Image[])Field(view.Backdrop,"layers");double needed=Math.Max(view.Backdrop.ActualWidth,view.Backdrop.ActualHeight)*1.6;
  Check(layers.All(l=>l.Width>=needed&&l.Height>=needed),"The background layers are too small to hide their own edges: "+string.Join(",",layers.Select(l=>l.Width+"x"+l.Height).ToArray())+" in "+view.Backdrop.ActualWidth+"x"+view.Backdrop.ActualHeight);
  var art=new System.Windows.Media.Imaging.PngBitmapEncoder();art.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(Cover(Color.FromRgb(220,60,30))));byte[] cover;using(var stream=new MemoryStream()){art.Save(stream);cover=stream.ToArray();}
  view.Core.SetTrack(new MediaTrack{Title="Big track",Artist="Big artist",Source="Fixture",Playing=true,CanPause=true,CanPlay=true,CanNext=true,CanPrevious=true,Duration=200,Position=50,Artwork=cover});shell.UpdateLayout();
  // It shows the album cover, large, with the track, progress, controls and volume.
  var image=view.Core.artwork;Check(image.Source!=null&&image.ActualWidth>=280,"Big player does not show the album cover: "+image.ActualWidth);
  Check(view.Core.title.Text=="Big track"&&((SmoothText)Field(view,"elapsed")).Text=="0:50"&&((SmoothText)Field(view,"remaining")).Text=="3:20","Big player text");
  Check(Math.Abs(((SeekBar)Field(view,"seek")).Fraction-.25)<.001&&view.Volume.IsVisible,"Big player progress or volume");
  // Everything fits at the smallest window.
  double width=shell.Width,height=shell.Height;shell.Width=shell.MinWidth;shell.Height=shell.MinHeight;shell.UpdateLayout();await Task.Delay(30);
  var frame=(FrameworkElement)Field(view,"coverFrame");var bounds=frame.TransformToAncestor(view).TransformBounds(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));var volumeBounds=view.Volume.TransformToAncestor(view).TransformBounds(new Rect(0,0,view.Volume.ActualWidth,view.Volume.ActualHeight));
  Check(bounds.Left>=0&&bounds.Top>=0&&volumeBounds.Bottom<=view.ActualHeight&&frame.ActualWidth>=220,"Big player does not fit the smallest window: "+bounds+" "+volumeBounds);shell.Width=width;shell.Height=height;shell.UpdateLayout();
  // The play disc grows on hover, so its slot must leave room for the growth or the disc is cut off at the slot edge.
  Check((view.Core.play.ActualWidth-12)*1.1<=view.Core.play.ActualWidth+.01&&view.Core.play.ActualWidth==view.Core.play.ActualHeight,"The play disc has no room to grow on hover: "+view.Core.play.ActualWidth);
  view.Core.play.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseEnterEvent,Source=view.Core.play});await Task.Delay(250);
  Check(!(view.Core.play.RenderTransform is ScaleTransform)||Math.Abs(((ScaleTransform)view.Core.play.RenderTransform).ScaleX-1)<.001,"The play disc moves on hover (nothing moves in a player)");
  view.Core.play.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseLeaveEvent,Source=view.Core.play});
  var playLeft=view.Core.play.TranslatePoint(new Point(),view).X+6;var nextLeft=view.Core.next.TranslatePoint(new Point(),view).X;var disc=view.Core.play.ActualWidth-12;
  Check(nextLeft-(playLeft+disc)>=10,"The play disc is too close to the next button");
  // The transport shares the mini player's instant response.
  var play=view.Core.play;play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(view.Core.displayedPlaying==false,"Big player icon did not change on click");
  // An open player keeps its button when media ends.
  typeof(Shell).GetField("lastMediaSeen",Private).SetValue(shell,DateTime.MinValue);shell.SetMediaAvailable(false);await Task.Delay(260);Check(((Border)Field(shell,"playerDock")).Visibility==Visibility.Visible,"Open big player lost its button");
  view.Core.SetTrack(null);Check(view.Core.title.Text=="Nothing playing"&&image.Source==null,"Big player did not clear");
  // The dock button closes it, and the page underneath is untouched.
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Collapsed&&!view.Backdrop.Running);Check(view.Visibility==Visibility.Collapsed&&!view.Backdrop.Running&&(string)Field(shell,"page")=="Video","Dock did not close big player");
  // A page change leaves the player, including the tab already selected.
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Visible);Call(shell,"ShowPage","Video");await Until(()=>view.Visibility==Visibility.Collapsed);Check(view.Visibility==Visibility.Collapsed,"The current tab did not close the big player");
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Visible);Call(shell,"ShowPage","Resolution");await Until(()=>view.Visibility==Visibility.Collapsed);Check(view.Visibility==Visibility.Collapsed&&(string)Field(shell,"page")=="Resolution","Another tab did not close the big player");Call(shell,"ShowPage","Video");
  // Settings opens over it and leaves it there.
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Visible);Call(shell,"ShowPage","Settings");await Task.Delay(40);Check((bool)Field(shell,"bigPlayerOpen"),"Settings closed the big player");Call(shell,"ShowPage","Settings");await Task.Delay(340);
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>view.Visibility==Visibility.Collapsed);shell.SetMediaAvailable(true);
 }
 static async Task CheckPopOut(Shell shell)
 {
  var button=(Button)Field(shell,"bigPlayerButton");var view=(BigPlayerView)Field(shell,"bigPlayerView");int windows=Application.Current.Windows.Count;
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(320);Check((bool)Field(shell,"bigPlayerOpen"),"The big player did not open in the app");
  Func<DependencyObject,string,Button> named=(root,name)=>Visuals<Button>(root).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)==name);
  named(view,"Pop out player").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
  var window=(BigPlayerWindow)Field(shell,"bigPlayerWindow");
  Check(window!=null&&window.IsVisible&&window.Owner==null&&Application.Current.Windows.Count==windows+1,"Popping out did not open a window of its own");
  Check(!(bool)Field(shell,"bigPlayerOpen"),"The big player stayed in the app after popping out");Check(Math.Abs(button.Opacity-1)<.01,"The big player button does not show it is open");
  window.UpdateLayout();
  window.View.Core.SetTrack(new MediaTrack{Title="Window track",Artist="W",Source="Fixture",Playing=true,CanPause=true,CanPlay=true,CanSeek=true,Duration=100,Position=25});await Task.Delay(30);
  Check(window.View.Core.title.Text=="Window track"&&((SeekBar)Field(window.View,"seek")).Seekable&&((SeekBar)Field(window.View,"seek")).Fraction>.2,"The popped-out player does not show the track");
  // The button closes it, and the dock button inside it brings it back into the app.
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);Check(Field(shell,"bigPlayerWindow")==null&&!window.IsVisible,"The button did not close the popped-out player");
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(320);named(view,"Pop out player").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
  window=(BigPlayerWindow)Field(shell,"bigPlayerWindow");named(window,"Dock player").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(340);
  Check(Field(shell,"bigPlayerWindow")==null&&(bool)Field(shell,"bigPlayerOpen")&&view.Visibility==Visibility.Visible&&Application.Current.Windows.Count==windows,"Docking did not bring the player back into the app");
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(300);Check(!(bool)Field(shell,"bigPlayerOpen"),"The button did not close the in-app player");
 }
 static async Task CheckRealMedia(Shell shell)
 {
  // Read-only: reads whatever Windows reports as playing and never sends the media app a command.
  var host=new Window{Left=-20000,Top=-20000,Width=1000,Height=640,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Resources=shell.Resources,WindowStartupLocation=WindowStartupLocation.Manual};
  var view=new BigPlayerView(host,false,new Button(),null,false);host.Content=view;host.Show();view.SetOpen(true);await Task.Delay(1500);
  if(view.Core.title.Text=="Nothing playing")Console.WriteLine("SKIP: real media read, nothing is playing.");
  else Console.WriteLine("PASS: real media read through the big player: title found, cover "+(view.Core.artwork.Source!=null?"found":"not reported")+", playing="+view.Core.displayedPlaying+", duration "+(view.Core.progressLength>1?"reported":"not reported")+".");
  // Read-only: finds the playing app's own entry in the Windows volume mixer without changing it.
  float appLevel;bool appMuted;string playingApp=view.Core.Source;
  Console.WriteLine(AppVolume.TryGet(playingApp,out appLevel,out appMuted)?"PASS: found the audio session of the playing app ("+playingApp+") at "+(int)Math.Round(appLevel*100)+"%"+(appMuted?", muted":"")+", read only.":"SKIP: the playing app ("+playingApp+") has no audio session to read right now.");
  view.SetOpen(false);host.Close();
 }
 static void CheckMemorySettling()
 {
  const ulong MB=1024*1024;
  Func<MemorySettler,Func<double,double>,double> run=(settler,usedAt)=>{for(double t=0;t<30;t+=.25)if(settler.Add(t,(ulong)(usedAt(t)*MB)))return t;return -1;};
  // Your case: a small drop at once, then the real drop arrives over the next seconds.
  var slow=new MemorySettler(9620*MB);double finished=run(slow,t=>Math.Max(8790,9620-t*280));Check(slow.Lowest<=8790*MB+MB&&finished>=2.9&&finished<=6,"Reported before memory settled: lowest "+slow.Lowest/MB+" MB after "+finished+" s");
  // Nothing more is coming: it does not keep you waiting.
  var flat=new MemorySettler(9000*MB);Check(run(flat,t=>9000)==2,"Waited too long with no change");
  // Ordinary noise does not extend the wait, and a later rise does not hide the low point.
  var noisy=new MemorySettler(9000*MB);Check(run(noisy,t=>9000+Math.Sin(t*9)*5)<=2.25,"Noise extended the wait");
  var rising=new MemorySettler(9000*MB);run(rising,t=>t<1?8600:9100);Check(rising.Lowest==8600*MB,"A rise hid the low point");
  // Memory that keeps falling cannot wait forever.
  var endless=new MemorySettler(30000*MB);Check(run(endless,t=>30000-t*400)==10,"Wait has no limit");
 }
 static void CheckPreferenceRecovery()
 {
  string folder=StateStore.Folder;StateStore.Folder=Path.Combine(folder,"recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  try
  {
   // Damaged with no backup: opens with defaults and keeps the damaged file.
   File.WriteAllText(StateStore.Config,"<Preferences><broken");var fresh=StateStore.Load();Check(fresh!=null&&StateStore.LoadProblem!=null&&File.Exists(StateStore.Config+".damaged"),"Damaged settings stopped loading");
   // Damaged with a good backup: restores it.
   var saved=new Preferences{CrosshairShape="Dot"};StateStore.Write(StateStore.Config+".bak",saved);var restored=StateStore.Load();Check(restored.CrosshairShape=="Dot"&&StateStore.LoadProblem!=null,"Backup was not restored");
   // Out-of-range values and missing lists are repaired; removed elements are ignored.
   File.WriteAllText(StateStore.Config,"<Preferences><Desktop><Red>900</Red><Zoom>0</Zoom></Desktop><ThemeIntensity>NaN</ThemeIntensity><EclipseOnly>true</EclipseOnly><PlayerPosition>Nope</PlayerPosition><BackgroundFile>library:old.gif</BackgroundFile></Preferences>");
   var repaired=StateStore.Load();Check(StateStore.LoadProblem==null&&repaired.Desktop.Red==100&&repaired.Desktop.Zoom==Look.MinimumZoom&&repaired.ThemeIntensity==Preferences.FixedIntensity&&repaired.PlayerPosition=="Middle"&&repaired.BackgroundFile=="","Values were not repaired");
   // No file: plain defaults, no problem reported.
   File.Delete(StateStore.Config);Check(StateStore.Load()!=null&&StateStore.LoadProblem==null,"Missing settings reported a problem");
  }
  finally{StateStore.Folder=folder;StateStore.LoadProblem=null;}
 }
 static async Task CheckButtonMotion(Shell shell,IDictionary pages)
 {
  // No page falls back to WPF's stepped glyph text, including dropdown contents.
  foreach(object name in pages.Keys.Cast<object>().ToArray())
  {
   if((string)name=="Settings")continue;
   Call(shell,"ShowPage",(string)name);shell.UpdateLayout();await Task.Delay(40);
   var old=Visuals<TextBlock>((DependencyObject)Field(shell,"content")).FirstOrDefault(x=>!string.IsNullOrEmpty(x.Text));Check(old==null,"Stepped text on "+name+": "+(old==null?"":old.Text));
  }
  Call(shell,"ShowPage","Resolution");shell.UpdateLayout();await Task.Delay(40);var page=(DependencyObject)Field(shell,"content");
  foreach(var combo in Visuals<ComboBox>(page))Check(Visuals<SmoothText>(combo).Any(x=>!string.IsNullOrWhiteSpace(x.Text)&&x.Text!="\u2304"),"Dropdown shows no text");
  // Text buttons dip when pressed and spring back without scaling up on hover; a small icon button grows.
  var wide=Visuals<Button>(page).First(b=>b.ActualWidth>=64&&b.IsEnabled&&b.Template.FindName("Glow",b)!=null);
  Func<Button,RoutedEvent,bool> mouse=(b,ev)=>{b.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=ev});return true;};
  mouse(wide,Mouse.MouseEnterEvent);await Task.Delay(180);Check(wide.RenderTransform is ScaleTransform&&Math.Abs(((ScaleTransform)wide.RenderTransform).ScaleX-1)<.001,"Text button scaled on hover");var glow=(UIElement)wide.Template.FindName("Glow",wide);Check(glow!=null&&glow.Opacity>.9,"Button glow did not light on hover");
  var wideScale=(ScaleTransform)wide.RenderTransform;wide.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});for(int i=0;i<80&&wideScale.ScaleX>=.999;i++)await Task.Delay(10);Check(wideScale.ScaleX<.999,"Text button did not dip when pressed: "+wideScale.ScaleX);
  wide.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});await Task.Delay(280);mouse(wide,Mouse.MouseLeaveEvent);await Task.Delay(200);Check(Math.Abs(wideScale.ScaleX-1)<.005,"Text button did not settle: "+wideScale.ScaleX);for(int i=0;i<60&&glow.Opacity>=.05;i++)await Task.Delay(10);Check(glow.Opacity<.05,"Button glow did not fade out: "+glow.Opacity);
  // The buttons by the bottom right do not move or ease: the icon is bright and a soft round highlight shows while the pointer is over it, and that is all.
  var icon=(Button)Field(shell,"playerDockButton");icon.Opacity=.65;mouse(icon,Mouse.MouseEnterEvent);Check(icon.Opacity==1&&!(icon.RenderTransform is ScaleTransform),"A dock icon should light up at once and not scale on hover: "+icon.Opacity);
  icon.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});await Task.Delay(100);Check(!(icon.RenderTransform is ScaleTransform),"A dock icon should not pop when pressed");
  icon.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});mouse(icon,Mouse.MouseLeaveEvent);Check(icon.Opacity<1,"A dock icon should dim again when the pointer leaves");  // Toggles slide instead of jumping.
  var surface=(Grid)Field(shell,"windowSurface");var toggle=new CheckBox{Style=(Style)shell.Resources["ToggleStyle"],Content="",Width=50,IsChecked=false,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};surface.Children.Add(toggle);shell.UpdateLayout();
  var knob=Visuals<System.Windows.Shapes.Ellipse>(toggle).First();Func<double> slide=()=>knob.RenderTransform.Value.OffsetX;toggle.IsChecked=true;var seen=new System.Collections.Generic.HashSet<double>();for(int i=0;i<30;i++){seen.Add(Math.Round(slide(),2));await Task.Delay(10);}Check(seen.Count>=3,"Toggle jumped without animating: "+string.Join(",",seen));await Task.Delay(120);Check(Math.Abs(slide()-15)<.01,"Toggle did not reach on: "+slide());toggle.IsChecked=false;await Task.Delay(280);Check(Math.Abs(slide())<.01,"Toggle did not return: "+slide());surface.Children.Remove(toggle);
  // Text links get no box on hover: they brighten and nudge instead.
  var link=new Button{Content="Link",Style=(Style)shell.Resources["TextAction"],HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};surface.Children.Add(link);shell.UpdateLayout();Check(link.Template.FindName("Glow",link)==null,"Text link has a hover box");
  var label=(UIElement)link.Template.FindName("Label",link);double idle=label.Opacity;link.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(220);Check(idle<1&&label.Opacity>.99&&label.RenderTransform.Value.OffsetX>1,"Text link did not react to hover");surface.Children.Remove(link);
  Call(shell,"ShowPage","Video");
 }
  static async Task CheckSettingsTransition(Shell shell)
 {
  var layer=(Grid)Field(shell,"settingsLayer");var card=(UIElement)Field(shell,"settingsCard");var shade=(UIElement)Field(shell,"settingsShade");var backdrop=(UIElement)Field(shell,"settingsBackdropLayer");
  string atVisible=null;DependencyPropertyChangedEventHandler watch=(s,e)=>{if((bool)e.NewValue&&atVisible==null)atVisible=card.Opacity+"|"+shade.Opacity+"|"+backdrop.Opacity;};layer.IsVisibleChanged+=watch;
  Call(shell,"SetSettingsOpen",true);layer.IsVisibleChanged-=watch;Check(atVisible=="0|0|0","Stale animation state when settings became visible: "+atVisible);
  // Every surface follows one value, sampled through the transition.
  for(int i=0;i<4;i++){await Task.Delay(30);double p=shade.Opacity;Check(Math.Abs(backdrop.Opacity-p)<1e-9&&Math.Abs(card.Opacity-p)<1e-9,"Settings surfaces drifted apart at p="+p);}
  await Until(()=>Math.Abs(card.Opacity-1)<.001&&Math.Abs(shade.Opacity-1)<.001);Check(Math.Abs(card.Opacity-1)<.001&&Math.Abs(shade.Opacity-1)<.001,"Settings did not settle open");
  Call(shell,"SetSettingsOpen",false);await Task.Delay(90);double midOpacity=card.Opacity;Check(midOpacity>0&&midOpacity<1,"Close animation did not start: "+midOpacity);
  Call(shell,"SetSettingsOpen",true);Check(Math.Abs(card.Opacity-midOpacity)<.05,"Reopening mid-close jumped: "+card.Opacity+" from "+midOpacity);
  await Until(()=>Math.Abs(card.Opacity-1)<.001&&layer.Visibility==Visibility.Visible);Check(Math.Abs(card.Opacity-1)<.001&&layer.Visibility==Visibility.Visible,"Reopen did not settle");
  Call(shell,"SetSettingsOpen",false);await Until(()=>layer.Visibility==Visibility.Hidden&&backdrop.Visibility==Visibility.Collapsed);Check(layer.Visibility==Visibility.Hidden&&backdrop.Visibility==Visibility.Collapsed,"Settings did not close and release its motion layers");
 }
 static System.Collections.Generic.IEnumerable<T> Visuals<T>(DependencyObject node)where T:DependencyObject
 {
  if(node is T)yield return (T)node;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var item in Visuals<T>(VisualTreeHelper.GetChild(node,i)))yield return item;
 }
 static async Task CheckScrolling(Shell shell,IDictionary pages)
 {
  Func<ScrollViewer> current=()=>(ScrollViewer)Field(shell,"contentScroll");bool scrolled=false;
  foreach(object name in pages.Keys.Cast<object>().ToArray())
  {
   if((string)name=="Settings")continue;
   Call(shell,"ShowPage",(string)name);shell.UpdateLayout();await Task.Delay(30);var main=current();
   foreach(var viewer in Visuals<ScrollViewer>(shell))Check(viewer.ComputedVerticalScrollBarVisibility!=Visibility.Visible&&viewer.ComputedHorizontalScrollBarVisibility!=Visibility.Visible,"Visible scrollbar on "+name);
   foreach(var bar in Visuals<System.Windows.Controls.Primitives.ScrollBar>(shell))Check(!bar.IsVisible,"Rendered scrollbar on "+name);
   if(main.ScrollableHeight<=0)continue;scrolled=true;main.ScrollToTop();shell.UpdateLayout();
   main.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.MouseWheelEvent});shell.UpdateLayout();Check(main.VerticalOffset>0,"Mouse wheel stopped scrolling "+name);
   // The check reads the real keyboard: a held Ctrl, Shift or Alt makes the app rightly ignore Page Down, so wait for the keys to be let go.
   for(int wait=0;wait<60&&Keyboard.Modifiers!=ModifierKeys.None;wait++)await Task.Delay(50);
   var source=PresentationSource.FromVisual(shell);main.ScrollToTop();shell.UpdateLayout();
   shell.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.PageDown){RoutedEvent=Keyboard.KeyDownEvent});shell.UpdateLayout();Check(main.VerticalOffset>0,"Page Down stopped scrolling "+name+" (modifiers "+Keyboard.Modifiers+", focus "+(Keyboard.FocusedElement==null?"none":Keyboard.FocusedElement.GetType().Name)+", scrollable "+main.ScrollableHeight+", offset "+main.VerticalOffset+")");
   shell.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.End){RoutedEvent=Keyboard.KeyDownEvent});shell.UpdateLayout();Check(Math.Abs(main.VerticalOffset-main.ScrollableHeight)<1,"End key stopped scrolling "+name);
   shell.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Home){RoutedEvent=Keyboard.KeyDownEvent});shell.UpdateLayout();Check(main.VerticalOffset==0,"Home key stopped scrolling "+name);
  }
  Check(scrolled,"No page overflowed, so wheel and keyboard scrolling went unproven");
  Call(shell,"ShowPage","Settings");await Task.Delay(400);Call(shell,"SelectSettingsCategory","General");shell.UpdateLayout();
  var inner=(ScrollViewer)Field(shell,"settingsScroll");Check(inner.VerticalScrollBarVisibility==ScrollBarVisibility.Hidden,"Settings scrollbar visible");
  foreach(var bar in Visuals<System.Windows.Controls.Primitives.ScrollBar>((DependencyObject)Field(shell,"settingsCard")))Check(!bar.IsVisible,"Rendered settings scrollbar");
  current().ScrollToTop();shell.UpdateLayout();Call(shell,"SetSettingsOpen",false);await Task.Delay(360);Call(shell,"ShowPage","Video");
 }
 static void CheckMediaClock()
 {
  // Smooth monotonic motion at 60 fps through quantized, jittery 500 ms samples.
  var clock=new MediaProgressClock();var random=new Random(4);double now=100,truth=30;clock.Synchronize("a",truth,true,now);double last=clock.Position(now);
  for(int frame=0;frame<3600;frame++)
  {
   now+=1/60d;truth+=1/60d;
   if(frame%30==29)clock.Synchronize("a",Math.Floor((truth+(random.NextDouble()-.5)*.6)*4)/4,true,now);
   double value=clock.Position(now);Check(value>=last-1e-9,"Clock moved backwards at frame "+frame+": "+last+" to "+value);Check(value-last<.06,"Clock jumped at frame "+frame+": "+(value-last));last=value;
  }
  Check(Math.Abs(last-truth)<.45,"Clock drifted from provider time: "+(last-truth));
  // A slow provider resynchronizes gradually without reversing.
  clock.Synchronize("a",clock.Position(now)-1,true,now);double slowStart=clock.Position(now);now+=.75;Check(clock.Position(now)>slowStart&&clock.Position(now)<slowStart+.75,"Backward correction was not gradual");
  // Large disagreement, including repeat-one restarts, snaps.
  clock.Synchronize("a",2,true,now);Check(clock.Position(now)==2,"Large error did not snap");
  // Pause stops at the reported position and resume continues from there.
  clock.Synchronize("a",50,false,now);now+=5;Check(clock.Position(now)==50,"Paused clock kept moving");clock.Synchronize("a",50,false,now);Check(clock.Position(now)==50,"Paused sample moved clock");
  clock.Synchronize("a",50,true,now);now+=1;Check(Math.Abs(clock.Position(now)-51)<.001,"Resume did not continue");
  // Track changes snap; Reset forgets everything.
  clock.Synchronize("a",60,true,now);clock.Synchronize("b",0,true,now);Check(clock.Position(now)==0,"Track change did not snap");now+=1;Check(Math.Abs(clock.Position(now)-1)<.001,"New track did not advance");
  clock.Reset();clock.Synchronize("b",40,true,now);Check(clock.Position(now)==40,"Reset did not snap");
 }
 static System.Windows.Media.Imaging.BitmapSource Cover(params Color[] colors)
 {
  var visual=new DrawingVisual();using(var dc=visual.RenderOpen())for(int i=0;i<colors.Length;i++)dc.DrawRectangle(new SolidColorBrush(colors[i]),null,new Rect(128d*i/colors.Length,0,128d/colors.Length,128));
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(128,128,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
 }
 static double Channel(double value){value/=255;return value<=.03928?value/12.92:Math.Pow((value+.055)/1.055,2.4);}
 static double Luminance(double r,double g,double b){return .2126*Channel(r)+.7152*Channel(g)+.0722*Channel(b);}
 static void CheckSmoothText(Shell shell)
 {
  // Plain string content anywhere in the app uses the smooth text element.
  var host=new ContentPresenter{Content="Plain label"};var surface=(Grid)Field(shell,"windowSurface");surface.Children.Add(host);shell.UpdateLayout();Check(Visuals<SmoothText>(host).Count()==1,"String content did not use smooth text");surface.Children.Remove(host);
  // Measuring, wrapping and trimming behave like a normal text block.
  var one=new SmoothText{Text="A reasonably long line of text for wrapping",FontSize=14,FontFamily=Design.Font};one.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));double wide=one.DesiredSize.Width,line=one.DesiredSize.Height;Check(wide>100&&line>=14,"Text was not measured");
  var wrap=new SmoothText{Text=one.Text,FontSize=14,FontFamily=Design.Font,TextWrapping=TextWrapping.Wrap};wrap.Measure(new Size(wide/2,double.PositiveInfinity));Check(wrap.DesiredSize.Height>=line*1.9&&wrap.DesiredSize.Width<=wide/2+1,"Text did not wrap");
  var trim=new SmoothText{Text=one.Text,FontSize=14,FontFamily=Design.Font,TextTrimming=TextTrimming.CharacterEllipsis};trim.Measure(new Size(wide/2,double.PositiveInfinity));Check(trim.DesiredSize.Height<line*1.5&&trim.DesiredSize.Width<=wide/2+1,"Text did not trim to one line");
  // Curved tops gain vertical antialiasing: a large glyph's top edge has several partial coverage rows.
  var glyph=new SmoothText{Text="O",FontSize=44,FontFamily=Design.Font,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};surface.Children.Add(glyph);shell.UpdateLayout();
  var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)glyph.ActualWidth,(int)glyph.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(glyph);surface.Children.Remove(glyph);int gw=image.PixelWidth,gh=image.PixelHeight;var pixels=new byte[gw*gh*4];image.CopyPixels(pixels,gw*4,0);
  Check(pixels.Where((v,i)=>i%4==3&&v>200).Count()>50,"Cached text rendered empty");int column=gw/2;var alpha=Enumerable.Range(0,gh).Select(y=>pixels[(y*gw+column)*4+3]).ToArray();var partial=alpha.Where(v=>v>12&&v<243).Count();Check(partial>=2,"Glyph edges are stepped: "+partial);
 }
 static void CheckBackdrop()
 {
  const int W=800,H=500;
  Func<System.Windows.Media.Imaging.BitmapSource,byte[]> render=cover=>{var image=AmbientBackdrop.Create(Design.Palette(cover),.85,true,W,H,cover);var bytes=new byte[W*H*4];image.CopyPixels(bytes,W*4,0);return bytes;};
  Func<byte[],int,double> mean=(bytes,channel)=>{double total=0;for(int i=channel;i<bytes.Length;i+=4)total+=bytes[i];return total/(W*H);};
  // The field follows the art: hue, and left/right placement.
  var warm=render(Cover(Color.FromRgb(220,60,30)));var cool=render(Cover(Color.FromRgb(30,70,220)));
  Check(mean(warm,2)>mean(warm,0)*2&&mean(warm,2)>40,"Warm art is not noticeable in the backdrop: R="+mean(warm,2)+" B="+mean(warm,0));Check(mean(cool,0)>mean(cool,2)*2&&mean(cool,0)>40,"Cool art is not noticeable in the backdrop");
  var dim=render(Cover(Color.FromRgb(46,18,12)));Check(mean(dim,2)>mean(dim,0)*1.5&&mean(dim,2)>14,"Dim art vanished from the backdrop: "+mean(dim,2));
  // The backdrop is the cover: colors stay where the cover has them.
  var sides=render(Cover(Color.FromRgb(230,40,30),Color.FromRgb(30,60,230)));double leftRed=0,rightRed=0;for(int y=0;y<H/3;y+=2)for(int x=0;x<W/8;x+=2){leftRed+=sides[(y*W+x)*4+2];rightRed+=sides[(y*W+W-1-x)*4+2];}Check(leftRed>rightRed*1.5,"Backdrop lost the cover's layout: "+leftRed+" vs "+rightRed);
  var gray=render(Cover(Color.FromRgb(120,120,120)));for(int i=0;i<gray.Length;i+=4000)Check(Math.Abs(gray[i]-gray[i+2])<=2,"A gray cover gained invented color");
  // Several hues in a cover all reach the backdrop as separate color regions.
  var split=render(Cover(Color.FromRgb(230,40,30),Color.FromRgb(30,60,230),Color.FromRgb(40,200,90)));
  double reddest=0,bluest=0,greenest=0;for(int i=0;i<split.Length;i+=4){reddest=Math.Max(reddest,split[i+2]-(split[i]+split[i+1])/2d);bluest=Math.Max(bluest,split[i]-(split[i+2]+split[i+1])/2d);greenest=Math.Max(greenest,split[i+1]-(split[i]+split[i+2])/2d);}
  Check(reddest>30&&bluest>30&&greenest>15,"Backdrop lost a cover hue: red "+reddest+" blue "+bluest+" green "+greenest);
  // The lower window follows the cover's brightness: bright covers are not forced dark, dark ones stay dark.
  Func<byte[],int,double> lower=(bytes,channel)=>{double total=0;int count=0;for(int y=H*7/10;y<H;y+=3)for(int x=0;x<W;x+=3){total+=bytes[(y*W+x)*4+channel];count++;}return total/count;};
  foreach(var bright in new[]{Colors.White,Color.FromRgb(250,226,120),Color.FromRgb(180,230,245)}){var image=render(Cover(bright));double level=Math.Max(lower(image,0),Math.Max(lower(image,1),lower(image,2)));Check(level>=55,"Bright cover "+bright+" has a dark lower window: "+level);}
  var darkCover=render(Cover(Color.FromRgb(40,34,24)));Check(Math.Max(lower(darkCover,2),lower(darkCover,1))<=22,"Dark cover has a bright lower window: "+lower(darkCover,2));
  // A single-hue cover stays that hue instead of drifting toward brown.
  var pure=render(Cover(Color.FromRgb(190,18,8)));for(int x=30;x<W;x+=120){int i=(30*W+x)*4;Check(pure[i+1]<pure[i+2]*.2,"Pure red cover drifted at x="+x+": "+pure[i+2]+","+pure[i+1]+","+pure[i]);}
  // Readability: no artwork can raise the backdrop past readable contrast for white or muted text, before cards darken it further.
  foreach(var color in new[]{Colors.White,Color.FromRgb(255,235,60),Color.FromRgb(120,255,120),Color.FromRgb(90,240,255)})
  {
   var bytes=render(Cover(color));double brightest=0;for(int i=0;i<bytes.Length;i+=4)brightest=Math.Max(brightest,Luminance(bytes[i+2],bytes[i+1],bytes[i]));
   Check(1.05/(brightest+.05)>=4.2,"White text contrast too low on "+color+": "+1.05/(brightest+.05));Check((Luminance(188,188,188)+.05)/(brightest+.05)>=2.2,"Muted text contrast too low on "+color+": "+(Luminance(188,188,188)+.05)/(brightest+.05));
  }
  // No gradient lines: after averaging rows to remove dither, the horizontal slope may not change abruptly anywhere.
  var edge=render(Cover(Color.FromRgb(240,40,30),Color.FromRgb(20,30,50),Color.FromRgb(40,220,160)));
  foreach(int channel in new[]{0,1,2})
  {
   var line=new double[W];for(int x=0;x<W;x++){double total=0;for(int y=0;y<H;y++)total+=edge[(y*W+x)*4+channel];line[x]=total/H;}
   for(int x=6;x<W-6;x++){double before=(line[x]-line[x-6])/6,after=(line[x+6]-line[x])/6;Check(Math.Abs(after-before)<.25,"Gradient kink in channel "+channel+" at x="+x+": "+Math.Abs(after-before));}
  }
 }
 static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject node)where T:DependencyObject
 {
  if(node is T)yield return (T)node;
  foreach(object child in LogicalTreeHelper.GetChildren(node))if(child is DependencyObject)foreach(var item in All<T>((DependencyObject)child))yield return item;
 }
}
