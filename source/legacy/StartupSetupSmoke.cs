using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class StartupSetupSmoke
{
    [STAThread] static void Main()
    {
        string link=Path.GetFullPath("../startup-test/Lucent.lnk");
        string target=Path.GetFullPath("Lucent.exe");
        SetupForm.ConfigureStartupAt(link,true,target);
        object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        object shortcut=shell.GetType().InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{link});
        string actual=(string)shortcut.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,shortcut,null);
        Marshal.ReleaseComObject(shortcut);Marshal.ReleaseComObject(shell);
        if(!string.Equals(actual,target,StringComparison.OrdinalIgnoreCase))throw new Exception("Startup target mismatch");
        SetupForm.ConfigureStartupAt(link,true,target);
        SetupForm.ConfigureStartupAt(link,false,target);
        if(File.Exists(link))throw new Exception("Startup removal failed");
        Console.WriteLine("Startup shortcut creation, target, update, and removal passed in workspace.");
    }
}
