using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Lucent;

// Shows the real mini player on a display that is NOT the primary one, at several sizes, and takes a screenshot of each from the screen,
// so what the player really looks like (not a software render) can be looked at. Writes release\previews\Mini-live-*.png.
// It never touches the primary display and changes no setting.
class MiniLiveShot
{
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int fromX,int fromY,uint operation);
 static Bitmap Shot(System.Drawing.Rectangle area)
 {
  var bitmap=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap))
  {
   IntPtr screen=GetDC(IntPtr.Zero),target=g.GetHdc();
   try{BitBlt(target,0,0,area.Width,area.Height,screen,area.Left,area.Top,0x00CC0020|0x40000000);}
   finally{g.ReleaseHdc(target);ReleaseDC(IntPtr.Zero,screen);}
  }
  return bitmap;
 }
 [STAThread]static int Main()
 {
  var other=Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(other==null){Console.WriteLine("SKIP: there is no second display.");return 0;}
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(other);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static async Task Run(Screen other)
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","mini-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};shell.Show();
  var prefs=new Preferences();
  byte[] cover;
  using(var picture=new Bitmap(256,256))
  {
   using(var g=Graphics.FromImage(picture))
   {
    g.Clear(System.Drawing.Color.FromArgb(30,60,170));
    using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(240,110,50)))g.FillEllipse(brush,20,20,150,150);
    using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(60,210,170)))g.FillEllipse(brush,120,120,110,110);
    using(var pen=new Pen(System.Drawing.Color.White,6))g.DrawLine(pen,10,246,246,10);
   }
   using(var stream=new MemoryStream()){picture.Save(stream,ImageFormat.Png);cover=stream.ToArray();}
  }
  var player=new MediaPlayerWindow(shell,prefs,true);
  player.WindowStartupLocation=WindowStartupLocation.Manual;
  double scale=VisualScale(other);
  player.Left=other.Bounds.Left/scale+40;player.Top=other.Bounds.Top/scale+60;
  player.Show();
  player.SetTrack(new MediaTrack{Title="Nvr again",Artist="Yeat",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=162,Position=42,Artwork=cover});
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews");Directory.CreateDirectory(folder);
  foreach(double width in new[]{400d,800d})
  {
   player.Width=width;
   for(int i=0;i<20;i++){await Task.Delay(40);}
   var rect=new System.Drawing.Rectangle((int)Math.Round(player.Left*scale),(int)Math.Round(player.Top*scale),(int)Math.Round(player.ActualWidth*scale),(int)Math.Round(player.ActualHeight*scale));
   using(var shot=Shot(rect))shot.Save(Path.Combine(folder,"Mini-live-"+(int)width+".png"),ImageFormat.Png);
   Console.WriteLine("window "+player.ActualWidth+"x"+player.ActualHeight+" at scale "+player.Scale+", display scale "+scale);
  }
  player.Close();
  // Hiding the controls while idle, with the real timer and the real player: the same window a moment apart, and the mouse not moving over it.
  prefs.MiniHideCompact=true;
  var live=new MediaPlayerWindow(shell,prefs,false){StartAt=new System.Windows.Point(other.Bounds.Left/scale+40,other.Bounds.Top/scale+60)};
  live.Show();
  live.SetTrack(new MediaTrack{Title="Nvr again",Artist="Yeat",Source="Fixture",Playing=true,CanPlay=true,CanPause=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=162,Position=42,Artwork=cover});
  await Task.Delay(1200);
  var before=new System.Drawing.Rectangle((int)Math.Round(live.Left*scale),(int)Math.Round(live.Top*scale),(int)Math.Round(live.ActualWidth*scale),(int)Math.Round(live.ActualHeight*scale));
  using(var shot=Shot(before))shot.Save(Path.Combine(folder,"Mini-idle-before.png"),ImageFormat.Png);
  double heightBefore=live.ActualHeight;
  await Task.Delay(5200);
  var after=new System.Drawing.Rectangle((int)Math.Round(live.Left*scale),(int)Math.Round(live.Top*scale),(int)Math.Round(live.ActualWidth*scale),(int)Math.Round(live.ActualHeight*scale));
  using(var shot=Shot(after))shot.Save(Path.Combine(folder,"Mini-idle-after.png"),ImageFormat.Png);
  Console.WriteLine("idle hiding: height "+heightBefore+" -> "+live.ActualHeight+", tuck "+live.TuckAmount+", controls faded "+live.ControlsFaded);
  live.Close();shell.Close();
  Console.WriteLine("PASS: shots written");
 }
 // The scale between WPF units and pixels on the window's display.
 static double VisualScale(Screen screen){using(var g=Graphics.FromHwnd(IntPtr.Zero))return g.DpiX/96.0;}
}
