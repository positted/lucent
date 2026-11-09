using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ResolutionSwitcher;

namespace Lucent
{
    internal static class RamCleanup
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryReading
        {
            public uint Size, Load;
            public ulong Total, Available, TotalPageFile, AvailablePageFile;
            public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
            public ulong Used { get { return Total - Available; } }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MemoryReading reading);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        public static MemoryReading ReadMemory()
        {
            var reading = new MemoryReading { Size = (uint)Marshal.SizeOf(typeof(MemoryReading)) };
            if (!GlobalMemoryStatusEx(ref reading))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return reading;
        }

        public static string RunAutomatically(IEnumerable<string> excluded=null)
        {
            var exclusions=new HashSet<string>(excluded??Enumerable.Empty<string>(),StringComparer.OrdinalIgnoreCase);
            return Run(BackgroundAppBoost.Catalog.Where(d=>!exclusions.Contains(d.Id)), true);
        }

        [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint access,bool inherit,int id);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        [DllImport("psapi.dll",SetLastError=true)]static extern bool EmptyWorkingSet(IntPtr process);
        public static string Run(IEnumerable<BoostAppDefinition> selected, bool automatic = false)
        {
            long before=0,after=0;int trimmed=0,skipped=0;ProcessTree.Refresh();
            var visited = new HashSet<int>();
            int ownId, session;
            using (var current = Process.GetCurrentProcess())
            {
                ownId = current.Id;
                session = current.SessionId;
            }
            foreach(var definition in selected)foreach(string name in definition.ProcessNames.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach(var process in Process.GetProcessesByName(name))using(process)
            {
                IntPtr handle=IntPtr.Zero;
                try{
                    var identity=ProcessIdentity.Read(process);
                    if (identity.Id == ownId || process.SessionId != session ||
                        string.IsNullOrEmpty(identity.Path) || !BackgroundAppBoost.CanManage(definition,identity))
                    { skipped++; continue; }
                    if (automatic&&identity.IsRiot){skipped++;continue;}if (automatic)
                    {
                        uint foreground;
                        GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                        if (foreground == (uint)identity.Id) { skipped++; continue; }
                        try
                        {
                            if(foreground!=0)using(var active=Process.GetProcessById((int)foreground))
                            {
                                var activeIdentity=ProcessIdentity.Read(active);
                                if(string.Equals(identity.Path,activeIdentity.Path,StringComparison.OrdinalIgnoreCase) ||
                                    ProcessIdentity.Contains(identity.Ancestors,activeIdentity.Name+".exe"))
                                {skipped++;continue;}
                            }
                        }
                        catch {skipped++;continue;}
                    }
                    if (!visited.Add(identity.Id)) continue;
                    handle=OpenProcess(0x100|0x400,false,process.Id);if(handle==IntPtr.Zero){skipped++;continue;}
                    long initial=process.WorkingSet64;
                    if(!EmptyWorkingSet(handle))
                    {
                        skipped++;
                        continue;
                    }
                    process.Refresh();
                    before+=initial;
                    after+=process.WorkingSet64;
                    trimmed++;
                }catch{skipped++;}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
            }
            return "Trimmed "+trimmed+" background processes · observed working-set reduction "+Math.Max(0,(before-after)/1048576)+" MB · "+skipped+" skipped. Pages can return as apps use them; this does not guarantee more FPS.";
        }
    }
    // Decides when memory has stopped falling after a cleanup, and how low it got.
    internal sealed class MemorySettler
    {
        public const int SampleMilliseconds=250;
        const double QuietSeconds=2,LongestSeconds=10;
        // Smaller movements are ordinary background noise, not the cleanup still working.
        const ulong NoiseBytes=24UL*1024*1024;
        ulong level;
        double lastFall;
        public ulong Lowest {get;private set;}
        public MemorySettler(ulong start){Lowest=level=start;}
        // Returns true once there has been no real fall for a while, or the wait has gone on long enough.
        public bool Add(double seconds,ulong used)
        {
            if(used<Lowest)Lowest=used;
            if(used+NoiseBytes<level){level=used;lastFall=seconds;}
            return seconds>=LongestSeconds||seconds-lastFall>=QuietSeconds;
        }
    }
    internal static class DesktopTools
    {
        const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        public static bool Startup
        {
            get
            {
                using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&(key.GetValue("Lucent")!=null||key.GetValue(LegacyNames.StartupValue)!=null);
            }
            set
            {
                using(var key=Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    key.DeleteValue(LegacyNames.StartupValue,false);
                    if(value)key.SetValue("Lucent","\""+System.Windows.Forms.Application.ExecutablePath+"\" --tray");
                    else key.DeleteValue("Lucent",false);
                }
            }
        }
        [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
        public static void Media(byte key){keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);}
    }
}
