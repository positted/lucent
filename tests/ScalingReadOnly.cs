using System;
using System.Collections;
using System.Reflection;
class ScalingReadOnly
{
    static int Main(string[] args)
    {
        var assembly=Assembly.LoadFrom("Lucent.exe");
        var service=assembly.GetType("ResolutionSwitcher.DisplayService");
        var config=assembly.GetType("ResolutionSwitcher.NvidiaScalingConfig");
        foreach(object display in (IEnumerable)service.GetMethod("GetDisplays").Invoke(null,null))
        {
            string device=(string)display.GetType().GetProperty("DeviceName").GetValue(display,null);
            try
            {
                using(var reader=(IDisposable)Activator.CreateInstance(config,new object[]{device}))
                {
                    Console.WriteLine(device+" current scaling = "+config.GetProperty("CurrentScaling").GetValue(reader,null));
                    Console.WriteLine("  shared source: "+config.GetProperty("SharesSource").GetValue(reader,null));
                }
            }
            catch(Exception error){Console.WriteLine(device+" unavailable: "+error.GetBaseException().Message);return 1;}
        }
        Console.WriteLine("PASS: driver configuration read only; no setter or validation calls.");return 0;
    }
}
