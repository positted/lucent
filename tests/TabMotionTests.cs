using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Lucent;
// Measures how smoothly the page area changes between tabs: frames drawn and the longest stall.
class TabMotionTests
{
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object value,string name){return value.GetType().GetField(name,Private).GetValue(value);}
 static object Call(object value,string method,params object[] args){return value.GetType().GetMethod(method,Private).Invoke(value,args);}
 [STAThread]static int Main()
 {
  var app=new Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run();}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));app.Run();return exit;
 }
 static async Task Run()
 {
  StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","tab-motion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(StateStore.Folder);
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};shell.Show();
  try
  {
   var pages=((IDictionary)Field(shell,"pages")).Keys.Cast<string>().Where(n=>n!="Settings").ToArray();
   await Task.Delay(800);
   Console.WriteLine("{0,-14} {1,9} {2,9} {3,8} {4,9} {5,8}","page","block ms","photo ms","frames","max gap","stalls");
   var results=new List<double[]>();
   for(int pass=0;pass<2;pass++)
   foreach(string name in pages.Concat(new[]{pages[0]}))
   {
    await Task.Delay(450);
    var scroll=(System.Windows.Controls.ScrollViewer)Field(shell,"contentScroll");var photoClock=Stopwatch.StartNew();
    var picture=new System.Windows.Media.Imaging.RenderTargetBitmap((int)scroll.ActualWidth,(int)scroll.ActualHeight,96,96,PixelFormats.Pbgra32);picture.Render(scroll);double photo=photoClock.Elapsed.TotalMilliseconds;
    var clock=Stopwatch.StartNew();var times=new List<double>();EventHandler hook=(s,e)=>times.Add(clock.Elapsed.TotalMilliseconds);
    CompositionTarget.Rendering+=hook;
    Call(shell,"ShowPage",name);double block=clock.Elapsed.TotalMilliseconds;
    await Task.Delay(480);
    CompositionTarget.Rendering-=hook;
    var inWindow=times.Where(t=>t<=480).ToList();var gaps=new List<double>{inWindow.Count>0?inWindow[0]-block:0};
    for(int i=1;i<inWindow.Count;i++)gaps.Add(inWindow[i]-inWindow[i-1]);
    double max=gaps.Count>0?gaps.Max():0;int stalls=gaps.Count(g=>g>40);
    if(pass==1)Console.WriteLine("{0,-14} {1,9:0} {2,9:0} {3,8} {4,9:0} {5,8}",name,block,photo,inWindow.Count,max,stalls);
    if(pass==1)results.Add(new[]{block,inWindow.Count,max,stalls});
   }
   Console.WriteLine("SUMMARY worst stall {0:0} ms, worst block {1:0} ms, fewest frames {2}, total stalls {3}",results.Max(r=>r[2]),results.Max(r=>r[0]),results.Min(r=>r[1]),results.Sum(r=>r[3]));
  }
  finally{shell.Close();}
 }
}
