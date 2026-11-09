using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ResolutionSwitcher
{
    internal sealed class BoostStamp {public int Id;public long Started;public string Path;}
    internal static class BoostPolicy
    {
        internal static bool ProtectedName(string name)
        {
            string n=(name??"").ToLowerInvariant();
            string[] names={"idle","system","registry","smss","csrss","wininit","services","lsass","winlogon","svchost","fontdrvhost","dwm","explorer","shellexperiencehost","startmenuexperiencehost","searchhost","searchindexer","textinputhost","runtimebroker","taskhostw","sihost","ctfmon","audiodg","spoolsv","wmiprvse","securityhealthservice","securityhealthsystray","msmpeng","mpdefendercoreservice","nissrv","smartscreen","vgc","vgk","vgtray","vanguard","nvdisplay.container","nvcontainer","lghub_agent","lghub_updater","lghub","lghub_system_tray","logi_lamparray_service.amd64","razerappengine","razer synapse service","hidhideclient","lucent","lucent"};
            return names.Contains(n)||KnownGameName(n);
        }
        internal static bool KnownGameName(string name)
        {
            string n=(name??"").ToLowerInvariant();
            return n.Contains("-win64-shipping")||n.Contains("-wingdk-shipping")||new[]{"cs2","csgo","r5apex","rainbowsix","rainbowsix_vulkan","tslgame","gta5","gta5_enhanced","league of legends","dota2","overwatch","rocketleague","robloxplayerbeta","eldenring","destiny2","rustclient"}.Contains(n);
        }
        internal static string CoreReason(ProcessIdentity identity)
        {
            string n=(identity.Name??"").ToLowerInvariant(),path=identity.Path??"",company=identity.Company??"",product=identity.Product??"";
            if(n=="vgc"||n=="vgk"||n=="vgtray"||n.Contains("vanguard")||ProcessIdentity.Contains(path,"\\Riot Vanguard\\")||ProcessIdentity.Contains(product,"Riot Vanguard"))return "Riot Vanguard · never modified";
            if(identity.IsCritical)return "Critical Windows process";
            if(ProtectedName(n))return KnownGameName(n)?"Game process":"System, security or device component";
            if(identity.IsWindowsComponent)return "Windows component";
            if(identity.IsService||identity.SessionId==0)return "Windows service · not closed";
            if(string.IsNullOrEmpty(path))return "Executable identity unavailable";
            if(identity.Id>0&&identity.StartTicks==0)return "Process start time unavailable";
            string evidence=(company+" "+product+" "+path).ToLowerInvariant();
            string[] security={"crowdstrike","sentinelone","kaspersky","bitdefender","malwarebytes","windows defender","microsoft defender","sophos","eset","norton","symantec","avast","avg technologies","mcafee","f-secure","trend micro","easyanticheat","easy anti-cheat","battleye","faceit","ricochet","anticheat","anti-cheat"};
            if(security.Any(s=>evidence.Contains(s)))return "Security / anti-cheat component";
            string[] driver={"\\driverstore\\","\\system32\\drivers\\","\\nvidia corporation\\nvcontainer","\\amd\\cn\\","\\intel\\driver","\\corsair\\corsair icue5 software\\services","\\razer\\services"};
            if(driver.Any(s=>evidence.Contains(s)))return "Driver or hardware service";
            if(ProcessIdentity.Contains(path,"\\VALORANT\\live\\ShooterGame\\Binaries\\"))return "Game process";
            return null;
        }
        internal static bool Matches(BoostAppDefinition definition,ProcessIdentity identity)
        {
            if(!definition.ProcessNames.Contains(identity.Name,StringComparer.OrdinalIgnoreCase)||CoreReason(identity)!=null)return false;
            if(!identity.MatchesHint(definition.IdentityHint))return false;
            if(identity.IsRiot&&!definition.Id.StartsWith("riot",StringComparison.OrdinalIgnoreCase)&&!definition.Dynamic)return false;
            if(definition.Id=="codex")return identity.IsCodex||ProcessIdentity.Contains(identity.Path,"\\.codex\\")||ProcessIdentity.Contains(identity.Name,"codex");
            if(identity.IsCodex)return definition.Dynamic;
            if(definition.Id=="chatgpt")return identity.IsStandaloneChatGpt;
            return true;
        }
    }
    internal static class RunningServices
    {
        [StructLayout(LayoutKind.Sequential)]struct Status {public uint Type,State,Controls,Exit,Specific,Checkpoint,WaitHint,Pid,Flags;}
        [StructLayout(LayoutKind.Sequential)]struct Entry {public IntPtr Name,Display;public Status Status;}
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenSCManager(string machine,string database,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EnumServicesStatusEx(IntPtr manager,int info,uint type,uint state,IntPtr buffer,int bytes,out int needed,out int returned,ref int resume,string group);
        [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(IntPtr handle);
        internal static Dictionary<int,string> Read()
        {
            var result=new Dictionary<int,string>();IntPtr manager=OpenSCManager(null,null,4);if(manager==IntPtr.Zero)return result;
            IntPtr buffer=IntPtr.Zero;
            try
            {
                int needed,count,resume=0;EnumServicesStatusEx(manager,0,0x30,1,IntPtr.Zero,0,out needed,out count,ref resume,null);
                if(needed<=0||needed>4*1024*1024)return result;
                buffer=Marshal.AllocHGlobal(needed);resume=0;
                if(!EnumServicesStatusEx(manager,0,0x30,1,buffer,needed,out needed,out count,ref resume,null))return result;
                int size=Marshal.SizeOf(typeof(Entry));
                for(int i=0;i<count;i++)
                {
                    var entry=(Entry)Marshal.PtrToStructure(IntPtr.Add(buffer,i*size),typeof(Entry));
                    if(entry.Status.Pid==0)continue;
                    string name=Marshal.PtrToStringUni(entry.Display);
                    int id=(int)entry.Status.Pid;
                    result[id]=result.ContainsKey(id)?result[id]+", "+name:name;
                }
            }
            catch{}finally{if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);CloseServiceHandle(manager);}return result;
        }
    }
    internal sealed class AdvancedBoostEngine
    {
        readonly BoostAppDefinition[] catalog;
        readonly Func<ProcessIdentity> foregroundReader;
        readonly Dictionary<string,BoostAppDefinition> scanned=new Dictionary<string,BoostAppDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,string> restore=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        int session,ownId;
        const int ClosePatienceMs=3000;
        internal string LastSkipReason;
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
        internal AdvancedBoostEngine(BoostAppDefinition[] catalog):this(catalog,null){}
        internal AdvancedBoostEngine(BoostAppDefinition[] catalog,Func<ProcessIdentity> foregroundReader)
        {
            this.catalog=catalog;
            this.foregroundReader=foregroundReader??ReadForeground;
            using(var p=Process.GetCurrentProcess())
            {
                session=p.SessionId;
                ownId=p.Id;
            }
        }
        static ProcessIdentity ReadForeground()
        {
            uint id;
            GetWindowThreadProcessId(GetForegroundWindow(),out id);
            if(id==0)return null;
            using(var process=Process.GetProcessById((int)id))return ProcessIdentity.Read(process);
        }
        sealed class Sample {public Process Process;public BoostCandidate Candidate;public TimeSpan Cpu;public long At;}
        static string DynamicId(string path)
        {
            using(var sha=SHA256.Create())return "app:"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Replace("-","").Substring(0,24);
        }
        internal IList<BoostCandidate> Scan()
        {
            ProcessTree.Refresh();
            var services=RunningServices.Read();
            var groups=new Dictionary<string,BoostCandidate>(StringComparer.OrdinalIgnoreCase);
            var samples=new List<Sample>();
            scanned.Clear();var running=Process.GetProcesses();
            ProcessIdentity active=null;try{active=foregroundReader();}catch{}
            bool riotGame=running.Any(p=>
            {
                try
                {
                    return BoostPolicy.KnownGameName(p.ProcessName)&&(p.ProcessName.IndexOf("VALORANT",StringComparison.OrdinalIgnoreCase)>=0||p.ProcessName.Equals("League of Legends",StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            });
            foreach(var process in running)
            {
                try
                {
                    var identity=ProcessIdentity.Read(process);string service;
                    identity.IsService=services.TryGetValue(identity.Id,out service);
                    string reason=BoostPolicy.CoreReason(identity);
                    if(identity.Id==ownId)reason="Lucent";
                    if(identity.SessionId!=session&&reason==null)reason="Another Windows session";
                    if(reason==null&&active!=null&&(identity.Id==active.Id||!string.IsNullOrEmpty(active.Path)&&string.Equals(identity.Path,active.Path,StringComparison.OrdinalIgnoreCase)||ProcessTree.IsDescendant(identity.Id,active.Id)))reason="Foreground application";
                    if(reason==null&&riotGame&&identity.Name.Equals("RiotClientServices",StringComparison.OrdinalIgnoreCase))reason="Launcher backend used by a running game";
                    var known=catalog.FirstOrDefault(d=>BoostPolicy.Matches(d,identity));
                    string key=known==null?DynamicId(string.IsNullOrEmpty(identity.Path)?identity.Name:identity.Path):known.Id;
                    if(reason!=null)key="protected:"+key;
                    BoostCandidate candidate;
                    if(!groups.TryGetValue(key,out candidate))
                    {
                        var definition=known==null?new BoostAppDefinition(key,DisplayName(identity,service),identity.Name,false,"Review before closing",identity.Name){Dynamic=true}:new BoostAppDefinition(key,known.DisplayName,known.RestoreProcessName,known.Recommended,known.Reason,known.ProcessNames){IdentityHint=known.IdentityHint};
                        definition.Captured=new List<BoostStamp>();
                        candidate=new BoostCandidate{Definition=definition,IsProtected=reason!=null,ProtectionReason=reason,Category=identity.IsService?"Service":known==null?"Application":"Known app"};
                        groups[key]=candidate;
                        if(reason==null)scanned[key]=definition;
                    }
                    candidate.Instances++;
                    candidate.MemoryBytes+=Math.Max(0,process.WorkingSet64);
                    candidate.Evidence.Add(identity.Evidence+(service==null?"":"\nServices: "+service));
                    candidate.Definition.Captured.Add(new BoostStamp {Id=identity.Id,Started=identity.StartTicks,Path=identity.Path});
                    samples.Add(new Sample{Process=process,Candidate=candidate,Cpu=process.TotalProcessorTime,At=Stopwatch.GetTimestamp()});
                }
                catch{process.Dispose();}
            }
            if(samples.Count>0)Thread.Sleep(200);
            foreach(var sample in samples)
            {
                try
                {
                    double elapsed=(Stopwatch.GetTimestamp()-sample.At)*1000d/Stopwatch.Frequency;
                    double cpu=(sample.Process.TotalProcessorTime-sample.Cpu).TotalMilliseconds;
                    sample.Candidate.CpuPercent+=Math.Max(0,cpu)/Math.Max(1,elapsed)/Math.Max(1,Environment.ProcessorCount)*100;
                }
                catch{}finally{sample.Process.Dispose();}
            }
            return groups.Values.OrderBy(x=>x.IsProtected).ThenByDescending(x=>x.CpuPercent).ThenByDescending(x=>x.MemoryBytes).ThenBy(x=>x.Definition.DisplayName).ToList();
        }
        static string DisplayName(ProcessIdentity identity,string service)
        {
            if(!string.IsNullOrWhiteSpace(service))return service;
            string product=identity.Product??"";
            if(!string.IsNullOrWhiteSpace(product)&&!product.Equals("Microsoft® Windows® Operating System",StringComparison.OrdinalIgnoreCase))return product.Length>64?product.Substring(0,64):product;
            return identity.Name;
        }
        bool IsForeground(ProcessIdentity identity)
        {
            try
            {
                var active=foregroundReader();
                if(active==null)return false;
                return active.Id==identity.Id||!string.IsNullOrEmpty(active.Path)&&string.Equals(identity.Path,active.Path,StringComparison.OrdinalIgnoreCase)||ProcessTree.IsDescendant(identity.Id,active.Id);
            }
            catch{return true;}
        }
        bool CanClose(BoostAppDefinition definition,Process process,BoostStamp stamp)
        {
            try
            {
                var identity=ProcessIdentity.Read(process);
                if(identity.Id==ownId||identity.SessionId!=session){LastSkipReason="Protected process/session";return false;}
                if(identity.StartTicks!=stamp.Started||!string.Equals(identity.Path,stamp.Path,StringComparison.OrdinalIgnoreCase))
                {
                    LastSkipReason="Process changed since the scan";
                    return false;
                }
                if(!BoostPolicy.Matches(definition,identity))
                {
                    LastSkipReason=BoostPolicy.CoreReason(identity)??"Executable identity did not match";
                    return false;
                }
                if(IsForeground(identity)){LastSkipReason="Foreground application or helper";return false;}
                if(identity.Name.Equals("RiotClientServices",StringComparison.OrdinalIgnoreCase))
                {
                    foreach(string game in new[]{"VALORANT-Win64-Shipping","League of Legends"})foreach(var p in Process.GetProcessesByName(game))using(p)if(!p.HasExited)
                    {
                        LastSkipReason="Launcher backend used by a running game";
                        return false;
                    }
                }
                return true;
            }
            catch(Exception error){LastSkipReason="Process could not be rechecked: "+error.GetType().Name;return false;}
        }
        internal BoostResult CloseSelected(IEnumerable<string> ids,IEnumerable<string> excluded)
        {
            LastSkipReason=null;
            var blocked=new HashSet<string>(excluded??Enumerable.Empty<string>(),StringComparer.OrdinalIgnoreCase);
            int closed=0,apps=0,skipped=0,waiting=0;
            foreach(string id in (ids??Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                BoostAppDefinition definition;if(blocked.Contains(id)||!scanned.TryGetValue(id,out definition)){skipped++;continue;}
                bool affected=false;
                foreach(var stamp in definition.Captured.OrderByDescending(x=>Path.GetFileNameWithoutExtension(x.Path).Equals(definition.RestoreProcessName,StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using(var process=Process.GetProcessById(stamp.Id))
                        {
                            if(!CanClose(definition,process,stamp)){skipped++;continue;}
                            // A window that stays open may be asking to save. Leave it and its helpers alone.
                            if(process.CloseMainWindow()&&!process.WaitForExit(ClosePatienceMs)){waiting++;break;}
                            if(!process.HasExited)
                            {
                                if(!CanClose(definition,process,stamp)){skipped++;continue;}
                                process.Kill();process.WaitForExit(400);
                            }
                            if(process.HasExited)
                            {
                                closed++;
                                affected=true;
                                if(!restore.ContainsKey(id)&&File.Exists(stamp.Path))restore[id]=stamp.Path;
                            }
                        }
                    }
                    catch{skipped++;}
                }
                if(affected)apps++;
            }
            return new BoostResult {StoppedProcesses=closed,AffectedApps=apps,Message=closed+" process"+(closed==1?"":"es")+" closed across "+apps+" apps"+(skipped>0?" · "+skipped+" skipped after rechecking"+(LastSkipReason==null?"":" ("+LastSkipReason+")"):"")+(waiting>0?" · "+waiting+" still open, possibly asking to save":"")+"."};
        }
        internal bool HasRestorable {get{return restore.Count>0;}}
        internal BoostResult Restore()
        {
            int count=0;
            foreach(var entry in restore.ToArray())try
            {
                if(File.Exists(entry.Value))
                {
                    Process.Start(new ProcessStartInfo(entry.Value){UseShellExecute=true});
                    count++;
                }
            }
            catch{}restore.Clear();
            return new BoostResult {AffectedApps=count,Message=count+" apps reopened. Unsaved work cannot be restored."};
        }
    }
}
