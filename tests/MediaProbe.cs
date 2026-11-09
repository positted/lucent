using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Windows.Media.Control;
// Lists every media session Windows knows and what each says it can do. With "seek" it also asks the picked session
// to seek to where it already is, which changes nothing audible, to see whether the app accepts a seek.
class MediaProbe
{
 [STAThread]static int Main(string[] args)
 {
  var app=new Application();int result=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(args.Length>0&&args[0]=="seek");}catch(Exception error){Console.WriteLine(error);result=1;}finally{app.Shutdown();}}));app.Run();return result;
 }
 static async Task<T> Finish<T>(Windows.Foundation.IAsyncOperation<T> operation)
 {
  var info=(Windows.Foundation.IAsyncInfo)operation;var clock=System.Diagnostics.Stopwatch.StartNew();
  while(info.Status==Windows.Foundation.AsyncStatus.Started){if(clock.ElapsedMilliseconds>3000)throw new TimeoutException();await Task.Delay(25);}
  return operation.GetResults();
 }
 static async Task Run(bool seek)
 {
  var manager=await Finish(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
  var current=manager.GetCurrentSession();
  foreach(var session in manager.GetSessions())
  {
   var info=session.GetPlaybackInfo();var controls=info.Controls;var timeline=session.GetTimelineProperties();
   Console.WriteLine("{0}{1}: status={2} play={3} pause={4} prev={5} next={6} seek={7} shuffle={8} repeat={9} start={10:0.0} pos={11:0.0} end={12:0.0}",ReferenceEquals(session,current)||session.SourceAppUserModelId==(current==null?"":current.SourceAppUserModelId)?"* ":"  ",session.SourceAppUserModelId,info.PlaybackStatus,controls.IsPlayEnabled,controls.IsPauseEnabled,controls.IsPreviousEnabled,controls.IsNextEnabled,controls.IsPlaybackPositionEnabled,controls.IsShuffleEnabled,controls.IsRepeatEnabled,timeline.StartTime.TotalSeconds,timeline.Position.TotalSeconds,timeline.EndTime.TotalSeconds);
  }
  if(!seek)return;
  var assembly=Assembly.LoadFrom("Lucent.exe");var type=assembly.GetType("Lucent.MediaSession");var media=Activator.CreateInstance(type,true);
  var read=(Task)type.GetMethod("Read").Invoke(media,null);await read;var track=read.GetType().GetProperty("Result").GetValue(read,null);
  if(track==null){Console.WriteLine("Lucent picked no session.");return;}
  var t=track.GetType();double position=(double)t.GetField("Position").GetValue(track);
  Console.WriteLine("Lucent picked {0}, position {1:0.0}s, seek allowed={2}. Seeking to the same spot...",t.GetField("Source").GetValue(track),position,t.GetField("CanSeek").GetValue(track));
  Func<Task<object>> look=async()=>{var r=(Task)type.GetMethod("Read").Invoke(media,null);await r;return r.GetType().GetProperty("Result").GetValue(r,null);};
  Func<double,Task<object>> go=async seconds=>{var s=(Task)type.GetMethod("Seek").Invoke(media,new object[]{seconds});await s;return s.GetType().GetProperty("Result").GetValue(s,null);};
  double before=(double)t.GetField("Position").GetValue(await look());
  Console.WriteLine("position now {0:0.0}s; asking for {1:0.0}s (+6)",before,before+6);
  Console.WriteLine("The app answered: "+await go(before+6));
  foreach(int wait in new[]{300,700,1500})
  {
   await Task.Delay(wait);var later=(double)t.GetField("Position").GetValue(await look());
   Console.WriteLine("  after another {0} ms the app reports {1:0.0}s (expected about {2:0.0}s)",wait,later,before+6);
  }
  var restored=(double)t.GetField("Position").GetValue(await look());
  Console.WriteLine("putting it back near where it was: "+await go(Math.Max(0,restored-6)));
 }
}
