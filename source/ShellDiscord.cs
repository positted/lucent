using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lucent
{
    // Discord Rich Presence: "Using Lucent" on the user's own Discord profile, or "Playing <game> with Lucent" while a game from their game profiles
    // is running. Off until it is switched on in Settings; see DiscordActivity for the little that is sent.
    internal sealed partial class Shell
    {
        DiscordRpcClient discord;
        DispatcherTimer discordTimer;
        bool readingDiscord;
        string discordSentKey,discordShowing="",discordGameExe;
        DateTime discordGameSince,discordStarted;
        TextBlock discordHead,discordSub;
        // Tests point the client at a made-up Discord and supply the running game; with no factory a test never opens a real connection.
        internal Func<DiscordRpcClient> DiscordFactory;
        internal Func<GameProfile> DiscordGameProbe;
        internal Func<IEnumerable<string>> DiscordNameProbe;
        sealed class DiscordGame {public string Name,Executable,Icon;public DiscordSong Song;}
        sealed class DiscordSong {public string Title,Artist,Cover;public double Position,Duration;}
        MediaSession discordSession;
        double discordSongPosition;
        DateTime discordSongSentAt;
        // Tests supply the song that is playing; with none, a test never reads the real media controls.
        internal Func<MediaTrack> DiscordTrackProbe;
        internal DiscordRpcClient DiscordClient {get{return discord;}}
        internal string DiscordHeadText {get{return discordHead==null?null:discordHead.Text;}}
        internal string DiscordShowing {get{return discordShowing;}}

        PresenceOptions PresenceNow()
        {
            return new PresenceOptions {Timeline=preferences.DiscordTimeline,Button=preferences.DiscordButton,Idle=preferences.DiscordIdle,Songs=preferences.DiscordSongs,Competing=preferences.DiscordCompeting};
        }
        // Starts or stops the connection to match the setting.
        internal void ApplyDiscord()
        {
            if(preferences.DiscordOn&&discord==null)
            {
                if(TestMode&&DiscordFactory==null){ShowDiscordStatus();return;}
                discord=DiscordFactory!=null?DiscordFactory():new DiscordRpcClient {ClientId=DiscordActivity.ClientId};
                discord.StatusChanged=()=>{try{Dispatcher.BeginInvoke(new Action(ShowDiscordStatus));}catch(Exception){}};
                discordSentKey=null;
                discord.Start();
                if(discordTimer==null)
                {
                    discordTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};
                    discordTimer.Tick+=async(s,e)=>await PushDiscord();
                    Dispatcher.ShutdownStarted+=(s,e)=>StopDiscord();
                }
                discordTimer.Start();
                var first=PushDiscord();
            }
            else if(!preferences.DiscordOn&&discord!=null)StopDiscord();
            ShowDiscordStatus();
        }
        void StopDiscord()
        {
            if(discordTimer!=null)discordTimer.Stop();
            var client=discord;discord=null;discordSentKey=null;discordShowing="";discordGameExe=null;
            if(client!=null){client.Set(null);client.Dispose();}
            ShowDiscordStatus();
        }
        async Task PushDiscord()
        {
            if(discord==null||readingDiscord||closing)return;
            readingDiscord=true;
            try
            {
                var profiles=preferences.Profiles.ToArray();var probe=DiscordGameProbe;var namesProbe=DiscordNameProbe;
                // The song that is playing, read from Windows' media controls (nothing is sent to the app playing it).
                MediaTrack track=null;
                if(preferences.DiscordSongs)
                {
                    try{track=DiscordTrackProbe!=null?DiscordTrackProbe():(TestMode?null:await (discordSession??(discordSession=new MediaSession())).Read());}
                    catch(Exception){track=null;}
                }
                // What a web browser plays (YouTube, TikTok and the like) is not shown on the profile; only music from a music app is.
                bool playing=track!=null&&track.Playing&&!string.IsNullOrWhiteSpace(track.Title)&&!DiscordActivity.IsBrowserSource(track.Source);
                // Only the names of running programs are looked at (the way game profiles are matched, and the way Discord recognises games); a game is never opened.
                var found=await Task.Run(()=>
                {
                    var profile=probe!=null?probe():FindRunningProfile(profiles);
                    if(profile!=null)return new DiscordGame {Name=profile.Name,Executable=profile.Executable};
                    var known=DiscordGameIcons.FindRunning(namesProbe!=null?namesProbe():RunningProgramNames(),namesProbe!=null?(Func<string,string>)null:ProgramPath);
                    if(known!=null)return new DiscordGame {Name=known.Name,Icon=known.Icon};
                    // Not in Discord's list (a small or new game, like Rob Run): any program running from the folder of a game the launchers have installed
                    // (Steam, Epic, GOG, Ubisoft, EA, Battle.net, Riot, Xbox) is that game.
                    if(namesProbe==null&&probe==null)
                    {
                        var installed=InstalledGameRunning();
                        if(installed!=null)
                        {
                            var listed=DiscordGameIcons.Lookup(null,installed.Name);
                            // A name all in capitals (ROB RUN) is shown the way its folder writes it (Rob Run) when they are the same words.
                            string shown=installed.Name,leaf=System.IO.Path.GetFileName(InstalledGames.Folder(installed.Executable)??"");
                            if(shown==shown.ToUpperInvariant()&&leaf!=shown&&string.Equals(leaf,shown,StringComparison.OrdinalIgnoreCase))shown=leaf;
                            return new DiscordGame {Name=shown,Executable=installed.Executable,Icon=listed==null?null:listed.Icon};
                        }
                    }
                    // No game: the song, with its cover from Deezer's public search.
                    if(playing)return new DiscordGame {Song=new DiscordSong {Title=track.Title,Artist=track.Artist,Cover=SongCovers.Find(track.Title,track.Artist),Position=track.Position,Duration=track.Duration}};
                    return null;
                });
                if(discord!=null)SendDiscord(found,PresenceNow());
            }
            catch(Exception){}
            finally{readingDiscord=false;}
        }        // Sends the status when it differs from what Discord already shows (a game starting or ending, a changed option). No game means no status.
        // The installed games and the folders they live in, read again every ten minutes (on the Discord thread, never the window's).
        static List<KeyValuePair<string,LibraryGame>> installedFolders;static DateTime installedAt;
        static LibraryGame InstalledGameRunning()
        {
            if(installedFolders==null||DateTime.UtcNow-installedAt>TimeSpan.FromMinutes(10))
            {
                var list=new List<KeyValuePair<string,LibraryGame>>();
                try{foreach(var game in GameLibrary.Scan()){string folder=InstalledGames.Folder(game.Executable);if(folder!=null)list.Add(new KeyValuePair<string,LibraryGame>(folder,game));}}catch(Exception){}
                // The deepest folder first, so a game inside another game's folder is named for itself.
                installedFolders=list.OrderByDescending(e=>e.Key.Length).ToList();installedAt=DateTime.UtcNow;
            }
            if(installedFolders.Count==0)return null;
            return InstalledGames.Match(installedFolders,RunningProgramPaths());
        }
        static IEnumerable<string> RunningProgramPaths()
        {
            System.Diagnostics.Process[] all;
            try{all=System.Diagnostics.Process.GetProcesses();}catch(Exception){yield break;}
            try
            {
                foreach(var process in all.OrderByDescending(p=>{try{return p.Id;}catch(Exception){return 0;}}))
                {
                    string path=null;
                    try{if(process.Id>4)path=ResolutionSwitcher.ProcessIdentity.ImagePath(process.Id);}catch(Exception){}
                    if(path!=null)yield return path;
                }
            }
            finally{foreach(var process in all)process.Dispose();}
        }
        // Where a running program is, from its name (null when Windows will not say). Only the path is read, never the program itself.
        static string ProgramPath(string name)
        {
            System.Diagnostics.Process[] found;
            try{found=System.Diagnostics.Process.GetProcessesByName(name);}catch(Exception){return null;}
            try{foreach(var process in found){string path=ResolutionSwitcher.ProcessIdentity.ImagePath(process.Id);if(path!=null)return path;}return null;}
            finally{foreach(var process in found)process.Dispose();}
        }
        static List<string> RunningProgramNames()
        {
            var found=new List<KeyValuePair<int,string>>();
            try
            {
                foreach(var process in System.Diagnostics.Process.GetProcesses())
                {
                    try{found.Add(new KeyValuePair<int,string>(process.Id,process.ProcessName));}catch(Exception){}
                    finally{process.Dispose();}
                }
            }
            catch(Exception){}
            return found.OrderBy(p=>p.Key).Select(p=>p.Value).ToList();
        }
        internal void SendDiscord(GameProfile game,PresenceOptions options)
        {
            SendDiscord(game==null?null:new DiscordGame {Name=game.Name,Executable=game.Executable},options);
        }
        // The song playing, for tests: no game, a song (with or without a cover address).
        internal void SendDiscordSong(string title,string artist,string cover,double position,double duration,PresenceOptions options)
        {
            SendDiscord(title==null?null:new DiscordGame {Song=new DiscordSong {Title=title,Artist=artist,Cover=cover,Position=position,Duration=duration}},options);
        }
        void SendDiscord(DiscordGame game,PresenceOptions options)
        {
            var client=discord;if(client==null)return;
            var song=game==null?null:game.Song;
            if(song!=null)game=null;
            string exe=game==null?null:(game.Executable??game.Name);
            if(exe!=discordGameExe){discordGameExe=exe;discordGameSince=DateTime.UtcNow;}
            if(discordStarted==default(DateTime))
            {
                try{discordStarted=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();}catch(Exception){discordStarted=DateTime.UtcNow;}
            }
            var info=new PresenceInfo {Game=game==null?null:game.Name,Since=game==null?discordStarted:discordGameSince,GameIcon=game==null?null:(game.Icon??(DiscordGameIcons.Lookup(game.Executable,game.Name)??new KnownGame()).Icon)};
            if(song!=null){info.Title=song.Title;info.Artist=song.Artist;info.CoverUrl=song.Cover;info.Position=song.Position;info.Duration=song.Duration;}
            var activity=DiscordActivity.Build(info,options,DateTime.UtcNow);
            bool listening=activity!=null&&info.Game==null&&info.Title!=null&&options.Songs;
            string key=(info.Game??"")+"|"+(info.GameIcon??"")+"|"+options.Timeline+options.Button+options.Idle+options.Songs+"|"+(listening?info.Title+"|"+info.Artist+"|"+info.CoverUrl:"");
            // A song moves on by itself; the status is only sent again if the song changed or was skipped through, not as it plays on.
            if(listening&&key==discordSentKey)
            {
                double expected=discordSongPosition+(DateTime.UtcNow-discordSongSentAt).TotalSeconds;
                if(Math.Abs(info.Position-expected)<=3.5)return;
            }
            else if(key==discordSentKey)return;
            discordSentKey=key;discordSongPosition=info.Position;discordSongSentAt=DateTime.UtcNow;
            discordShowing=activity==null?"":(info.Game!=null?"Playing "+info.Game+" with Lucent":(listening?"Listening to "+info.Title:"Idle"));
            client.Set(activity);
            ShowDiscordStatus();
        }        void RefreshDiscordNow(){discordSentKey=null;var again=PushDiscord();}

        void ShowDiscordStatus()
        {
            if(discordHead==null)return;
            string status=discord==null?"Disabled":discord.Status;
            bool connected=discord!=null&&status.StartsWith("Connected");
            discordHead.Text=connected?"Discord connected":"Discord not connected";
            if(!preferences.DiscordOn)discordSub.Text="Disabled";
            else if(connected)discordSub.Text=discordShowing.Length>0?"Showing: "+discordShowing:"Connected. Waiting for a game to start.";
            else if(status=="Needs setup")discordSub.Text="Discord status is not set up in this build yet.";
            else discordSub.Text=status+(discord!=null&&!string.IsNullOrEmpty(discord.Error)?" ("+discord.Error+")":"");
        }

        void BuildDiscordSection(SectionPanel page)
        {
            // The status card: whether Lucent is talking to Discord, and what it is showing.
            var card=new DockPanel {Margin=new Thickness(2,2,0,2)};
            var iconBox=new Border {Width=64,Height=64,CornerRadius=new CornerRadius(15),Background=new SolidColorBrush(Color.FromRgb(46,46,48)),Margin=new Thickness(0,0,18,0),VerticalAlignment=VerticalAlignment.Center,
                Child=new ContentControl {Content=Design.SettingsIcon("Discord RPC",30,new SolidColorBrush(Color.FromRgb(196,196,200))),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Focusable=false}};
            DockPanel.SetDock(iconBox,Dock.Left);card.Children.Add(iconBox);
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            discordHead=new TextBlock {Text="Discord not connected",FontSize=18,FontWeight=FontWeights.Bold,Foreground=Brushes.White};
            discordSub=new TextBlock {Text="Disabled",FontSize=13,Foreground=Muted,Margin=new Thickness(0,3,0,0),TextWrapping=TextWrapping.Wrap};
            words.Children.Add(discordHead);words.Children.Add(discordSub);card.Children.Add(words);
            var cardBox=new Border {Background=settingsGroupSurface,CornerRadius=new CornerRadius(15),Padding=new Thickness(16),Margin=new Thickness(0,2,0,16),Child=card};
            page.Children.Add(cardBox);
            var master=new StackPanel();
            master.Children.Add(SettingsSwitch("Discord Rich Presence","Show on your Discord profile which game you are playing with Lucent. Lucent recognises games the way Discord does, by the name of the running program. Works with Discord, Discord PTB and Discord Canary.",preferences.DiscordOn,v=>{preferences.DiscordOn=v;ApplyDiscord();SaveSoon();}));
            page.Children.Add(SettingsGroup(master));
            page.Children.Add(new TextBlock {Text="Display options",FontSize=15,FontWeight=FontWeights.Bold,Foreground=Brushes.White,Margin=new Thickness(2,4,0,8)});
            var options=new StackPanel();
            options.Children.Add(SettingsSwitch("Show songs","When a song is playing and no game is running, show it on your profile as \u201CListening to Lucent\u201D with its cover, artist and progress. The song\u2019s title and artist go to Discord (that is what your profile shows), and the cover is found with Deezer\u2019s public search, which is sent the same two names.",preferences.DiscordSongs,v=>{preferences.DiscordSongs=v;SaveSoon();RefreshDiscordNow();}));
            options.Children.Add(SettingsSwitch("Show next to a game\u2019s own status","Shows Lucent\u2019s card as \u201CCompeting\u201D so it appears next to the game\u2019s own card. Off shows \u201CPlaying\u201D.",preferences.DiscordCompeting,v=>{preferences.DiscordCompeting=v;SaveSoon();RefreshDiscordNow();}));
            options.Children.Add(SettingsSwitch("Show Idle","While nothing is playing and no game is running, show “Idle” with the Lucent logo on your profile.",preferences.DiscordIdle,v=>{preferences.DiscordIdle=v;SaveSoon();RefreshDiscordNow();}));
            options.Children.Add(SettingsSwitch("Elapsed time","Show how long you have been playing the game, or how long Lucent has been open while idle.",preferences.DiscordTimeline,v=>{preferences.DiscordTimeline=v;SaveSoon();RefreshDiscordNow();}));
            page.Children.Add(SettingsGroup(options));
            page.Children.Add(new TextBlock {Text="Profile button",FontSize=15,FontWeight=FontWeights.Bold,Foreground=Brushes.White,Margin=new Thickness(2,4,0,8)});
            var buttons=new StackPanel();
            buttons.Children.Add(SettingsSwitch("\u201CGet Lucent\u201D button","Add a button to your status that opens Lucent\u2019s download page.",preferences.DiscordButton,v=>{preferences.DiscordButton=v;SaveSoon();RefreshDiscordNow();}));
            page.Children.Add(SettingsGroup(buttons));
            page.Children.Add(new TextBlock {Text="Nothing is shown on Discord unless a game is running (like \u201CPlaying Rust with Lucent\u201D), a song is playing (\u201CListening to Lucent\u201D) or, if Show Idle is on, nothing is happening (\u201CIdle\u201D). Lucent only checks the names of running programs to know a game is open, and never opens the game.",FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,0,0,10)});
            ShowDiscordStatus();
            if(preferences.DiscordOn)ApplyDiscord();
        }
    }
    // Recognising a game Discord does not list: a program running from the folder of a game the launchers installed.
    internal static class InstalledGames
    {
        static readonly System.Text.RegularExpressions.Regex NotTheGame=new System.Text.RegularExpressions.Regex(@"(unins|crash|report|redist|setup|installer|updater|prereq|easyanticheat|battleye|eac|launcher_helper|cefprocess|webhelper)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // The folder a game is installed in: for Steam the game's own folder under steamapps\common (its program may sit deeper, like Binaries\Win64), otherwise the program's folder.
        internal static string Folder(string executable)
        {
            if(string.IsNullOrEmpty(executable))return null;
            string full;try{full=System.IO.Path.GetFullPath(executable);}catch(Exception){return null;}
            const string common=@"\steamapps\common\";
            int at=full.IndexOf(common,StringComparison.OrdinalIgnoreCase);
            if(at>=0){int end=full.IndexOf('\\',at+common.Length);return end<0?null:full.Substring(0,end);}
            string folder=System.IO.Path.GetDirectoryName(full);
            // A program straight in a drive or in Program Files is not a game's own folder.
            if(folder==null||folder.TrimEnd('\\').Count(c=>c=='\\')<2)return null;
            foreach(var shared in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.Windows)})
                if(!string.IsNullOrEmpty(shared)&&string.Equals(folder.TrimEnd('\\'),shared.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return null;
            return folder;
        }
        // Which installed game has a program running from its folder (the first one found), or null.
        internal static LibraryGame Match(IEnumerable<KeyValuePair<string,LibraryGame>> folders,IEnumerable<string> runningPaths)
        {
            foreach(string path in runningPaths)
            {
                if(string.IsNullOrEmpty(path)||NotTheGame.IsMatch(System.IO.Path.GetFileName(path)))continue;
                foreach(var entry in folders)
                    if(path.StartsWith(entry.Key+"\\",StringComparison.OrdinalIgnoreCase))return entry.Value;
            }
            return null;
        }
    }
}
