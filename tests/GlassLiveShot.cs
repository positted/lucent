using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Lucent;

// Shows the real main window (with the player bar up and a cover playing) on a display that is NOT the primary one and takes a screenshot of it from the screen,
// so the edges of the glass (the bar, the sections) can be looked at as they are really drawn, not as a software render. Writes the pictures to the folder given
// as the first argument. It never touches the primary display and changes no setting.
class GlassLiveShot
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
 static string folder;
 [STAThread]static int Main(string[] args)
 {
  folder=args.Length>0?args[0]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews");Directory.CreateDirectory(folder);
  var other=Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(other==null){Console.WriteLine("SKIP: there is no second display.");return 0;}
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(other);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);}
 static FieldInfo F(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);}
 static async Task Run(Screen other)
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","glass-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  double scale;using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96.0;
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=other.Bounds.Left/scale+10,Top=other.Bounds.Top/scale+10,Width=1060,Height=800};
  shell.Show();
  byte[] cover;
  using(var picture=new Bitmap(256,256))
  {
   using(var g=Graphics.FromImage(picture))
   {
    g.Clear(System.Drawing.Color.FromArgb(40,110,200));
    using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(210,120,70)))g.FillEllipse(brush,120,60,100,130);
    using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(235,240,250)))g.FillEllipse(brush,10,10,90,50);
   }
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
  var origin=shell.PointToScreen(new System.Windows.Point(0,0));
  var rect=new System.Drawing.Rectangle((int)Math.Round(origin.X),(int)Math.Round(origin.Y),(int)Math.Round(shell.ActualWidth*scale),(int)Math.Round(shell.ActualHeight*scale));
  using(var shot=Shot(rect))
  {
   shot.Save(Path.Combine(folder,"glass-window.png"),ImageFormat.Png);
   // The bar's left end and a section's corner, enlarged without smoothing, so single pixels can be seen.
   var bar=(FrameworkElement)F(shell,"playerBar").GetValue(shell);
   var barPoint=bar.TransformToAncestor(shell).Transform(new System.Windows.Point(0,0));
   Zoom(shot,new System.Drawing.Rectangle((int)(barPoint.X*scale),(int)(barPoint.Y*scale)-6,90,90),6,Path.Combine(folder,"glass-bar-left.png"));
   Zoom(shot,new System.Drawing.Rectangle((int)(barPoint.X*scale)+(int)(bar.ActualWidth*scale)-90,(int)(barPoint.Y*scale)-6,90,90),6,Path.Combine(folder,"glass-bar-right.png"));
   Zoom(shot,new System.Drawing.Rectangle((int)(barPoint.X*scale),(int)(barPoint.Y*scale)-120,90,90),6,Path.Combine(folder,"glass-section-corner.png"));
  }
  Console.WriteLine("window "+shell.ActualWidth+"x"+shell.ActualHeight+" scale "+scale+" at "+rect);
  shell.Close();
  Console.WriteLine("PASS: shots written to "+folder);
 }
 static void Zoom(Bitmap source,System.Drawing.Rectangle area,int factor,string path)
 {
  area.Intersect(new System.Drawing.Rectangle(0,0,source.Width,source.Height));
  using(var big=new Bitmap(area.Width*factor,area.Height*factor))
  using(var g=Graphics.FromImage(big))
  {
   g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
   g.DrawImage(source,new System.Drawing.Rectangle(0,0,big.Width,big.Height),area,GraphicsUnit.Pixel);
   big.Save(path,ImageFormat.Png);
  }
 }
}
