using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Lucent;

// Resizes the real main window on a display that is NOT the primary one, the way dragging an edge does (a new size every few milliseconds), and measures what each
// step costs: the time to lay out, and how often the screen really got a new picture. It does this with parts of the window switched off one at a time, to show
// which part is slow. It never touches the primary display and changes no setting.
class ResizeLiveBench
{
 [STAThread]static int Main(string[] args)
 {
  var other=args.Contains("primary")?Screen.PrimaryScreen:Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(other==null){Console.WriteLine("SKIP: there is no second display.");return 0;}
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(other,args);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);}
 static FieldInfo F(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);}
 static async Task Run(Screen other,string[] args)
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","resize-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  double scale;using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96.0;
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=other.Bounds.Left/scale+10,Top=other.Bounds.Top/scale+10,Width=1000,Height=720};
  shell.Show();
  byte[] cover;
  using(var picture=new Bitmap(256,256))
  {
   using(var g=Graphics.FromImage(picture)){g.Clear(System.Drawing.Color.FromArgb(40,110,200));using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(210,120,70)))g.FillEllipse(brush,120,60,100,130);}
   using(var stream=new MemoryStream()){picture.Save(stream,ImageFormat.Png);cover=stream.ToArray();}
  }
  var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=new MemoryStream(cover);bitmap.EndInit();bitmap.Freeze();
  var track=new MediaTrack{Title="Hold On, We're Going Home",Artist="Drake",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=240,Position=60,Artwork=cover};
  shell.SetMediaAvailable(true);Call(shell,"SetPlayerBarOpen",true);
  F(shell,"ambientTrack").SetValue(shell,track);F(shell,"paletteArtwork").SetValue(shell,cover);F(shell,"paletteBitmap").SetValue(shell,bitmap);
  F(shell,"mediaAccent").SetValue(shell,Lucent.Design.Palette(bitmap));
  Call(shell,"UpdateAmbientColor");
  ((PlayerBar)F(shell,"playerBar").GetValue(shell)).Core.SetTrack(track);
  await Task.Delay(2500);
  var prefs=(Preferences)F(shell,"preferences").GetValue(shell);
  var flow=(FlowingBackdrop)F(shell,"flowBackdrop").GetValue(shell);
  var bar=(UIElement)F(shell,"playerBar").GetValue(shell);
  var ambient=(UIElement)F(shell,"ambientBackdrop").GetValue(shell);
  var flowHost=(UIElement)F(shell,"flowHost").GetValue(shell);
  Console.WriteLine("display scale "+scale+"; window "+shell.ActualWidth+"x"+shell.ActualHeight);
  if(args.Contains("warm")){var warm=Stopwatch.StartNew();Call(shell,"WarmResize");Console.WriteLine("warm resize took "+warm.ElapsedMilliseconds+" ms");}
  if(args.Contains("nopage"))((UIElement)F(shell,"pageHost").GetValue(shell)).Visibility=Visibility.Collapsed;
  if(args.Contains("nobar"))bar.Visibility=Visibility.Collapsed;
  if(args.Contains("noflow"))flowHost.Visibility=Visibility.Collapsed;
  if(args.Contains("noambient"))ambient.Visibility=Visibility.Collapsed;
  if(args.Contains("nosettings"))((UIElement)F(shell,"settingsLayer").GetValue(shell)).Visibility=Visibility.Collapsed;
  if(args.Contains("noworkspace"))((UIElement)F(shell,"workspace").GetValue(shell)).Visibility=Visibility.Collapsed;
  await Sweep(shell,"(the first drag)");
  if(args.Contains("onlyfirst")){shell.Close();return;}
  await Sweep(shell,"everything on");
  {
   var playerBar=(PlayerBar)F(shell,"playerBar").GetValue(shell);
   playerBar.SetGlass(false);await Task.Delay(300);await Sweep(shell,"bar without glass (blur, sheen, edge)");playerBar.SetGlass(true);await Task.Delay(300);
   var blurRect=(UIElement)F(playerBar,"blur").GetValue(playerBar);blurRect.Visibility=Visibility.Collapsed;await Sweep(shell,"bar with only the blur collapsed");blurRect.Visibility=Visibility.Visible;
   var rimLayer=(UIElement)F(playerBar,"glassRim").GetValue(playerBar);rimLayer.Visibility=Visibility.Collapsed;await Sweep(shell,"bar with only the sheen+edge collapsed");rimLayer.Visibility=Visibility.Visible;
   playerBar.Backdrop.Visibility=Visibility.Collapsed;await Sweep(shell,"bar with only the colored band collapsed");playerBar.Backdrop.Visibility=Visibility.Visible;
  }
  bar.Visibility=Visibility.Collapsed;await Sweep(shell,"player bar hidden");bar.Visibility=Visibility.Visible;
  flowHost.Visibility=Visibility.Collapsed;await Sweep(shell,"moving colors hidden");
  ambient.Visibility=Visibility.Collapsed;await Sweep(shell,"moving colors and window colors hidden");
  bar.Visibility=Visibility.Collapsed;await Sweep(shell,"bar + moving colors + window colors hidden");
  ambient.Visibility=Visibility.Visible;flowHost.Visibility=Visibility.Visible;bar.Visibility=Visibility.Visible;
  // Which part of the window costs what: the page and the sidebar and buttons, switched off one at a time (with the colors and the bar off, so only they remain).
  ambient.Visibility=Visibility.Collapsed;flowHost.Visibility=Visibility.Collapsed;bar.Visibility=Visibility.Collapsed;
  var pageHost=(UIElement)F(shell,"pageHost").GetValue(shell);
  pageHost.Visibility=Visibility.Collapsed;await Sweep(shell,"colors+bar off, and the page off");pageHost.Visibility=Visibility.Visible;
  var workspace=(UIElement)F(shell,"workspace").GetValue(shell);
  workspace.Visibility=Visibility.Collapsed;await Sweep(shell,"colors+bar off, and the whole workspace off");workspace.Visibility=Visibility.Visible;
  // The pieces that are not on show (settings, the big player, the notice, the history list), one by one.
  foreach(string name in new[]{"settingsLayer","bigPlayerView","historyPanel","statusCard","playerDock","previewPopup","gifShade"})
  {
   var field=F(shell,name);if(field==null){Console.WriteLine("(no field "+name+")");continue;}
   var element=field.GetValue(shell) as UIElement;if(element==null)continue;var kept=element.Visibility;
   element.Visibility=Visibility.Collapsed;await Sweep(shell,"colors+bar off, and "+name+" collapsed ("+kept+")");element.Visibility=kept;
  }
  ambient.Visibility=Visibility.Visible;flowHost.Visibility=Visibility.Visible;bar.Visibility=Visibility.Visible;
  prefs.PerformanceMode=true;Call(shell,"ApplyGlass");Call(shell,"UpdateFlowBackdrop");await Task.Delay(500);
  await Sweep(shell,"performance mode");
  shell.Close();
  Console.WriteLine("PASS: measured");
 }
 // About 150 steps, a new width about every 8 ms, back and forth between 1000 and 1500 wide.
 static async Task Sweep(Shell shell,string name)
 {
  var layout=new List<double>();var frames=new List<double>();var sw=Stopwatch.StartNew();double lastFrame=-1;
  EventHandler onFrame=(s,e)=>{double now=sw.Elapsed.TotalMilliseconds;if(lastFrame>=0)frames.Add(now-lastFrame);lastFrame=now;};
  CompositionTarget.Rendering+=onFrame;
  var total=Stopwatch.StartNew();
  for(int i=0;i<150;i++)
  {
   double width=1250+250*Math.Sin(i/12.0*Math.PI/2);
   var step=Stopwatch.StartNew();
   shell.Width=width;double setMs=step.Elapsed.TotalMilliseconds;shell.UpdateLayout();
   layout.Add(step.Elapsed.TotalMilliseconds);
   if(i<4&&name.StartsWith("(the first"))Console.WriteLine("   step "+i+": set width "+setMs.ToString("0.0")+" ms, layout "+(step.Elapsed.TotalMilliseconds-setMs).ToString("0.0")+" ms");
   // Like the system's resize loop: the next size comes when the window has had its turn to draw.
   await shell.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
   var wait=8-step.Elapsed.TotalMilliseconds;if(wait>1)await Task.Delay((int)wait);
  }
  double seconds=total.Elapsed.TotalSeconds;
  CompositionTarget.Rendering-=onFrame;
  layout.Sort();frames.Sort();
  Func<List<double>,double,double> pct=(list,p)=>list.Count==0?0:list[Math.Min(list.Count-1,(int)(list.Count*p))];
  Console.WriteLine(string.Format("{0,-44} layout avg {1,5:0.0} ms  p95 {2,5:0.0}  max {3,5:0.0} | steps/s {4,5:0}  screen frames/s {5,5:0}",name,layout.Average(),pct(layout,.95),layout.Last(),150/seconds,frames.Count/seconds));
  shell.Width=1000;shell.UpdateLayout();await Task.Delay(400);
 }
}
