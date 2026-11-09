using System;
using System.Collections;
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

// Opens and closes the lyrics of the real mini player (the real window, the real animation) at several window sizes, and measures how steadily the screen was
// redrawn while it happened: how many pictures, and the longest wait between two. A long wait is a stutter. The lyrics come from a made-up song put in the
// lyrics cache, so nothing is looked up online. Arguments: "primary" to use the main display (otherwise a second one), "shots <folder>" to also save pictures.
class MiniLyricsBench
{
 [STAThread]static int Main(string[] args)
 {
  var screen=args.Contains("primary")?Screen.PrimaryScreen:Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(screen==null){Console.WriteLine("SKIP: there is no second display.");return 0;}
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(screen,args);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);}
 static FieldInfo F(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);}
 static async Task Run(Screen screen,string[] args)
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","mini-lyrics-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  double scale;using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96.0;
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};shell.Show();
  // A made-up song with lyrics, in the cache the app looks in first.
  var lines=string.Join("\n",Enumerable.Range(0,60).Select(k=>"["+(k*4/60).ToString("00")+":"+(k*4%60).ToString("00")+".00] This is line number "+k+" of a made up song, long enough to wrap onto two lines sometimes"));
  var cache=(IDictionary)typeof(LyricsService).GetField("cache",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  bool real=args.Contains("real");
  if(!real)cache["yeat|nvr again"]=LyricsService.Parse(lines,null);
  byte[] cover;
  using(var picture=new Bitmap(256,256))
  {
   using(var g=Graphics.FromImage(picture)){g.Clear(System.Drawing.Color.FromArgb(30,60,170));using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(240,110,50)))g.FillEllipse(brush,20,20,150,150);}
   using(var stream=new MemoryStream()){picture.Save(stream,ImageFormat.Png);cover=stream.ToArray();}
  }
  if(!args.Contains("cold")){var warmWatch=Stopwatch.StartNew();Shell.WarmText();Console.WriteLine("(text warmed in "+warmWatch.ElapsedMilliseconds+" ms, as the app does while idle)");}
  string shots=args.Contains("shots")?args[Array.IndexOf(args,"shots")+1]:null;if(shots!=null)Directory.CreateDirectory(shots);
  if(args.Contains("closebench")){await CloseBench(shell,screen,scale,cover);shell.Close();return;}
  foreach(double width0 in args.Contains("only760")?new[]{760d,760d}:new[]{400d,560d,760d,1000d})
  {
   double width=width0;var prefs=new Preferences();prefs.ShowLyrics=false;
   var player=new MediaPlayerWindow(shell,prefs,false){StartAt=new System.Windows.Point(screen.Bounds.Left/scale+60,screen.Bounds.Top/scale+40)};
   player.Show();
   if(!real)
   {
    player.Core.Stop();
    player.SetTrack(new MediaTrack{Title="Nvr again",Artist="Yeat",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=162,Position=42,Artwork=cover});
   }
   else
   {
    // What the media controls of Windows really report (Spotify, playing): the player reads it by itself. If nothing plays, the play key is pressed once.
    bool pressed=false;
    for(int wait=0;wait<100;wait++)
    {
     var now=player.Core.Track;
     if(now!=null&&!string.IsNullOrEmpty(now.Title)&&now.Playing)break;
     if(!pressed&&wait==15){keybd_event(0xB3,0,0,UIntPtr.Zero);keybd_event(0xB3,0,2,UIntPtr.Zero);pressed=true;Console.WriteLine("(pressed the play key)");}
     await Task.Delay(100);
    }
    var track=player.Core.Track;Console.WriteLine("playing now: "+(track==null?"nothing":track.Source+" | "+track.Title+" | "+track.Artist+" | playing "+track.Playing));
   }
   await Task.Delay(900);
   if(args.Contains("nobackdrop"))player.Backdrop.Visibility=Visibility.Collapsed;
   player.Width=width;await Task.Delay(700);
   var layoutStart=player.ActualHeight;
   Console.WriteLine("--- window "+player.ActualWidth.ToString("0")+" x "+player.ActualHeight.ToString("0")+" (scale "+player.Scale.ToString("0.00")+")");
   if(real){var t=player.Core.Track;Console.WriteLine("   track: "+(t==null?"none":t.Title+" by "+t.Artist));}
   await Measure(player,"open lyrics ",shots==null?null:Path.Combine(shots,"mini-"+(int)width+"-open"),scale,()=>Call(player,"ToggleLyrics"));
   { var t=Stopwatch.StartNew(); player.Lyrics.SetLyrics(LyricsService.Parse(lines,null)); double a=t.Elapsed.TotalMilliseconds; player.UpdateLayout(); Console.WriteLine("   lyrics built again at this size: SetLyrics "+a.ToString("0")+" ms, plus layout "+(t.Elapsed.TotalMilliseconds-a).ToString("0")+" ms"); }
   await Task.Delay(500);
   await Measure(player,"close lyrics",shots==null?null:Path.Combine(shots,"mini-"+(int)width+"-close"),scale,()=>Call(player,"ToggleLyrics"));
   player.Close();await Task.Delay(300);
  }
  shell.Close();
  Console.WriteLine("PASS: measured");
 }
 // How the mini player goes away: what is on screen, millisecond by millisecond, from the click on close to the last picture.
 static async Task CloseBench(Shell shell,Screen screen,double scale,byte[] cover)
 {
  foreach(double width in new[]{400d,760d})
  {
   var prefs=new Preferences();prefs.ShowLyrics=false;
   var player=new MediaPlayerWindow(shell,prefs,false){StartAt=new System.Windows.Point(screen.Bounds.Left/scale+60,screen.Bounds.Top/scale+40)};
   player.Show();player.Core.Stop();
   player.SetTrack(new MediaTrack{Title="Nvr again",Artist="Yeat",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=162,Position=42,Artwork=cover});
   await Task.Delay(900);player.Width=width;await Task.Delay(900);
   // Like the real click: the pointer reaches the close button first (which prepares the picture), then the click.
   Console.WriteLine("--- closing a mini player "+player.ActualWidth.ToString("0")+" x "+player.ActualHeight.ToString("0"));
   var sw=Stopwatch.StartNew();var frames=new List<double>();double last=-1;
   EventHandler onFrame=(s,e)=>{double now=sw.Elapsed.TotalMilliseconds;if(last>=0)frames.Add(now-last);last=now;};
   CompositionTarget.Rendering+=onFrame;
   var seen=new List<string>();string previous="";
   var known=new HashSet<Window>(System.Windows.Application.Current.Windows.Cast<Window>());
   // Pictures of the screen while it closes (for looking at it): the player's place, with a little room around it.
   var shotList=new List<Bitmap>();var shotTimes=new List<double>();
   string shotFolder=Environment.GetEnvironmentVariable("LUCENT_SHOTS");
   var origin=player.PointToScreen(new System.Windows.Point(0,0));
   var area=new System.Drawing.Rectangle((int)origin.X-16,(int)origin.Y-16,(int)Math.Round(player.ActualWidth*scale)+32,(int)Math.Round(player.ActualHeight*scale)+32);
   player.Close();
   while(sw.Elapsed.TotalMilliseconds<700)
   {
    string state="real:"+(player.IsVisible?"shown":"gone");
    foreach(Window w in System.Windows.Application.Current.Windows)
    {
     if(known.Contains(w))continue;
     var image=w.Content as System.Windows.Controls.Image;var scaleT=image==null?null:image.RenderTransform as ScaleTransform;
     state+=" | ghost opacity "+w.Opacity.ToString("0.00")+" scale "+(scaleT==null?"-":scaleT.ScaleX.ToString("0.000"))+" visible "+w.IsVisible;
    }
    if(state!=previous){seen.Add(sw.Elapsed.TotalMilliseconds.ToString("0")+" ms: "+state);previous=state;}
    if(shotFolder!=null&&shotList.Count<10&&sw.Elapsed.TotalMilliseconds>=shotList.Count*24){var b=new Bitmap(area.Width,area.Height);using(var g=Graphics.FromImage(b))g.CopyFromScreen(area.Location,System.Drawing.Point.Empty,area.Size);shotList.Add(b);shotTimes.Add(sw.Elapsed.TotalMilliseconds);}
    await Task.Delay(2);
   }
   CompositionTarget.Rendering-=onFrame;
   var sorted=frames.OrderBy(x=>x).ToList();
   if(shotFolder!=null&&shotList.Count>0)
   {
    Directory.CreateDirectory(shotFolder);
    using(var sheet=new Bitmap(shotList[0].Width*5+8*4,(shotList[0].Height+8)*2))using(var g=Graphics.FromImage(sheet))
    {
     g.Clear(System.Drawing.Color.FromArgb(50,50,50));
     for(int i=0;i<shotList.Count;i++){int x=(i%5)*(shotList[0].Width+8),y=(i/5)*(shotList[0].Height+8);g.DrawImage(shotList[i],x,y);g.DrawString(shotTimes[i].ToString("0")+" ms",new System.Drawing.Font("Segoe UI",9f),System.Drawing.Brushes.Yellow,x+4,y+2);}
     sheet.Save(Path.Combine(shotFolder,"mini-close-"+(int)width+".png"));
    }
   }
   foreach(var line in seen.Take(40))Console.WriteLine("   "+line);
   Console.WriteLine("   screen frames: "+frames.Count+", longest gap "+(sorted.Count==0?0:sorted.Last()).ToString("0")+" ms, p95 "+(sorted.Count==0?0:sorted[(int)(sorted.Count*.95)]).ToString("0")+" ms");
   await Task.Delay(400);
  }
 }
 static async Task Measure(MediaPlayerWindow player,string name,string shotPrefix,double scale,Action action)
 {
  var frames=new List<double>();var heights=new List<double>();var sw=Stopwatch.StartNew();double last=-1;
  EventHandler onFrame=(s,e)=>{double now=sw.Elapsed.TotalMilliseconds;if(last>=0)frames.Add(now-last);last=now;heights.Add(player.ActualHeight);};
  CompositionTarget.Rendering+=onFrame;
  // Anything the window's thread does for a long time in one go, and what it is.
  var started=new System.Collections.Generic.Dictionary<System.Windows.Threading.DispatcherOperation,double>();
  System.Windows.Threading.DispatcherHookEventHandler begin=(s,e)=>{started[e.Operation]=sw.Elapsed.TotalMilliseconds;};
  System.Windows.Threading.DispatcherHookEventHandler end=(s,e)=>{double from;if(started.TryGetValue(e.Operation,out from)){double took=sw.Elapsed.TotalMilliseconds-from;started.Remove(e.Operation);if(took>40){var method=typeof(System.Windows.Threading.DispatcherOperation).GetField("_method",BindingFlags.Instance|BindingFlags.NonPublic);var del=method==null?null:method.GetValue(e.Operation) as Delegate;Console.WriteLine("      long operation: "+took.ToString("0")+" ms at +"+from.ToString("0")+", priority "+e.Operation.Priority+", "+(del==null?"?":del.Method.DeclaringType+"."+del.Method.Name));}}};
  player.Dispatcher.Hooks.OperationStarted+=begin;player.Dispatcher.Hooks.OperationCompleted+=end;
  var call=Stopwatch.StartNew();action();double callMs=call.Elapsed.TotalMilliseconds;
  { var lay=Stopwatch.StartNew();player.UpdateLayout();Console.WriteLine("      layout right after the call: "+lay.ElapsedMilliseconds+" ms"); }
  int shot=0;
  while(sw.Elapsed.TotalMilliseconds<(shotPrefix==null?1400:1400))
  {
   await Task.Delay(30);
   if(shotPrefix!=null&&shot<8&&sw.Elapsed.TotalMilliseconds>shot*110)
   {
    var origin=player.PointToScreen(new System.Windows.Point(0,0));
    var rect=new System.Drawing.Rectangle((int)origin.X,(int)origin.Y,(int)Math.Round(player.ActualWidth*scale),(int)Math.Round(player.ActualHeight*scale));
    using(var bitmap=new Bitmap(rect.Width,rect.Height))using(var g=Graphics.FromImage(bitmap)){g.CopyFromScreen(rect.Location,System.Drawing.Point.Empty,rect.Size);bitmap.Save(shotPrefix+"-"+shot+".png",ImageFormat.Png);}
    shot++;
   }
  }
  CompositionTarget.Rendering-=onFrame;player.Dispatcher.Hooks.OperationStarted-=begin;player.Dispatcher.Hooks.OperationCompleted-=end;
  var sorted=frames.OrderBy(x=>x).ToList();
  double distinct=heights.Distinct().Count();
  Console.WriteLine(string.Format("{0}: the call itself {1,5:0} ms | {2,3} screen frames in 1.4 s, avg gap {3,5:0.0} ms, p95 {4,5:0.0}, longest {5,5:0.0} ms | size changed in {6,3:0} different steps, height {7:0}",name,callMs,frames.Count,frames.Count==0?0:frames.Average(),sorted.Count==0?0:sorted[(int)(sorted.Count*.95)],sorted.Count==0?0:sorted.Last(),distinct,player.ActualHeight));
 }
}
