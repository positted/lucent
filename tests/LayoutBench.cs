using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Lucent;

// What laying out each page costs when its width changes (no drawing: measure and arrange only), and which parts of a page cost the most.
class LayoutBench
{
 [STAThread]static int Main(string[] args)
 {
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run();}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);}
 static double Time(FrameworkElement element,int rounds)
 {
  double w=element.ActualWidth,h=element.ActualHeight;var parent=VisualTreeHelper.GetParent(element) as UIElement;
  var watch=Stopwatch.StartNew();
  for(int i=0;i<rounds;i++)
  {
   double width=w-(i%2==0?12:0);
   element.Measure(new Size(width,double.PositiveInfinity));element.Arrange(new Rect(0,0,width,Math.Max(h,element.DesiredSize.Height)));
  }
  double each=watch.Elapsed.TotalMilliseconds/rounds;
  element.InvalidateMeasure();if(parent!=null)parent.InvalidateMeasure();
  return each;
 }
 static async Task Run()
 {
  StateStore.Folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","layout-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(StateStore.Folder);
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=80,Top=60,Width=1000,Height=720};
  shell.Show();await Task.Delay(1200);
  var pages=(IDictionary)Field(shell,"pages");
  foreach(DictionaryEntry entry in pages)
  {
   if((string)entry.Key=="Settings")continue;
   shell.GetType().GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{entry.Key});await Task.Delay(700);
   var page=(FrameworkElement)entry.Value;Time(page,6);
   Console.WriteLine(((string)entry.Key).PadRight(14)+" measure+arrange "+Time(page,40).ToString("0.00")+" ms");
   var panel=page as System.Windows.Controls.Panel;
   if(panel!=null&&Environment.GetCommandLineArgs().Contains("parts"))
    foreach(FrameworkElement child in panel.Children.OfType<FrameworkElement>().Where(c=>c.ActualWidth>0))
     Console.WriteLine("     "+child.GetType().Name.PadRight(18)+" "+Time(child,30).ToString("0.00")+" ms   ("+child.ActualWidth.ToString("0")+"x"+child.ActualHeight.ToString("0")+")");
   await Task.Delay(100);
  }
  shell.Close();
 }
}
