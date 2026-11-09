using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

internal static class SetupScope
{
    internal static string AppFolder(bool all) { return Path.Combine(Environment.GetFolderPath(all ? Environment.SpecialFolder.ProgramFiles : Environment.SpecialFolder.LocalApplicationData), "Lucent"); }
    internal static string DesktopLink(bool all) { return Path.Combine(Environment.GetFolderPath(all ? Environment.SpecialFolder.CommonDesktopDirectory : Environment.SpecialFolder.DesktopDirectory), "Lucent.lnk"); }
    internal static string MenuFolder(bool all) { return Path.Combine(Environment.GetFolderPath(all ? Environment.SpecialFolder.CommonPrograms : Environment.SpecialFolder.Programs), "Lucent"); }
    internal static string StartupLink(bool all) { return Path.Combine(Environment.GetFolderPath(all ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup), "Lucent.lnk"); }
    internal static int Worker(string[] args)
    {
        try
        {
            using(WindowsIdentity identity=WindowsIdentity.GetCurrent())
                if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 5;
            if(args[0]=="--machine-install")
            {
                int flags;
                if(args.Length!=2 || !int.TryParse(args[1],out flags) || flags<0 || flags>7) return 2;
                Install(true,flags);
            }
            else if(args[0]=="--machine-uninstall") Uninstall(true);
            else return 2;
            return 0;
        }
        catch { return 1; }
    }
    internal static void Install(bool all,int flags)
    {
        string folder=AppFolder(all), app=Path.Combine(folder,"Lucent.exe");
        Directory.CreateDirectory(folder);
        using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream("Lucent.exe"))
        {
            if(source==null)throw new InvalidOperationException("The app files were not found in this installer.");
            using(FileStream target=File.Create(app))source.CopyTo(target);
        }
        if((flags&1)!=0) Shortcut(DesktopLink(all),app);
        if((flags&2)!=0) {Directory.CreateDirectory(MenuFolder(all));Shortcut(Path.Combine(MenuFolder(all),"Lucent.lnk"),app);}
        if((flags&4)!=0) Shortcut(StartupLink(all),app); else Delete(StartupLink(all));
    }
    internal static void Uninstall(bool all)
    {
        // These fixed, scope-specific paths never enumerate other users' profiles.
        Delete(Path.Combine(AppFolder(all),"Lucent.exe"));
        Delete(DesktopLink(all)); Delete(StartupLink(all)); Delete(Path.Combine(MenuFolder(all),"Lucent.lnk"));
        RemoveIfEmpty(MenuFolder(all)); RemoveIfEmpty(AppFolder(all));
        if(!all) Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ResolutionSwitcher","settings.ini"));
    }
    private static void RemoveIfEmpty(string path) {if(Directory.Exists(path)&&Directory.GetFileSystemEntries(path).Length==0)Directory.Delete(path);}
    private static void Delete(string path) {if(File.Exists(path))File.Delete(path);}
    private static void Shortcut(string path,string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        object shell=null,link=null;
        try
        {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            Type type=link.GetType();
            type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
            type.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{target+",0"});
            type.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
        }
        finally {if(link!=null)Marshal.ReleaseComObject(link);if(shell!=null)Marshal.ReleaseComObject(shell);}
    }
}
