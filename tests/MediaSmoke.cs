using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
class MediaSmoke
{
 [STAThread]static int Main()
 {
  var app=new Application();int result=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run();}catch(Exception error){Console.WriteLine(error.GetBaseException().Message);result=1;}finally{app.Shutdown();}}));app.Run();return result;
 }
 static async Task Run()
 {
  var assembly=Assembly.LoadFrom("Lucent.exe");
  var type=assembly.GetType("Lucent.MediaSession");var session=Activator.CreateInstance(type,true);
  var task=(Task)type.GetMethod("Read").Invoke(session,null);await task;
  bool present=task.GetType().GetProperty("Result").GetValue(task,null)!=null;
  Console.WriteLine("PASS: Windows media-session read completed. Session available: "+present+". No playback, favorite or seek commands sent.");
 }
}
