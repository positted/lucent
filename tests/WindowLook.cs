using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Lucent;

// Pictures of the real window as the screen shows it (its edges and corners, and how it fits the screen when maximized), taken from the screen itself.
class WindowLook
{
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 static void Grab(Rectangle area,string path){using(var b=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(area.Location,System.Drawing.Point.Empty,area.Size);b.Save(path,ImageFormat.Png);}Console.WriteLine("  "+Path.GetFileName(path)+" "+area);}
 [STAThread]static int Main(string[] args)
 {
  SetProcessDPIAware();
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(args[0]);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static async Task Run(string output)
 {
  Directory.CreateDirectory(output);
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","look-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  double scale;using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96.0;
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=160,Top=90,Width=1000,Height=700,Topmost=true};
  shell.Show();shell.Activate();await Task.Delay(1800);
  var o=shell.PointToScreen(new System.Windows.Point(0,0));
  Grab(new Rectangle((int)o.X-30,(int)o.Y-30,(int)(shell.ActualWidth*scale)+60,(int)(shell.ActualHeight*scale)+60),Path.Combine(output,"window.png"));
  Grab(new Rectangle((int)o.X-12,(int)o.Y-12,90,60),Path.Combine(output,"corner.png"));
  shell.WindowState=WindowState.Maximized;await Task.Delay(1500);
  var surface=(FrameworkElement)shell.GetType().GetField("windowSurface",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(shell);
  Console.WriteLine("maximized: window "+shell.ActualWidth+"x"+shell.ActualHeight+", contents margin "+surface.Margin+", screen "+System.Windows.Forms.Screen.PrimaryScreen.Bounds+" work "+System.Windows.Forms.Screen.PrimaryScreen.WorkingArea);
  var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
  Grab(screen,Path.Combine(output,"maximized.png"));
  shell.WindowState=WindowState.Normal;await Task.Delay(700);
  shell.Close();
 }
}
