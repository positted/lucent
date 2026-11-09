using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace ResolutionSwitcher
{
    internal sealed class ProcessIdentity
    {
        public string Name = "", Path = "", Product = "", Company = "", Ancestors = "";
        public int Id,SessionId=-1; public long StartTicks; public bool IsCritical,IsService;
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll")] private static extern bool IsProcessCritical(IntPtr process,out bool critical);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        public static ProcessIdentity Read(Process process)
        {
            ProcessIdentity result = new ProcessIdentity { Name = process.ProcessName, Id = process.Id };
            result.Ancestors = ProcessTree.Ancestors(result.Id);try{result.SessionId=process.SessionId;result.StartTicks=process.StartTime.ToUniversalTime().Ticks;}catch{}
            IntPtr handle = OpenProcess(0x1000, false, (uint)result.Id);
            if (handle != IntPtr.Zero)
            {
                try { bool critical;if(IsProcessCritical(handle,out critical))result.IsCritical=critical;else result.IsCritical=true; StringBuilder path = new StringBuilder(32768); int size = path.Capacity; if (QueryFullProcessImageName(handle, 0, path, ref size)) result.Path = path.ToString(); }
                finally { CloseHandle(handle); }
            }
            if (!string.IsNullOrEmpty(result.Path))
                try { FileVersionInfo info = FileVersionInfo.GetVersionInfo(result.Path); result.Product = info.ProductName ?? ""; result.Company = info.CompanyName ?? ""; } catch { }
            return result;
        }
        // The executable path of a process using only limited query rights, or null when even that is denied.
        public static string ImagePath(int id)
        {
            IntPtr handle = OpenProcess(0x1000, false, (uint)id);
            if (handle == IntPtr.Zero) return null;
            try { StringBuilder path = new StringBuilder(32768); int size = path.Capacity; return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : null; }
            finally { CloseHandle(handle); }
        }
        internal static bool Contains(string text, string part) { return (text ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; }
        internal bool IsCodex
        {
            get { return Contains(Product, "Codex") || Contains(Path, "OpenAI.Codex_") || (Contains(Company, "OpenAI") && Contains(Path, "\\.codex\\")); }
        }
        internal bool IsRiot
        {
            get { return Contains(Company, "Riot") || Contains(Path, "\\Riot Games\\") || Contains(Path, "\\Riot Vanguard\\") || Contains(Ancestors, "VALORANT") || Contains(Ancestors, "RiotClient") || Contains(Ancestors, "League of Legends") || Contains(Ancestors, "LeagueClient"); }
        }
        internal bool IsStandaloneChatGpt
        {
            get { return Name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) && !IsCodex && !IsRiot && !string.IsNullOrEmpty(Path) && (Product.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) || Contains(Path, "OpenAI.ChatGPT_")) && Contains(Company, "OpenAI"); }
        }
        internal bool MatchesHint(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return true;
            if (string.IsNullOrEmpty(Path)) return false;
            foreach (string token in hint.Split('|')) if (Contains(Product, token) || Contains(Company, token) || Contains(Path, token)) return true;
            return false;
        }
        internal bool IsWindowsComponent
        {
            get
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
                return !string.IsNullOrEmpty(Path) && Path.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
            }
        }
        public string Evidence { get { return Name + ".exe · PID " + Id + Environment.NewLine + (string.IsNullOrEmpty(Path) ? "Executable path unavailable" : Path) + (Ancestors.Length == 0 ? "" : Environment.NewLine + "Parents: " + Ancestors); } }
    }
    internal static class ProcessTree
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Entry
        {
            public uint Size, Usage, Id; public UIntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
        }
        private sealed class Link { public uint Parent; public string Name; }
        [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] private static extern bool First(IntPtr snapshot, ref Entry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] private static extern bool Next(IntPtr snapshot, ref Entry entry);
        [DllImport("kernel32.dll")] private static extern bool IsProcessCritical(IntPtr process,out bool critical);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static Dictionary<uint, Link> links = new Dictionary<uint, Link>();
        private static readonly Stopwatch age = new Stopwatch();
        private static readonly object gate = new object();
        public static void Refresh()
        {
            lock (gate)
            {
                Dictionary<uint, Link> fresh = new Dictionary<uint, Link>();
                IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
                if (snapshot == new IntPtr(-1)) return;
                try { Entry entry = new Entry { Size = (uint)Marshal.SizeOf(typeof(Entry)) }; if (First(snapshot, ref entry)) do { fresh[entry.Id] = new Link { Parent = entry.Parent, Name = entry.Exe ?? "" }; } while (Next(snapshot, ref entry)); }
                finally { CloseHandle(snapshot); }
                links = fresh; age.Restart();
            }
        }
        internal static bool IsDescendant(int id,int parentId)
        {
            lock(gate)
            {
                var visited=new HashSet<uint>();uint current=(uint)id;Link link;
                while(visited.Count<64&&visited.Add(current)&&links.TryGetValue(current,out link)&&link.Parent!=0)
                {if(link.Parent==(uint)parentId)return true;current=link.Parent;}return false;
            }
        }
        public static string Ancestors(int id)
        {
            lock (gate)
            {
                if (!age.IsRunning || age.ElapsedMilliseconds > 1000) Refresh();
                List<string> names = new List<string>(); HashSet<uint> visited = new HashSet<uint>();
                uint current = (uint)id; Link link;
                while (names.Count < 32 && visited.Add(current) && links.TryGetValue(current, out link) && link.Parent != 0)
                {
                    current = link.Parent; Link parent;
                    if (!links.TryGetValue(current, out parent)) break;
                    names.Add(parent.Name);
                }
                return string.Join(" ← ", names);
            }
        }
    }
}
