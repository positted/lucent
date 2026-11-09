using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using ResolutionSwitcher;
class BoostSafetyTests
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main()
 {
  foreach(string name in new[]{"vgc","vgk","vgtray","MsMpEng","lsass","System","VALORANT-Win64-Shipping","cs2"})Check(BackgroundAppBoost.IsProtectedProcessName(name),"Missing protection: "+name);
  foreach(string name in new[]{"RiotClientServices","RiotClientUx","RiotClientUxRender","VALORANT"})Check(!BackgroundAppBoost.IsProtectedProcessName(name),"Launcher still blanket-protected: "+name);
  var riot=BackgroundAppBoost.Catalog.First(d=>d.Id=="riot-interface");
  Check(BackgroundAppBoost.CanManage(riot,new ProcessIdentity{Name="RiotClientUxRender",Path=@"C:\Riot Games\Riot Client\UX\RiotClientUxRender.exe",Company="Riot Games",Product="Riot Client"}),"Verified Riot interface blocked");
  Check(!BackgroundAppBoost.CanManage(riot,new ProcessIdentity{Name="RiotClientUxRender",Path=@"C:\Riot Vanguard\RiotClientUxRender.exe",Company="Riot Games"}),"Vanguard path bypass");
  var epic=BackgroundAppBoost.Catalog.First(d=>d.Id=="epic-helper");
  Check(!BackgroundAppBoost.CanManage(epic,new ProcessIdentity{Name="EpicWebHelper",Path=@"C:\Riot Games\Riot Client\EpicWebHelper.exe",Company="Riot Games",Product="Riot Client"}),"Shared helper mistaken for Epic");
  Check(BoostPolicy.CoreReason(new ProcessIdentity{Name="x",Path=@"C:\Apps\x.exe",IsCritical=true})!=null,"Critical process allowed");
  Check(BoostPolicy.CoreReason(new ProcessIdentity{Name="x",Path=@"C:\Apps\x.exe",Company="SentinelOne"})!=null,"Security publisher allowed");
  Check(BoostPolicy.CoreReason(new ProcessIdentity{Name="x",Path=@"C:\Apps\x.exe",IsService=true})!=null,"Service allowed");
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","boost-safety-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string a=Path.Combine(folder,"a"),b=Path.Combine(folder,"b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
  var startA=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lucent.boostfixtureA.exe"),"\""+a+"\""){UseShellExecute=false,CreateNoWindow=true};
  var startB=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lucent.boostfixtureB.exe"),"\""+b+"\""){UseShellExecute=false,CreateNoWindow=true};
  using(var first=Process.Start(startA))using(var second=Process.Start(startB))
  {
   try
   {
    var clock=Stopwatch.StartNew();while((!File.Exists(Path.Combine(a,"ready.txt"))||!File.Exists(Path.Combine(b,"ready.txt")))&&clock.ElapsedMilliseconds<6000)Thread.Sleep(25);
    Check(!first.HasExited&&!second.HasExited,"Fixture launch");
    var engine=new AdvancedBoostEngine(new[]{new BoostAppDefinition("fixture-a","Fixture A","lucent.boostfixtureA","lucent.boostfixtureA"),new BoostAppDefinition("fixture-b","Fixture B","lucent.boostfixtureB","lucent.boostfixtureB")},()=>ProcessIdentity.Read(second));
    var detected=engine.Scan();Check(detected.Any(x=>x.Definition.Id=="fixture-a")&&detected.Any(x=>x.Definition.Id=="protected:fixture-b"&&x.IsProtected),"Snapshot detection");
    Check(!first.HasExited&&!second.HasExited,"Scanning closed a process");engine.CloseSelected(new[]{"fixture-b"},null);Check(!second.HasExited,"Foreground fixture closed");
    engine.CloseSelected(new[]{"fixture-a"},new[]{"fixture-a"});Check(!first.HasExited&&!second.HasExited,"Exclusion ignored");
    var stamp=detected.First(x=>x.Definition.Id=="fixture-a").Definition.Captured.First();long original=stamp.Started;stamp.Started++;
    engine.CloseSelected(new[]{"fixture-a"},null);Check(!first.HasExited&&!second.HasExited,"Changed start time accepted");stamp.Started=original;
    // A window that stays open may be asking to save: it must survive, with the result saying so.
    string w=Path.Combine(folder,"w");Directory.CreateDirectory(w);
    using(var window=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lucent.boostwindow.exe"),"\""+w+"\""){UseShellExecute=false}))
    {
     try
     {
      var wait=Stopwatch.StartNew();while(!File.Exists(Path.Combine(w,"ready.txt"))&&wait.ElapsedMilliseconds<6000)Thread.Sleep(25);Thread.Sleep(150);
      var windowed=new AdvancedBoostEngine(new[]{new BoostAppDefinition("fixture-window","Window","lucent.boostwindow","lucent.boostwindow")},()=>ProcessIdentity.Read(second));windowed.Scan();
      var refused=windowed.CloseSelected(new[]{"fixture-window"},null);Console.WriteLine(refused.Message);Check(!window.HasExited&&refused.StoppedProcesses==0&&refused.Message.Contains("asking to save"),"Boost force-closed a window that stayed open");
     }
     finally{File.WriteAllText(Path.Combine(w,"stop.txt"),"stop");if(!window.WaitForExit(5000))window.Kill();}
    }
    var result=engine.CloseSelected(new[]{"fixture-a"},null);Console.WriteLine(result.Message+" A="+first.HasExited+" B="+second.HasExited);Check(first.WaitForExit(2000)&&!second.HasExited&&result.StoppedProcesses==1,"Selection did not isolate fixture A");
    Console.WriteLine("PASS: Vanguard/game/security/service protections, eligible Riot UI, Epic identity verification, all-process read-only detection, exclusions, stale-process rejection, leaving open windows alone and closure of only the selected disposable fixture.");
   }
   finally{File.WriteAllText(Path.Combine(a,"stop.txt"),"stop");File.WriteAllText(Path.Combine(b,"stop.txt"),"stop");if(!first.HasExited)first.WaitForExit(5000);if(!second.HasExited)second.WaitForExit(5000);}
  }
 }
}
