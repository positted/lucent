using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Lucent
{
    internal sealed class PresenceOptions
    {
        public bool Timeline=true,Button=true;
        // Show "Idle" while Lucent is open and no game is running (off here; the setting turns it on).
        public bool Idle=false;
        // Show the game status as "Competing", which Discord lists beside a game's own "Playing" card; a second "Playing" card is hidden behind it.
        public bool Competing=false;
        // Show the song that is playing, with its cover (off here; the setting turns it on).
        public bool Songs=false;
    }
    // What Lucent knows right now: the game running from the user's game profiles (if any), its picture, and since when.
    internal sealed class PresenceInfo
    {
        public string Game,GameIcon;
        public DateTime Since;   // UTC
        // The song playing now (no title: none), the address of its cover picture (may be null), and how far in it is, in seconds.
        public string Title,Artist,CoverUrl;
        public double Position,Duration;
    }

    // What Lucent tells Discord: only that a game from the user's own game profiles is running, like "Playing Rust with Lucent". With no game
    // running nothing is shown. Nothing else leaves the PC, and only while Discord Rich Presence is switched on.
    internal static class DiscordActivity
    {
        // The Discord application that owns the status. It gives the status its name, and holds the "lucent" picture.
        public const string ClientId="1568368030155542691";
        public const string ImageKey="lucent";
        public const string ButtonLabel="Get Lucent";
        public const string SongLine="Listening with Lucent";
        public static string ButtonUrl{get{return "https://github.com/"+Updates.DefaultSource+"/releases";}}

        // Whether the app playing the media is a web browser, judged by the app's Windows identity (for example "Chrome", "MSEdge", "Brave", "Firefox-...").
        internal static bool IsBrowserSource(string source)
        {
            if(string.IsNullOrWhiteSpace(source))return false;
            string name=source.ToLowerInvariant();
            foreach(string browser in new[]{"chrome","msedge","microsoftedge","brave","firefox","opera","vivaldi","chromium","iexplore","yandex","librewolf","waterfox","thorium","duckduckgo","tor browser"})
                if(name.Contains(browser))return true;
            return false;
        }
        // Discord wants 2 to 128 characters in each line.
        internal static string Line(string text)
        {
            text=(text??"").Trim();
            if(text.Length==0)return null;
            if(text.Length>128)text=text.Substring(0,127).TrimEnd()+"\u2026";
            if(text.Length<2)text+="\u2800";
            return text;
        }
        // The status to show while a game from the user's game profiles is running: "Playing <game> with Lucent", with the game's picture and the
        // Lucent mark in its corner. With no game running there is nothing to show (null), and the status is cleared.
        internal static Dictionary<string,object> Build(PresenceInfo info,PresenceOptions options,DateTime nowUtc)
        {
            if(options==null||info==null)return null;
            string game=Line(info.Game);
            if(game==null)return Line(info.Title)!=null&&options.Songs?BuildSong(info,options,nowUtc):(options.Idle?BuildIdle(info,options,nowUtc):null);
            var activity=new Dictionary<string,object>();
            activity["type"]=options.Competing?5:0; // Playing, or Competing so it shows beside the game's own card
            activity["status_display_type"]=0;
            activity["name"]=Line(game+" with Lucent");
            activity["details"]=Line("Playing "+game);
            activity["state"]="with Lucent";
            if(options.Timeline)
            {
                DateTime since=info.Since!=default(DateTime)?info.Since:nowUtc;
                var stamps=new Dictionary<string,object>();
                stamps["start"]=(long)Math.Round(since.ToUniversalTime().Subtract(new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds);
                activity["timestamps"]=stamps;
            }
            var assets=new Dictionary<string,object>();
            if(!string.IsNullOrEmpty(info.GameIcon))
            {
                assets["large_image"]=info.GameIcon;assets["large_text"]=game;
                assets["small_image"]=ImageKey;assets["small_text"]="Lucent";
            }
            else{assets["large_image"]=ImageKey;assets["large_text"]="Lucent";}
            activity["assets"]=assets;
            if(options.Button)
            {
                var button=new Dictionary<string,object>();button["label"]=ButtonLabel;button["url"]=ButtonUrl;
                activity["buttons"]=new object[]{button};
            }
            return activity;
        }
        // A song is playing and no game is running: "Listening to Lucent", the song and its artist, the cover with the Lucent mark in its corner,
        // and the song's own progress bar.
        static Dictionary<string,object> BuildSong(PresenceInfo info,PresenceOptions options,DateTime nowUtc)
        {
            var activity=new Dictionary<string,object>();
            activity["type"]=2; // Listening
            activity["status_display_type"]=0;
            activity["name"]="Lucent";
            activity["details"]=Line(info.Title);
            string artist=Line(info.Artist);
            if(artist!=null)activity["state"]=artist;
            if(options.Timeline&&info.Duration>1)
            {
                double position=Math.Max(0,Math.Min(info.Duration,info.Position));
                var epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
                double start=nowUtc.ToUniversalTime().Subtract(epoch).TotalMilliseconds-position*1000;
                var stamps=new Dictionary<string,object>();
                stamps["start"]=(long)Math.Round(start);stamps["end"]=(long)Math.Round(start+info.Duration*1000);
                activity["timestamps"]=stamps;
            }
            var assets=new Dictionary<string,object>();
            // The line under the artist (where Spotify shows the album) says how it is being played.
            if(!string.IsNullOrEmpty(info.CoverUrl)){assets["large_image"]=info.CoverUrl;assets["large_text"]=SongLine;assets["small_image"]=ImageKey;assets["small_text"]="Lucent";}
            else{assets["large_image"]=ImageKey;assets["large_text"]=SongLine;}
            activity["assets"]=assets;
            if(options.Button)
            {
                var button=new Dictionary<string,object>();button["label"]=ButtonLabel;button["url"]=ButtonUrl;
                activity["buttons"]=new object[]{button};
            }
            return activity;
        }
        // Lucent is open but nothing is playing and no game is running: "Idle", since Lucent was started, with the Lucent logo.
        static Dictionary<string,object> BuildIdle(PresenceInfo info,PresenceOptions options,DateTime nowUtc)
        {
            var activity=new Dictionary<string,object>();
            activity["type"]=0;
            activity["status_display_type"]=0;
            activity["name"]="Lucent";
            activity["details"]="Idle";
            if(options.Timeline)
            {
                DateTime since=info.Since!=default(DateTime)?info.Since:nowUtc;
                var stamps=new Dictionary<string,object>();
                stamps["start"]=(long)Math.Round(since.ToUniversalTime().Subtract(new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds);
                activity["timestamps"]=stamps;
            }
            var assets=new Dictionary<string,object>();
            assets["large_image"]=ImageKey;assets["large_text"]="Lucent";
            activity["assets"]=assets;
            if(options.Button)
            {
                var button=new Dictionary<string,object>();button["label"]=ButtonLabel;button["url"]=ButtonUrl;
                activity["buttons"]=new object[]{button};
            }
            return activity;
        }
    }
    internal sealed class KnownGame
    {
        public string Name,Icon;
    }

    // Games and their pictures, taken from Discord's own public list of known games (the list Discord itself uses to recognise a running game),
    // so Lucent can say which game is open and show its icon without hosting anything. One download, kept for the session.
    internal static class DiscordGameIcons
    {
        // Tests replace this so nothing is downloaded.
        internal static Func<string> Fetcher=Download;
        static Dictionary<string,KnownGame> byExecutable,byName;
        // For an executable the list gives with a folder ("green hell/gh.exe"), the folder it must run from. A short name like gh.exe is also the GitHub CLI.
        static Dictionary<string,string> folderOf=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static bool loading;
        static readonly object gate=new object();
        internal static void Reset(){lock(gate){byExecutable=null;byName=null;loading=false;}}
        static string Download()
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://discord.com/api/v10/applications/detectable");
            request.UserAgent="Lucent";request.Timeout=15000;request.ReadWriteTimeout=15000;
            using(var response=request.GetResponse())
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
            {
                var text=new StringBuilder();var buffer=new char[16384];int read;
                while(text.Length<48*1024*1024&&(read=reader.Read(buffer,0,buffer.Length))>0)text.Append(buffer,0,read);
                return text.ToString();
            }
        }
        // Discord's list also files programs that many games share (the crash handler every Unity game runs, installers, updaters) under whichever game
        // listed them first, so UnityCrashHandler64 made every Unity game show up as "Spellcraft". Those are not a game being played.
        internal static bool IsHelperProgram(string file)
        {
            string name=(file??"").ToLowerInvariant();
            foreach(string part in new[]{"crashhandler","crashpad","crashreport","crashsender","errorreport","bugreport","unitycrash","dxsetup","vc_redist","vcredist","uninstall","updater"})
                if(name.Contains(part))return true;
            return name=="setup.exe"||name=="install.exe"||name=="installer.exe"||name=="update.exe";
        }
        internal static void Parse(string json)
        {
            var exe=new Dictionary<string,KnownGame>(StringComparer.OrdinalIgnoreCase);var names=new Dictionary<string,KnownGame>(StringComparer.OrdinalIgnoreCase);var folders=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var list=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(json) as System.Collections.IEnumerable;
            if(list!=null)
                foreach(object item in list)
                {
                    var app=item as IDictionary<string,object>;if(app==null)continue;
                    object name,icon,id,execs,aliases;
                    app.TryGetValue("name",out name);app.TryGetValue("id",out id);
                    // The picture is "icon_hash" in Discord's list today (older lists called it "icon").
                    if(!app.TryGetValue("icon_hash",out icon)||icon==null)app.TryGetValue("icon",out icon);
                    if(name==null||id==null)continue;
                    string title=name.ToString();
                    // A name written in capitals (ROBLOX) is shown the way its own alias writes it (Roblox).
                    if(title==title.ToUpperInvariant()&&app.TryGetValue("aliases",out aliases)&&aliases is System.Collections.IEnumerable)
                        foreach(object alias in (System.Collections.IEnumerable)aliases)
                            if(alias is string&&!string.Equals((string)alias,title,StringComparison.Ordinal)&&string.Equals((string)alias,title,StringComparison.OrdinalIgnoreCase)){title=(string)alias;break;}
                    var game=new KnownGame {Name=title,Icon=icon==null?null:"https://cdn.discordapp.com/app-icons/"+id+"/"+icon+".png?size=512"};
                    if(!names.ContainsKey(game.Name))names[game.Name]=game;
                    if(app.TryGetValue("executables",out execs)&&execs is System.Collections.IEnumerable)
                        foreach(object e in (System.Collections.IEnumerable)execs)
                        {
                            var entry=e as IDictionary<string,object>;object file,os,launcher;if(entry==null||!entry.TryGetValue("name",out file)||file==null)continue;
                            if(entry.TryGetValue("os",out os)&&os!=null&&os.ToString()!="win32")continue;
                            if(entry.TryGetValue("is_launcher",out launcher)&&launcher is bool&&(bool)launcher)continue;
                            string listed=file.ToString().TrimStart('>').Replace('/','\\');string leaf=Path.GetFileName(listed);
                            if(leaf.Length>0&&!IsHelperProgram(leaf)&&!exe.ContainsKey(leaf))
                            {
                                exe[leaf]=game;
                                string folder=Path.GetFileName((Path.GetDirectoryName(listed)??"").TrimEnd('\\'));
                                if(!string.IsNullOrEmpty(folder))folders[leaf]=folder;
                            }
                        }
                }
            lock(gate){byExecutable=exe;byName=names;folderOf=folders;}
        }
        // Gets the list started, once. Until it has arrived, lookups find nothing.
        static void Load(out Dictionary<string,KnownGame> exe,out Dictionary<string,KnownGame> names)
        {
            bool start=false;
            lock(gate){exe=byExecutable;names=byName;if(exe==null&&!loading){loading=true;start=true;}}
            if(start)System.Threading.ThreadPool.QueueUserWorkItem(delegate{try{Parse(Fetcher());}catch(Exception){lock(gate){byExecutable=new Dictionary<string,KnownGame>();byName=new Dictionary<string,KnownGame>();}}});
        }
        // The known game for an executable (or, failing that, a game name), or null while the list is not loaded or the game is not in it.
        internal static KnownGame Lookup(string executable,string gameName)
        {
            Dictionary<string,KnownGame> exe,names;Load(out exe,out names);
            if(exe==null)return null;
            KnownGame game;
            if(!string.IsNullOrEmpty(executable)&&exe.TryGetValue(Path.GetFileName(executable),out game))return game;
            if(!string.IsNullOrEmpty(gameName)&&names.TryGetValue(gameName,out game))return game;
            return null;
        }
        // The first running program (by name, like "RobloxPlayerBeta") that is a known game. Only names are looked at.
        internal static KnownGame FindRunning(IEnumerable<string> processNames){return FindRunning(processNames,null);}
        // pathOf gives where a running program is (or null when it cannot be read): a program whose list entry names a folder only counts when it runs from a folder of that name,
        // so a tool like the GitHub CLI (gh.exe) is not taken for the game that shares its file name.
        internal static KnownGame FindRunning(IEnumerable<string> processNames,Func<string,string> pathOf)
        {
            Dictionary<string,KnownGame> exe,names;Load(out exe,out names);
            if(exe==null||processNames==null)return null;
            Dictionary<string,string> folders;lock(gate)folders=folderOf;
            foreach(string name in processNames)
            {
                KnownGame game;string leaf=name+".exe";
                if(string.IsNullOrEmpty(name)||!exe.TryGetValue(leaf,out game))continue;
                string folder;
                if(pathOf!=null&&folders!=null&&folders.TryGetValue(leaf,out folder))
                {
                    string path=null;try{path=pathOf(name);}catch(Exception){}
                    if(path!=null&&path.IndexOf("\\"+folder+"\\",StringComparison.OrdinalIgnoreCase)<0)continue;
                }
                return game;
            }
            return null;
        }
    }
    // Talks to the Discord app on this PC through the pipe it opens for programs (discord-ipc-0 to 9), which the normal, PTB and Canary
    // versions all use. It connects when Discord is running, keeps the status up to date, and clears it when stopped.
    internal sealed class DiscordRpcClient:IDisposable
    {
        public string ClientId="";
        public string PipePrefix="discord-ipc-";
        public Action StatusChanged;
        public int MinimumGap=4000;      // Discord limits how fast a status may change.
        public int RefreshEvery=20000;
        public int RetryEvery=5000;
        string status="Disabled",error="";
        readonly object gate=new object();
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        Dictionary<string,object> wanted;bool dirty,stop,started;
        Thread worker;
        public string Status{get{return status;}}
        public string Error{get{return error;}}

        void SetStatus(string value)
        {
            if(status==value)return;
            status=value;
            var changed=StatusChanged;if(changed!=null)try{changed();}catch(Exception){}
        }
        public void Start()
        {
            if(started)return;
            started=true;
            if(string.IsNullOrEmpty(ClientId)){SetStatus("Needs setup");return;}
            SetStatus("Looking for Discord");
            worker=new Thread(Loop){IsBackground=true,Name="Lucent Discord status"};
            worker.Start();
        }
        // The status to show; null clears it.
        public void Set(Dictionary<string,object> activity)
        {
            lock(gate){wanted=activity;dirty=true;}
            wake.Set();
        }
        public void Dispose()
        {
            stop=true;wake.Set();
            var thread=worker;if(thread!=null)thread.Join(1500);
        }

        // ---- The messages: 4 bytes of kind, 4 bytes of length, then JSON.
        internal static byte[] Frame(int kind,string json)
        {
            byte[] body=Encoding.UTF8.GetBytes(json);
            var frame=new byte[8+body.Length];
            BitConverter.GetBytes(kind).CopyTo(frame,0);BitConverter.GetBytes(body.Length).CopyTo(frame,4);body.CopyTo(frame,8);
            return frame;
        }
        static void Write(Stream stream,int kind,string json)
        {
            byte[] frame=Frame(kind,json);
            if(!stream.WriteAsync(frame,0,frame.Length).Wait(3000))throw new TimeoutException("Discord did not take the message.");
            stream.Flush();
        }
        static byte[] ReadExactly(Stream stream,int count)
        {
            var buffer=new byte[count];int got=0;
            while(got<count)
            {
                var task=stream.ReadAsync(buffer,got,count-got);
                if(!task.Wait(4000))throw new TimeoutException("Discord did not answer.");
                if(task.Result<=0)throw new EndOfStreamException("Discord closed the connection.");
                got+=task.Result;
            }
            return buffer;
        }
        static string Read(Stream stream,out int kind)
        {
            byte[] head=ReadExactly(stream,8);
            kind=BitConverter.ToInt32(head,0);int length=BitConverter.ToInt32(head,4);
            if(length<0||length>1024*1024)throw new InvalidDataException("Discord sent something unexpected.");
            return length==0?"":Encoding.UTF8.GetString(ReadExactly(stream,length));
        }
        static IDictionary<string,object> Parse(string json)
        {
            try{return new JavaScriptSerializer().DeserializeObject(json) as IDictionary<string,object>;}
            catch(Exception){return null;}
        }
        static string Text(IDictionary<string,object> data,string key)
        {
            object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?value.ToString():null;
        }

        NamedPipeClientStream Connect()
        {
            // Look for the pipes first, so a PC without Discord is checked in an instant and stopping never waits on it.
            var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try{foreach(string path in Directory.GetFiles(@"\\.\pipe\"))existing.Add(Path.GetFileName(path));}catch(Exception){}
            for(int i=0;i<10;i++)
            {
                if(!existing.Contains(PipePrefix+i))continue;
                var pipe=new NamedPipeClientStream(".",PipePrefix+i,PipeDirection.InOut,PipeOptions.Asynchronous);
                try{pipe.Connect(250);return pipe;}
                catch(Exception){pipe.Dispose();}
            }
            return null;
        }
        // Says hello and waits for Discord's answer. Returns the user name, or throws with Discord's reason.
        string Handshake(Stream pipe)
        {
            Write(pipe,0,new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"v",1},{"client_id",ClientId}}));
            for(int i=0;i<4;i++)
            {
                int kind;string json=Read(pipe,out kind);
                if(kind==2)throw new InvalidOperationException(Text(Parse(json),"message")??"Discord refused the connection.");
                var message=Parse(json);
                if(Text(message,"evt")=="READY")
                {
                    object data;string name=null;
                    if(message.TryGetValue("data",out data)){var user=data as IDictionary<string,object>;object who;if(user!=null&&user.TryGetValue("user",out who))name=Text(who as IDictionary<string,object>,"username");}
                    return name??"Discord";
                }
                if(Text(message,"evt")=="ERROR"){object data;message.TryGetValue("data",out data);throw new InvalidOperationException(Text(data as IDictionary<string,object>,"message")??"Discord refused the connection.");}
            }
            throw new InvalidDataException("Discord did not say it was ready.");
        }
        void Send(Stream pipe,Dictionary<string,object> activity)
        {
            var args=new Dictionary<string,object>{{"pid",System.Diagnostics.Process.GetCurrentProcess().Id},{"activity",activity}};
            var command=new Dictionary<string,object>{{"cmd","SET_ACTIVITY"},{"args",args},{"nonce",Guid.NewGuid().ToString()}};
            Write(pipe,1,new JavaScriptSerializer().Serialize(command));
            // Discord answers each command; an error is its way of saying it did not like the status.
            for(int i=0;i<4;i++)
            {
                int kind;string json=Read(pipe,out kind);
                if(kind==3){Write(pipe,4,json);continue;}
                if(kind==2)throw new EndOfStreamException("Discord closed the connection.");
                var message=Parse(json);
                if(Text(message,"evt")=="ERROR"){object data;message.TryGetValue("data",out data);error=Text(data as IDictionary<string,object>,"message")??"Discord refused the status.";return;}
                error="";return;
            }
        }

        void Loop()
        {
            NamedPipeClientStream pipe=null;string user=null;
            DateTime nextTry=DateTime.MinValue,lastSend=DateTime.MinValue;
            try
            {
                while(!stop)
                {
                    if(pipe==null)
                    {
                        if(DateTime.UtcNow<nextTry){wake.WaitOne(300);continue;}
                        pipe=Connect();
                        if(pipe==null){SetStatus("Discord is not running");nextTry=DateTime.UtcNow.AddMilliseconds(RetryEvery);continue;}
                        try{user=Handshake(pipe);SetStatus("Connected as "+user);lock(gate)dirty=true;}
                        catch(InvalidOperationException problem)
                        {
                            error=problem.Message;SetStatus("Discord refused the connection");
                            pipe.Dispose();pipe=null;nextTry=DateTime.UtcNow.AddSeconds(30);continue;
                        }
                        catch(Exception){pipe.Dispose();pipe=null;nextTry=DateTime.UtcNow.AddMilliseconds(RetryEvery);continue;}
                    }
                    Dictionary<string,object> activity;bool send;
                    lock(gate){send=dirty;activity=wanted;}
                    if(!send&&pipe!=null&&(DateTime.UtcNow-lastSend).TotalMilliseconds>=RefreshEvery&&activity!=null)send=true;
                    if(send)
                    {
                        double wait=MinimumGap-(DateTime.UtcNow-lastSend).TotalMilliseconds;
                        if(wait>0){wake.WaitOne((int)Math.Min(wait,500));continue;}
                        lock(gate){dirty=false;activity=wanted;}
                        try{Send(pipe,activity);lastSend=DateTime.UtcNow;}
                        catch(Exception)
                        {
                            try{pipe.Dispose();}catch(Exception){}
                            pipe=null;SetStatus("Discord is not running");lock(gate)dirty=true;nextTry=DateTime.UtcNow.AddMilliseconds(RetryEvery);
                        }
                        continue;
                    }
                    wake.WaitOne(500);
                }
            }
            finally
            {
                // Leaving: the status goes away with Lucent.
                if(pipe!=null){try{Send(pipe,null);}catch(Exception){}try{pipe.Dispose();}catch(Exception){}}
                SetStatus("Disabled");
            }
        }
    }
}
