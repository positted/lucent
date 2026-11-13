using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Lucent;

// Takes the pictures for the project page: the real main window, the player bar, the big player, the mini player and the settings, on the main display in front of
// a plain dark window (so no part of the desktop is in a picture), with real album covers (files named "Artist__Song.jpg" in the folder given as the first argument).
// The pictures are written, with see-through rounded corners, to the folder given as the second argument. It changes no setting.
class ReadmeShots
{
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [STAThread]static int Main(string[] args)
 {
  if(args.Length<2){Console.WriteLine("usage: covers-folder output-folder");return 2;}
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(args[0],args[1]);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);}
 static FieldInfo F(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);}
 static double scale;
 static System.Drawing.Rectangle RectOf(Window window)
 {
  var origin=window.PointToScreen(new System.Windows.Point(0,0));
  return new System.Drawing.Rectangle((int)Math.Round(origin.X),(int)Math.Round(origin.Y),(int)Math.Round(window.ActualWidth*scale),(int)Math.Round(window.ActualHeight*scale));
 }
 // The window as the screen shows it, with the corners cut away smoothly (they would show the dark window behind).
 static void Save(System.Drawing.Rectangle area,string path,double radius)
 {
  using(var raw=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb))
  {
   using(var g=Graphics.FromImage(raw))g.CopyFromScreen(area.Location,System.Drawing.Point.Empty,area.Size);
   if(radius<=0){raw.Save(path,ImageFormat.Png);Console.WriteLine("  "+Path.GetFileName(path)+"  "+area.Width+"x"+area.Height);return;}
   using(var result=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb))
   using(var g=Graphics.FromImage(result))
   {
    g.SmoothingMode=SmoothingMode.AntiAlias;
    using(var shape=new GraphicsPath())
    {
     float r=(float)(radius*scale*2);
     shape.AddArc(0,0,r,r,180,90);shape.AddArc(area.Width-r,0,r,r,270,90);shape.AddArc(area.Width-r,area.Height-r,r,r,0,90);shape.AddArc(0,area.Height-r,r,r,90,90);shape.CloseFigure();
     using(var brush=new TextureBrush(raw))g.FillRectangle(brush,0,0,area.Width,area.Height);
    }
    result.Save(path,ImageFormat.Png);
   }
  }
  Console.WriteLine("  "+Path.GetFileName(path)+"  "+area.Width+"x"+area.Height);
 }
 // The window drawn straight from its own visuals, over black: nothing else on the screen can get into the picture (a window in front of it, or the desktop).
 static void Shot(Window window,string path)
 {
  if(Environment.GetEnvironmentVariable("LUCENT_CAPTURE")=="screen"){Save(RectOf(window),path,8);return;}
  int w=(int)Math.Round(window.ActualWidth*scale),h=(int)Math.Round(window.ActualHeight*scale);
  var shown=new System.Windows.Media.Imaging.RenderTargetBitmap(w,h,96*scale,96*scale,System.Windows.Media.PixelFormats.Pbgra32);
  shown.Render(window);
  var visual=new System.Windows.Media.DrawingVisual();
  using(var context=visual.RenderOpen())
  {
   context.DrawRectangle(System.Windows.Media.Brushes.Black,null,new System.Windows.Rect(0,0,w/scale,h/scale));
   context.DrawImage(shown,new System.Windows.Rect(0,0,w/scale,h/scale));
  }
  var final=new System.Windows.Media.Imaging.RenderTargetBitmap(w,h,96*scale,96*scale,System.Windows.Media.PixelFormats.Pbgra32);
  final.Render(visual);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(final));
  using(var stream=File.Create(path))encoder.Save(stream);
  Console.WriteLine("  "+Path.GetFileName(path)+"  "+w+"x"+h);
 } static async Task Run(string coversFolder,string output)
 {
  Directory.CreateDirectory(output);
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","readme-shots-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96.0;
  var screen=Screen.PrimaryScreen;
  // A plain dark window across the display, behind everything we show.
  var stage=new Window{WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Background=new System.Windows.Media.SolidColorBrush(Environment.GetEnvironmentVariable("LUCENT_STAGE")=="white"?System.Windows.Media.Color.FromRgb(240,240,240):System.Windows.Media.Color.FromRgb(0,0,0)),WindowStartupLocation=WindowStartupLocation.Manual,Left=screen.Bounds.Left/scale,Top=screen.Bounds.Top/scale,Width=screen.Bounds.Width/scale,Height=screen.Bounds.Height/scale};
  stage.Show();
  Lucent.Design.VersionOverride="1.1126";   // the pictures show the release name, not a build number (set before the window builds About)
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=screen.Bounds.Left/scale+90,Top=screen.Bounds.Top/scale+60,Width=1180,Height=780};
  shell.Show();shell.Activate();
  var prefs=(Preferences)F(shell,"preferences").GetValue(shell);
  var covers=Directory.GetFiles(coversFolder,"*.jpg").OrderBy(x=>x).ToList();
  Console.WriteLine("covers: "+string.Join(", ",covers.Select(Path.GetFileNameWithoutExtension)));
  Func<string,Tuple<MediaTrack,System.Windows.Media.Imaging.BitmapImage>> load=file=>
  {
   var parts=Path.GetFileNameWithoutExtension(file).Split(new[]{"__"},StringSplitOptions.None);
   var bytes=File.ReadAllBytes(file);
   var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=128;bitmap.StreamSource=new MemoryStream(bytes);bitmap.EndInit();bitmap.Freeze();
   var track=new MediaTrack{Title=parts[1],Artist=parts[0],Source="Spotify",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,CanShuffle=true,CanRepeat=true,Duration=191,Position=74,Artwork=bytes};
   return Tuple.Create(track,bitmap);
  };
  Action<string> play=file=>
  {
   var loaded=load(file);
   shell.SetMediaAvailable(true);
   F(shell,"ambientTrack").SetValue(shell,loaded.Item1);F(shell,"paletteArtwork").SetValue(shell,loaded.Item1.Artwork);F(shell,"paletteBitmap").SetValue(shell,loaded.Item2);
   F(shell,"mediaAccent").SetValue(shell,Lucent.Design.Palette(loaded.Item2));
   Call(shell,"UpdateAmbientColor");Call(shell,"UpdateHome");
   var bar=(PlayerBar)F(shell,"playerBar").GetValue(shell);bar.Core.Stop();bar.Core.SetTrack(loaded.Item1);
   bar.Core.SetTrack(loaded.Item1);
  };
  Call(shell,"SetPlayerBarOpen",true);
  Call(shell,"ShowPage","Home");
  await Task.Delay(800);
  if(Environment.GetEnvironmentVariable("LUCENT_ONLY")=="update")
  {
   // The update panel and a notice, as they look at the top right.
   play(covers[0]);Call(shell,"ShowPage","Home");await Task.Delay(1500);
   Lucent.Updates.Source="getlucent/lucent";
   Lucent.Updates.Fetcher=url=>"{\"tag_name\":\"v1.1127\",\"html_url\":\"https://github.com/getlucent/lucent/releases/tag/v1.1127\",\"body\":\"A new player, vibrance up to 300%, and Customize Home.\"}";
   await (Task)Call(shell,"CheckForUpdates",true);await Task.Delay(900);
   Shot(shell,Path.Combine(output,"update-panel.png"));
   Call(shell,"HideUpdatePopup");await Task.Delay(300);
   Call(shell,"ShowPage","Crosshair");await Task.Delay(500);Call(shell,"SetStatus","Crosshair off");await Task.Delay(800);
   Shot(shell,Path.Combine(output,"notice.png"));
   shell.Close();stage.Close();Console.WriteLine("PASS: update pictures written to "+output);return;
  }  // Made-up numbers for Your PC, so the rings show percentages and temperatures in the pictures (nothing is measured here).
  shell.ShowSystemStats(new SystemSnapshot{Cpu=37,CpuTemp=58,RamUsedGb=19.8,RamTotalGb=31.9,Gpu=64,GpuTemp=66,VramUsedGb=6.1,VramTotalGb=12,GpuName="Fixture"});
  // Each cover on Home: the colors follow the cover.
  bool quick=Environment.GetEnvironmentVariable("LUCENT_ONLY")=="quick"||Environment.GetEnvironmentVariable("LUCENT_ONLY")=="color";
  foreach(var file in (quick?covers.Take(0):covers))
  {
   play(file);await Task.Delay(2600);
   var name=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(file).Replace("__","-").ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
   Shot(shell,Path.Combine(output,"home-"+name+".png"));
  }
  if(Environment.GetEnvironmentVariable("LUCENT_ONLY")=="home"){shell.Close();stage.Close();Console.WriteLine("PASS: home pictures written to "+output);return;}
  // The other pages, with the first cover.
  play(covers[0]);await Task.Delay(1800);
  var pages=(IDictionary)F(shell,"pages").GetValue(shell);
  Console.WriteLine("pages: "+string.Join(", ",pages.Keys.Cast<object>().Select(k=>k.ToString())));
  foreach(string page in (quick?new string[0]:new[]{"Resolution","Video","Crosshair"}))
  {
   if(!pages.Contains(page))continue;
   Call(shell,"ShowPage",page);await Task.Delay(1400);
   Shot(shell,Path.Combine(output,"page-"+page.ToLowerInvariant()+".png"));
  }
  Call(shell,"ShowPage","Home");await Task.Delay(900);
  // Color with vibrance at its 300% maximum (the headline feature), and the Boost page.
  Call(shell,"ShowPage","Video");await Task.Delay(1200);
  ((System.Windows.Controls.Slider)((IDictionary)F(shell,"sliders").GetValue(shell))["Vibrance"]).Value=300;await Task.Delay(1200);
  Shot(shell,Path.Combine(output,"color-300.png"));
  if(Environment.GetEnvironmentVariable("LUCENT_ONLY")=="color"){shell.Close();stage.Close();Console.WriteLine("PASS: color picture written to "+output);return;}
  ((System.Windows.Controls.Slider)((IDictionary)F(shell,"sliders").GetValue(shell))["Vibrance"]).Value=100;
  Call(shell,"ShowPage","Game Boost");await Task.Delay(1400);Shot(shell,Path.Combine(output,"boost.png"));
  Call(shell,"ShowPage","Home");await Task.Delay(600);
  // The big player.
  play(covers[Math.Min(1,covers.Count-1)]);await Task.Delay(1200);
  prefs.ArtistPictures=true;   // the artist's picture beside the name (looked up on Deezer, as the app does when this is on)
  Call(shell,"SetBigPlayerOpen",true);await Task.Delay(1500);
  // The artist's picture, fetched from Deezer the way the app does when it is switched on (a test-mode window does not look it up by itself).
  {var bigNow=(BigPlayerView)F(shell,"bigPlayerView").GetValue(shell);ArtistPictures.Enabled=()=>true;bigNow.ArtistPicture.Show(load(covers[Math.Min(1,covers.Count-1)]).Item1.Artist,true);}
  await Task.Delay(4500);
  Shot(shell,Path.Combine(output,"big-player.png"));
  // The same player with lyrics: made-up demo lines in the lyrics cache (nothing is looked up online, and no real song's words are shown).
  var lyricsCache=(IDictionary)typeof(LyricsService).GetField("cache",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  var demoLines=new[]{"This is where the words of the song would be","Each line lights up as it is sung","The one that is playing stays bright","The others wait softly behind it","Scroll with the wheel or click a line to jump there","The player takes its colors from the cover","And the lyrics glide to keep up","Line by line, in time with the music","Nothing here is a real song","These are just lines to show how it looks","Open the lyrics with the speech bubble","Close them and the player is just the cover again"};
  var loadedBig=load(covers[Math.Min(1,covers.Count-1)]);
  lyricsCache[(loadedBig.Item1.Artist+"|"+loadedBig.Item1.Title).ToLowerInvariant()]=LyricsService.Parse(string.Join("\n",Enumerable.Range(0,30).Select(k=>"["+(k*5/60).ToString("00")+":"+(k*5%60).ToString("00")+".00] "+demoLines[k%demoLines.Length])),null);
  prefs.ShowLyrics=true;
  var bigView=(BigPlayerView)F(shell,"bigPlayerView").GetValue(shell);
  Call(bigView,"ToggleLyrics");await Task.Delay(900);
  // (A test-mode window does not look songs up, so the demo lines are put in the lyrics view directly.)
  bigView.Lyrics.SetLyrics((LyricsResult)lyricsCache[(loadedBig.Item1.Artist+"|"+loadedBig.Item1.Title).ToLowerInvariant()]);bigView.Lyrics.Update(74);
  await Task.Delay(2200);
  Shot(shell,Path.Combine(output,"big-player-lyrics.png"));
  Call(bigView,"ToggleLyrics");await Task.Delay(500);
  Call(shell,"SetBigPlayerOpen",false);await Task.Delay(900);
  // Game profiles, scrolled to the games (the pictures of the games found on this PC).
  Call(shell,"ShowPage","Profiles");await Task.Delay(1500);
  Shell.ShowGameIcons=true;var found=GameLibrary.Scan();
  // For the picture: VALORANT as found on this PC, then well-known Steam games (their pictures are in the folder named by LUCENT_GAMES) instead of the games on this PC.
  string gamesFolder=Environment.GetEnvironmentVariable("LUCENT_GAMES");
  if(!string.IsNullOrEmpty(gamesFolder)&&Directory.Exists(gamesFolder))
  {
   var shown=new List<LibraryGame>();
   var valorant=found.FirstOrDefault(g=>g.Name=="VALORANT");if(valorant!=null)shown.Add(valorant);
   foreach(string file in Directory.GetFiles(gamesFolder,"*.png").OrderBy(x=>x))
   {
    var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(file);bitmap.EndInit();bitmap.Freeze();
    string title=Path.GetFileNameWithoutExtension(file);title=title.Substring(title.IndexOf('-')+1);if(title=="PUBG BATTLEGROUNDS")title="PUBG: BATTLEGROUNDS";
    shown.Add(new LibraryGame{Name=title,Executable=@"C:\Games\"+title+".exe",Store="Steam",Picture=bitmap});
   }
   found=shown;
  }
  shell.SetLibrary(found);Console.WriteLine("games found: "+found.Count+" ("+string.Join(", ",found.Take(12).Select(g=>g.Name))+")");await Task.Delay(2500);
  var scroll=(System.Windows.Controls.ScrollViewer)F(shell,"contentScroll").GetValue(shell);scroll.ScrollToVerticalOffset(520);await Task.Delay(900);
  Shot(shell,Path.Combine(output,"game-profiles-games.png"));
  // Settings, with the album cover of a song in the background: the grid of categories, then About and General.
  {
   var backdrop=covers.FirstOrDefault(c=>Path.GetFileNameWithoutExtension(c).EndsWith("__Hater"))??covers[0];
   play(backdrop);Call(shell,"ShowPage","Home");await Task.Delay(1800);
   shell.Width=1500;shell.Height=940;shell.UpdateLayout();await Task.Delay(900);
   Call(shell,"SetSettingsOpen",true);await Task.Delay(1900);Shot(shell,Path.Combine(output,"settings-grid.png"));
   Call(shell,"SelectSettingsCategory","About");await Task.Delay(1200);Shot(shell,Path.Combine(output,"settings-about.png"));
   ((System.Windows.Controls.ScrollViewer)F(shell,"settingsScroll").GetValue(shell)).ScrollToEnd();await Task.Delay(700);Shot(shell,Path.Combine(output,"settings-about-links.png"));
   Call(shell,"SelectSettingsCategory","General");await Task.Delay(1000);Shot(shell,Path.Combine(output,"settings-general.png"));
   Call(shell,"SetSettingsOpen",false);await Task.Delay(900);
  }
  if(Environment.GetCommandLineArgs().Contains("band"))
  {
   // The strip of the window around the player bar over a page with text just above it: is anything blurred above the bar?
   play(covers[1]);await Task.Delay(1800);
   Call(shell,"ShowPage","Visual tools");await Task.Delay(1400);
   var scrollBand=(System.Windows.Controls.ScrollViewer)F(shell,"contentScroll").GetValue(shell);scrollBand.ScrollToVerticalOffset(300);await Task.Delay(1000);
   var barBand=(FrameworkElement)F(shell,"playerBar").GetValue(shell);var topLeftBand=barBand.PointToScreen(new System.Windows.Point(0,0));
   Save(new System.Drawing.Rectangle((int)topLeftBand.X-30,(int)topLeftBand.Y-90,(int)Math.Round(barBand.ActualWidth*scale)+60,(int)Math.Round(barBand.ActualHeight*scale)+110),Path.Combine(output,"band-check.png"),0);
  }
  if(Environment.GetCommandLineArgs().Contains("layout"))
  {
   // The new layout: Home being customized, the grid of settings, and one settings category.
   play(covers[0]);Call(shell,"ShowPage","Home");await Task.Delay(1600);
   Call(shell,"SetHomeEditing",true);await Task.Delay(1000);Shot(shell,Path.Combine(output,"check-home-edit.png"));Call(shell,"SetHomeEditing",false);await Task.Delay(600);
   Call(shell,"SetSettingsOpen",true);await Task.Delay(1800);Shot(shell,Path.Combine(output,"check-settings-grid.png"));
   Call(shell,"SelectSettingsCategory","Appearance");await Task.Delay(1000);Shot(shell,Path.Combine(output,"check-settings-appearance.png"));
   Call(shell,"SetSettingsOpen",false);await Task.Delay(800);
  }
  shell.Close();stage.Close();
  Console.WriteLine("PASS: pictures written to "+output);
 }
}
