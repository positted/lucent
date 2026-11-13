using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Lucent;

// A display being plugged in or removed rebuilds the Color and Display pages. This does that rebuild (twice, as Windows often reports a change more than once)
// and prints any error with where it came from.
class DisplayRebuild
{
 [STAThread]static int Main()
 {
  var app=new Application();int exit=0;
  app.Dispatcher.BeginInvoke(new Action(async()=>
  {
   try
   {
    StateStore.Folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","displays-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(StateStore.Folder);
    var shell=new Shell(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
    shell.Show();await Task.Delay(1200);
    var rebuild=typeof(Shell).GetMethod("RebuildDisplayPages",BindingFlags.Instance|BindingFlags.NonPublic);
    for(int i=0;i<3;i++)
    {
     if(i==1)typeof(Shell).GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(shell,new object[]{"Video"});
     if(i==2)typeof(Shell).GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(shell,new object[]{"Resolution"});
     rebuild.Invoke(shell,new object[]{typeof(Shell).Assembly.GetType("ResolutionSwitcher.DisplayService").GetMethod("GetDisplays",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,null)});
     shell.UpdateLayout();await Task.Delay(300);
     Console.WriteLine("rebuild "+(i+1)+" ok");
    }
    Console.WriteLine("PASS: the display pages were rebuilt three times without an error");
    shell.Close();
   }
   catch(Exception error){Console.WriteLine(error.InnerException??error);exit=1;}
   finally{app.Shutdown();}
  }));
  app.Run();return exit;
 }
}
