using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Lucent;

// Where the time goes when Lucent starts in a fresh process: building the window, showing it for the first time, and opening the big player for the first and second time.
class StartupBench
{
 [STAThread]static int Main(string[] args)
 {
  var total=Stopwatch.StartNew();
  if(args.Length>0&&args[0]=="jit"){System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","jit"));System.Runtime.ProfileOptimization.SetProfileRoot(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","jit"));System.Runtime.ProfileOptimization.StartProfile("bench.jitprofile");}
  warm=args.Length>1&&args[1]=="warm";
  var app=new System.Windows.Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(total);}catch(Exception error){Console.WriteLine(error);exit=1;}finally{app.Shutdown();}}));
  app.Run();return exit;
 }
 static bool warm;
 static object Call(object target,string name,params object[] a){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,a);}
 static async Task Idle(){await Dispatcher.CurrentDispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
 static async Task Run(Stopwatch total)
 {
  StateStore.Folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","startup-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(StateStore.Folder);
  var watch=Stopwatch.StartNew();
  var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=80,Top=60};
  long built=watch.ElapsedMilliseconds;
  foreach(string mark in Shell.StartMarks)Console.WriteLine("   "+mark);
  watch.Restart();
  bool rendered=false;shell.ContentRendered+=(s,e)=>rendered=true;
  shell.Show();long shown=watch.ElapsedMilliseconds;
  while(!rendered)await Task.Delay(1);
  long first=watch.ElapsedMilliseconds;
  await Idle();long idle=watch.ElapsedMilliseconds;
  Console.WriteLine("build window "+built+" ms | Show() "+shown+" ms | first frame at "+first+" ms | idle at "+idle+" ms | since process start "+total.ElapsedMilliseconds+" ms");
  shell.SetMediaAvailable(true);
  if(warm){var w=Stopwatch.StartNew();Call(shell,"WarmBigPlayer");Console.WriteLine("warming the big player took "+w.ElapsedMilliseconds+" ms");await Idle();}
  for(int i=1;i<=2;i++)
  {
   watch.Restart();Call(shell,"SetBigPlayerOpen",true);long call=watch.ElapsedMilliseconds;
   await Dispatcher.CurrentDispatcher.InvokeAsync(()=>{},DispatcherPriority.Loaded);long laid=watch.ElapsedMilliseconds;
   await Idle();
   Console.WriteLine("big player open #"+i+": call "+call+" ms, laid out and drawn "+laid+" ms, idle "+watch.ElapsedMilliseconds+" ms");
   await Task.Delay(700);Call(shell,"SetBigPlayerOpen",false);await Task.Delay(700);
  }
  // Smoothness of the motions: the gaps between screen frames while each runs (with the drifting colors on, as in use).
  shell.TestMode=false;
  foreach(var step in new[]{Tuple.Create("big player open",(Action)(()=>Call(shell,"SetBigPlayerOpen",true))),Tuple.Create("big player close",(Action)(()=>Call(shell,"SetBigPlayerOpen",false))),
   Tuple.Create("settings open",(Action)(()=>Call(shell,"SetSettingsOpen",true))),Tuple.Create("settings close",(Action)(()=>Call(shell,"SetSettingsOpen",false)))})
  {
   var gaps=new System.Collections.Generic.List<double>();var clock=Stopwatch.StartNew();double last=-1;
   EventHandler frame=(s,e)=>{double now=clock.Elapsed.TotalMilliseconds;if(last>=0)gaps.Add(now-last);last=now;};
   System.Windows.Media.CompositionTarget.Rendering+=frame;
   var call=Stopwatch.StartNew();step.Item2();long callMs=call.ElapsedMilliseconds;
   await Task.Delay(650);
   System.Windows.Media.CompositionTarget.Rendering-=frame;
   var sorted=gaps.OrderBy(x=>x).ToList();
   Console.WriteLine(string.Format("{0,-17} call {1,3} ms | {2,3} frames, avg gap {3,5:0.0} ms, p95 {4,5:0.0}, longest {5,5:0.0} ms",step.Item1,callMs,gaps.Count,gaps.Count==0?0:gaps.Average(),sorted.Count==0?0:sorted[(int)(sorted.Count*.95)],sorted.Count==0?0:sorted.Last()));
   await Task.Delay(400);
  }
  shell.TestMode=true;
  shell.Close();
 }
}
