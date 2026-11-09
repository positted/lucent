using System;
using System.Collections;
using System.Reflection;
using System.Diagnostics;
class BoostReadOnly
{
 static int Main()
 {
  var assembly=Assembly.LoadFrom("Lucent.exe");var type=assembly.GetType("ResolutionSwitcher.BackgroundAppBoost");var engine=Activator.CreateInstance(type,true);
  var clock=Stopwatch.StartNew();var data=(IEnumerable)type.GetMethod("DetectCandidates").Invoke(engine,null);int apps=0,protectedGroups=0,processes=0,small=0;
  foreach(object candidate in data){var t=candidate.GetType();bool locked=(bool)t.GetField("IsProtected").GetValue(candidate);if(locked)protectedGroups++;else apps++;processes+=(int)t.GetField("Instances").GetValue(candidate);if((long)t.GetField("MemoryBytes").GetValue(candidate)<1048576)small++;}
  Console.WriteLine("READ ONLY: "+processes+" processes · "+apps+" eligible app groups · "+protectedGroups+" protected groups · "+small+" groups below 1 MB · "+clock.ElapsedMilliseconds+" ms including sample interval.");
  Console.WriteLine("No close, kill, trim, restore, service-control or display-setting calls were invoked.");return 0;
 }
}
