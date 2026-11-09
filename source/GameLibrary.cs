using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Lucent
{
    internal sealed class LibraryGame
    {
        public string Name,Executable,Store,Launch;
        // Only for the pictures of the app (tests): a picture to show in place of the program's own icon.
        public System.Windows.Media.ImageSource Picture;
    }

    // The games installed on this PC, found from the launchers' own install records and the files they point to. Nothing here starts, opens or
    // reads inside a game; it only lists folders and file names, and later takes an icon from a program's file.
    internal static class GameLibrary
    {
        static readonly Regex Junk=new Regex(@"(unins|uninst|crash|report|redist|vcredist|dxsetup|setup|installer|helper|updater|prereq|dotnet|vc_|easyanticheat|battleye|benchmark|editor|eac)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
        // Finds every game the launchers know about. Slow on a big library, so callers run it off the interface thread.
        public static List<LibraryGame> Scan()
        {
            var found=new List<LibraryGame>();
            try{found.AddRange(Steam());}catch(Exception){}
            try{found.AddRange(Epic());}catch(Exception){}
            try{found.AddRange(Gog());}catch(Exception){}
            try{found.AddRange(Ubisoft());}catch(Exception){}
            try{found.AddRange(EaApp());}catch(Exception){}
            try{found.AddRange(BattleNet());}catch(Exception){}
            try{found.AddRange(Riot());}catch(Exception){}
            try{found.AddRange(Xbox());}catch(Exception){}
            // One entry per program, the best name first.
            return found.Where(g=>!string.IsNullOrWhiteSpace(g.Executable)&&File.Exists(g.Executable))
                .GroupBy(g=>g.Executable,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())
                .OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        static IEnumerable<LibraryGame> Steam()
        {
            string root=null;
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))if(key!=null)root=key.GetValue("SteamPath") as string;
            if(string.IsNullOrEmpty(root))yield break;
            root=root.Replace('/','\\');
            var libraries=new List<string>{root};
            string vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");
            if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace("\\\\","\\"));
            foreach(string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string apps=Path.Combine(library,"steamapps");
                if(!Directory.Exists(apps))continue;
                foreach(string manifest in Directory.GetFiles(apps,"appmanifest_*.acf"))
                {
                    string text;try{text=File.ReadAllText(manifest);}catch(IOException){continue;}
                    var name=Regex.Match(text,"\"name\"\\s+\"([^\"]+)\"");var folder=Regex.Match(text,"\"installdir\"\\s+\"([^\"]+)\"");
                    if(!name.Success||!folder.Success)continue;
                    string title=name.Groups[1].Value;
                    // Steam lists its own tools and runtimes as apps too.
                    if(Regex.IsMatch(title,@"^(Steamworks|Steam Linux|Proton|Steam Controller|Source SDK)",RegexOptions.IgnoreCase))continue;
                    string dir=Path.Combine(apps,"common",folder.Groups[1].Value);
                    string exe=BestExecutable(dir);
                    var appId=Regex.Match(Path.GetFileName(manifest),@"appmanifest_(\d+)\.acf");
                    if(exe!=null)yield return new LibraryGame {Name=title,Executable=exe,Store="Steam",Launch=appId.Success?"steam://rungameid/"+appId.Groups[1].Value:null};
                }
            }
        }
        static IEnumerable<LibraryGame> Epic()
        {
            string manifests=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests");
            if(!Directory.Exists(manifests))yield break;
            var json=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
            foreach(string item in Directory.GetFiles(manifests,"*.item"))
            {
                IDictionary<string,object> data;
                try{data=json.DeserializeObject(File.ReadAllText(item)) as IDictionary<string,object>;}catch(Exception){continue;}
                if(data==null)continue;
                object name,location,launch,ns,catalogItem,appName;
                data.TryGetValue("DisplayName",out name);data.TryGetValue("InstallLocation",out location);data.TryGetValue("LaunchExecutable",out launch);
                data.TryGetValue("CatalogNamespace",out ns);data.TryGetValue("CatalogItemId",out catalogItem);data.TryGetValue("AppName",out appName);
                string folder=location as string,relative=launch as string,title=name as string;
                if(string.IsNullOrEmpty(folder)||string.IsNullOrEmpty(relative)||string.IsNullOrEmpty(title))continue;
                string exe=Path.Combine(folder,relative.Replace('/','\\'));
                string epicLaunch=ns is string&&catalogItem is string&&appName is string?"com.epicgames.launcher://apps/"+Uri.EscapeDataString((string)ns)+"%3A"+Uri.EscapeDataString((string)catalogItem)+"%3A"+Uri.EscapeDataString((string)appName)+"?action=launch&silent=true":null;
                if(File.Exists(exe))yield return new LibraryGame {Name=title,Executable=exe,Store="Epic Games",Launch=epicLaunch};
            }
        }
        // ---- The other launchers. Each is found only from what it records on this PC (registry entries, install records, folders): nothing is started or opened.
        // Launchers and their own helpers are not games.
        static readonly Regex NotAGame=new Regex(@"^(EA app|EA Desktop|Origin|Battle\.net|Ubisoft Connect|Uplay|Epic Games Launcher|GOG GALAXY|Riot Client|Xbox|Gaming Services)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
        static string RegText(RegistryKey key,string name){var value=key==null?null:key.GetValue(name) as string;return string.IsNullOrWhiteSpace(value)?null:value.Trim();}
        static string CleanPath(string path){return string.IsNullOrWhiteSpace(path)?null:path.Trim().Trim('"').Replace('/','\\').Replace("\\\\","\\");}
        static IEnumerable<RegistryKey> SubKeys(string path)
        {
            var list=new List<RegistryKey>();
            // The 32-bit view holds most launchers' records; the 64-bit view is checked as well.
            foreach(string prefix in new[]{@"SOFTWARE\WOW6432Node\",@"SOFTWARE\"})
            {
                RegistryKey parent=null;
                try{parent=Registry.LocalMachine.OpenSubKey(prefix+path);}catch(Exception){}
                if(parent==null)continue;
                foreach(string name in parent.GetSubKeyNames()){try{var sub=parent.OpenSubKey(name);if(sub!=null)list.Add(sub);}catch(Exception){}}
                parent.Dispose();
            }
            return list;
        }
        // GOG: one key per game under GOG.com\Games, with its name and its program.
        static IEnumerable<LibraryGame> Gog()
        {
            var games=new List<LibraryGame>();
            foreach(var key in SubKeys(@"GOG.com\Games"))
                using(key)
                {
                    string name=RegText(key,"gameName"),exe=CleanPath(RegText(key,"exe")),folder=CleanPath(RegText(key,"path"));
                    if(exe==null&&folder!=null)exe=BestExecutable(folder);
                    if(name!=null&&exe!=null)games.Add(new LibraryGame {Name=name,Executable=exe,Store="GOG"});
                }
            return games;
        }
        // Ubisoft Connect: Installs\<number> has the folder; its name is in the matching uninstall entry. It can start a game by its number.
        static IEnumerable<LibraryGame> Ubisoft()
        {
            var games=new List<LibraryGame>();
            foreach(var key in SubKeys(@"Ubisoft\Launcher\Installs"))
                using(key)
                {
                    string id=Path.GetFileName(key.Name),folder=CleanPath(RegText(key,"InstallDir"));
                    if(folder==null||!Directory.Exists(folder)||!Regex.IsMatch(id,@"^\d{1,9}$"))continue;
                    string name=null;
                    try{using(var uninstall=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Uplay Install "+id))name=RegText(uninstall,"DisplayName");}catch(Exception){}
                    if(name==null)name=Path.GetFileName(folder.TrimEnd('\\'));
                    string exe=BestExecutable(folder);
                    if(exe!=null)games.Add(new LibraryGame {Name=name,Executable=exe,Store="Ubisoft Connect",Launch="uplay://launch/"+id+"/0"});
                }
            return games;
        }
        // EA app (and older Origin games): each game has a key under EA Games or Electronic Arts with its install folder.
        static IEnumerable<LibraryGame> EaApp()
        {
            var games=new List<LibraryGame>();
            foreach(string group in new[]{"EA Games","Electronic Arts"})
                foreach(var key in SubKeys(group))
                    using(key)
                    {
                        string folder=CleanPath(RegText(key,"Install Dir")??RegText(key,"InstallDir")),name=RegText(key,"DisplayName")??Path.GetFileName(key.Name);
                        if(folder==null||!Directory.Exists(folder)||NotAGame.IsMatch(name))continue;
                        string exe=BestExecutable(folder);
                        if(exe!=null)games.Add(new LibraryGame {Name=name,Executable=exe,Store="EA app"});
                    }
            return games;
        }
        // Battle.net: Blizzard's games are listed as installed programs by Blizzard Entertainment.
        static IEnumerable<LibraryGame> BattleNet()
        {
            var games=new List<LibraryGame>();
            foreach(var key in SubKeys(@"Microsoft\Windows\CurrentVersion\Uninstall"))
                using(key)
                {
                    if(!string.Equals(RegText(key,"Publisher"),"Blizzard Entertainment",StringComparison.OrdinalIgnoreCase))continue;
                    string name=RegText(key,"DisplayName"),folder=CleanPath(RegText(key,"InstallLocation"));
                    if(name==null||folder==null||NotAGame.IsMatch(name)||!Directory.Exists(folder))continue;
                    string exe=BestExecutable(folder);
                    if(exe!=null)games.Add(new LibraryGame {Name=name,Executable=exe,Store="Battle.net"});
                }
            return games;
        }
        // Riot: each product has a settings file under Riot Games\Metadata with the folder it is installed in.
        static IEnumerable<LibraryGame> Riot()
        {
            var games=new List<LibraryGame>();
            string metadata=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Riot Games","Metadata");
            if(!Directory.Exists(metadata))return games;
            foreach(string folder in Directory.GetDirectories(metadata))
            {
                string product=Path.GetFileName(folder);
                if(!product.EndsWith(".live",StringComparison.OrdinalIgnoreCase))continue;
                string settings=Path.Combine(folder,product+".product_settings.yaml");
                if(!File.Exists(settings))continue;
                string install=RiotInstallFolder(File.ReadAllText(settings));
                if(install==null||!Directory.Exists(install))continue;
                string exe=RiotExecutable(product.Substring(0,product.Length-5),install);
                if(exe!=null)games.Add(new LibraryGame {Name=RiotName(product.Substring(0,product.Length-5)),Executable=exe,Store="Riot Games"});
            }
            return games;
        }
        internal static string RiotInstallFolder(string yaml)
        {
            var match=Regex.Match(yaml??"",@"product_install_full_path:\s*""?([^""\r\n]+)""?");
            return match.Success?CleanPath(match.Groups[1].Value):null;
        }
        internal static string RiotName(string product)
        {
            switch(product.ToLowerInvariant())
            {
                case "valorant":return "VALORANT";
                case "league_of_legends":return "League of Legends";
                case "bacon":return "Legends of Runeterra";
                case "lion":return "2XKO";
                default:return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(product.Replace('_',' '));
            }
        }
        static string RiotExecutable(string product,string install)
        {
            string known=null;
            if(product.Equals("valorant",StringComparison.OrdinalIgnoreCase))known=Path.Combine(install,"live","ShooterGame","Binaries","Win64","VALORANT-Win64-Shipping.exe");
            else if(product.Equals("league_of_legends",StringComparison.OrdinalIgnoreCase))known=Path.Combine(install,"Game","League of Legends.exe");
            if(known!=null&&File.Exists(known))return known;
            return BestExecutable(install);
        }
        // Xbox app and Game Pass on PC: games live in an XboxGames folder on a drive; the game's own config names its program.
        static IEnumerable<LibraryGame> Xbox()
        {
            var games=new List<LibraryGame>();
            foreach(var drive in DriveInfo.GetDrives())
            {
                string root;
                try{if(drive.DriveType!=DriveType.Fixed||!drive.IsReady)continue;root=Path.Combine(drive.RootDirectory.FullName,"XboxGames");}catch(Exception){continue;}
                if(!Directory.Exists(root))continue;
                foreach(string game in Directory.GetDirectories(root))
                {
                    string content=Path.Combine(game,"Content");
                    if(!Directory.Exists(content))continue;
                    string title=Path.GetFileName(game),exe=null,config=Path.Combine(content,"MicrosoftGame.config");
                    if(File.Exists(config))
                    {
                        string configName,configExe;
                        if(ParseXboxConfig(File.ReadAllText(config),out configName,out configExe))
                        {
                            if(!string.IsNullOrEmpty(configName))title=configName;
                            if(!string.IsNullOrEmpty(configExe)){string candidate=Path.Combine(content,configExe);if(File.Exists(candidate))exe=candidate;}
                        }
                    }
                    if(exe==null)exe=BestExecutable(content);
                    if(exe!=null&&!NotAGame.IsMatch(title))games.Add(new LibraryGame {Name=title,Executable=exe,Store="Xbox"});
                }
            }
            return games;
        }
        // The game's name and program from MicrosoftGame.config. A name that is only a resource reference (ms-resource:...) is left out.
        internal static bool ParseXboxConfig(string xml,out string name,out string executable)
        {
            name=null;executable=null;
            try
            {
                var document=new System.Xml.XmlDocument {XmlResolver=null};
                document.LoadXml(xml);
                var exe=document.SelectSingleNode("//*[local-name()='Executable']/@Name");
                if(exe!=null)executable=exe.Value.Replace('/','\\');
                var shell=document.SelectSingleNode("//*[local-name()='ShellVisuals']/@DefaultDisplayName");
                if(shell!=null&&!shell.Value.StartsWith("ms-resource",StringComparison.OrdinalIgnoreCase))name=shell.Value;
                return executable!=null||name!=null;
            }
            catch(Exception){return false;}
        }
        // The program that is most likely the game: the biggest executable in the folder that is not an installer, updater or helper.
        internal static string BestExecutable(string folder)
        {
            if(!Directory.Exists(folder))return null;
            try
            {
                var candidates=new List<FileInfo>();
                // The game folder itself, its subfolders, and the usual places a game keeps its program inside each.
                var places=new List<string>{folder};
                foreach(string sub in Directory.EnumerateDirectories(folder))
                    if(!Regex.IsMatch(Path.GetFileName(sub),@"^(_?redist|directx|vcredist|dotnet|__installer|engine|launcher|tools|support)$",RegexOptions.IgnoreCase))places.Add(sub);
                foreach(string place in places)
                {
                    foreach(string inner in new[]{"",@"Binaries\Win64","bin",@"bin\x64","x64","Win64"})
                    {
                        string directory=inner.Length==0?place:Path.Combine(place,inner);
                        if(Directory.Exists(directory))foreach(string exe in Directory.EnumerateFiles(directory,"*.exe"))candidates.Add(new FileInfo(exe));
                    }
                }                var best=candidates.Where(f=>!Junk.IsMatch(f.Name)).OrderByDescending(f=>f.Length).FirstOrDefault()??candidates.OrderByDescending(f=>f.Length).FirstOrDefault();
                return best==null?null:best.FullName;
            }
            catch(Exception){return null;}
        }

        // Starts a game the way its launcher would, so its updates, sign-in and anti-cheat run as usual: through the store address when there is one,
        // otherwise from the program file. Only those two kinds of address are ever opened. Nothing is closed or touched if it is already running.
        public static string Start(string launch,string executable)
        {
            try
            {
                if(IsStoreAddress(launch)){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launch){UseShellExecute=true});return null;}
                if(!string.IsNullOrEmpty(executable)&&executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(executable))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(executable)});
                    return null;
                }
                return "The game could not be found. It may have been moved or uninstalled.";
            }
            catch(Exception error){return "The game could not be started: "+error.Message;}
        }
        internal static bool IsStoreAddress(string launch)
        {
            if(string.IsNullOrEmpty(launch))return false;
            if(Regex.IsMatch(launch,@"^steam://rungameid/\d{1,12}$"))return true;
            if(Regex.IsMatch(launch,@"^uplay://launch/\d{1,9}/0$"))return true;
            return Regex.IsMatch(launch,@"^com\.epicgames\.launcher://apps/[A-Za-z0-9%._-]+\?action=launch&silent=true$");
        }
        static readonly Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
        // The icon of a program, read from its file and kept. The result goes to show, on the interface thread; nothing is shown if it has none.
        public static void LoadIcon(string executable,System.Windows.Threading.Dispatcher dispatcher,Action<ImageSource> show)
        {
            if(string.IsNullOrEmpty(executable))return;
            ImageSource known;
            lock(icons){if(icons.TryGetValue(executable,out known)){if(known!=null)show(known);return;}}
            Task.Run(()=>
            {
                try
                {
                    if(!File.Exists(executable))return (ImageSource)null;
                    using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(executable))
                    {
                        var picture=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));
                        picture.Freeze();
                        return (ImageSource)picture;
                    }
                }
                catch(Exception){return null;}
            }).ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>
            {
                var picture=task.Status==TaskStatus.RanToCompletion?task.Result:null;
                lock(icons)icons[executable]=picture;
                if(picture!=null)show(picture);
            })));
        }
    }
}
