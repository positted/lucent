using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Lucent;

// Going to the tray and coming back, watched on the real screen: how soon the screen starts to change after the call, whether there are in-between
// frames (a motion) or a jump, and how long until it has arrived. Pictures of the steps go to the folder given as the first argument.
class TrayMotion
{
 [StructLayout(LayoutKind.Sequential)]struct Box{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Box box);
 [STAThread]static int Main(string[] args)
 {
  var app=new System.Windows.Application();int exit=0;
  string folder=args.Length>0?args[0]:null;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(folder);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static object Call(object target,string name,params object[] a){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,a);}
 static Bitmap Grab(Rectangle area){var b=new Bitmap(area.Width,area.Height);using(var g=Graphics.FromImage(b))g.CopyFromScreen(area.Location,System.Drawing.Point.Empty,area.Size);return b;}
 // How different two pictures are, on a grid of points.
 static double Differ(Bitmap a,Bitmap b)
 {
  double sum=0;int n=0;
  for(int y=4;y<a.Height;y+=Math.Max(1,a.Height/40))for(int x=4;x<a.Width;x+=Math.Max(1,a.Width/60)){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);sum+=Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B);n++;}
  return sum/Math.Max(1,n);
 }
 static async Task<List<Tuple<double,Bitmap,string>>> Watch(Shell shell,Rectangle area,Action action,int ms)
 {
  var list=new List<Tuple<double,Bitmap,string>>();
  var clock=Stopwatch.StartNew();action();
  while(clock.ElapsedMilliseconds<ms)
  {
   double at=clock.Elapsed.TotalMilliseconds;var shot=Grab(area);
   list.Add(Tuple.Create(at,shot,(shell.IsVisible?"shown":"hidden")+" "+shell.WindowState));
   await Task.Delay(1);
  }
  return list;
 }
 static void Report(string name,List<Tuple<double,Bitmap,string>> frames,Bitmap from,Bitmap to,string folder)
 {
  double whole=Math.Max(1,Differ(from,to));
  Console.WriteLine("--- "+name);
  double firstMove=-1,arrived=-1;int between=0;
  foreach(var f in frames)
  {
   double p=Differ(f.Item2,from)/whole;
   if(firstMove<0&&p>.03)firstMove=f.Item1;
   if(p>.08&&p<.92)between++;
   if(arrived<0&&p>.97)arrived=f.Item1;
   Console.WriteLine(string.Format("   {0,5:0} ms  {1,5:0}%  {2}",f.Item1,p*100,f.Item3));
  }
  Console.WriteLine(string.Format("   first change at {0:0} ms, arrived at {1:0} ms, {2} in-between frames ({3})",firstMove,arrived,between,between>=2?"a motion":"a jump"));
  if(folder!=null)
  {
   System.IO.Directory.CreateDirectory(folder);
   var pick=frames.Where((f,i)=>i%Math.Max(1,frames.Count/10)==0).Take(10).ToList();
   int w=frames[0].Item2.Width/4,h=frames[0].Item2.Height/4;
   using(var sheet=new Bitmap(w*5+16,(h+4)*2))using(var g=Graphics.FromImage(sheet))
   {
    g.Clear(System.Drawing.Color.Gray);
    for(int i=0;i<pick.Count;i++){int x=(i%5)*(w+4),y=(i/5)*(h+4);g.DrawImage(pick[i].Item2,x,y,w,h);g.DrawString(pick[i].Item1.ToString("0")+" ms",new Font("Segoe UI",9f),Brushes.Yellow,x+3,y+2);}
    sheet.Save(System.IO.Path.Combine(folder,name.Replace(' ','-')+".png"));
   }
  }
 }
 static async Task Run(string folder)
 {
  StateStore.Folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","tray-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(StateStore.Folder);
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=160,Top=90,Width=980,Height=620};
  shell.Show();shell.Activate();await Task.Delay(1500);
  shell.TestMode=false;
  Box box;GetWindowRect(new WindowInteropHelper(shell).Handle,out box);
  var area=new Rectangle(box.Left,box.Top,box.Right-box.Left,box.Bottom-box.Top);
  for(int round=1;round<=2;round++)
  {
   var shown=Grab(area);
   var away=await Watch(shell,area,()=>Call(shell,"HideToTray"),700);
   var gone=Grab(area);
   Console.WriteLine("after hiding: "+(shell.IsVisible?"shown":"hidden")+" "+shell.WindowState);
   Report("to tray "+round,away,shown,gone,folder);
   await Task.Delay(600);
   var back=await Watch(shell,area,()=>Call(shell,"BringToFront"),900);
   Console.WriteLine("after coming back: "+(shell.IsVisible?"shown":"hidden")+" "+shell.WindowState);
   Report("from tray "+round,back,gone,shown,folder);
   await Task.Delay(800);
  }
  shell.TestMode=true;shell.Close();
 }
}
