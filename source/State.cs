using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Lucent
{
    public class Look
    {
        public const double MinimumVibrance = 100;
        public const double MaximumVibrance = 300;
        // Zoom never goes below this, so it is always plain that zoom is on.
        public const double MinimumZoom = 1.25,MaximumZoom = 4;
        private double saturation = MinimumVibrance;

        // Keep the XML element name used by existing desktop and game profiles.
        public double Saturation
        {
            get { return saturation; }
            set { saturation = double.IsNaN(value) ? MinimumVibrance : Math.Max(MinimumVibrance, Math.Min(MaximumVibrance, value)); }
        }

        public double Red=0,Dim=0,DarkStrength=0,BlackLevel=0,Zoom=2,ZoomSharpness=30,PopStrength=60,RadarSensitivity=60;
        // How much of the color blindness aid is applied: 100 is the full aid, 0 none (profiles saved before this existed have the full aid).
        public double VisionStrength=100;
        public string Vision="Normal",DarkColor="Green",PopColor="Off",RadarSize="Medium",RadarPlace="Center";
        // How zoom is sharpened: Adaptive (follows how far it is zoomed and leaves noise and strong edges alone), Manual (the Sharpness slider) or Off.
        public string ZoomSharp="Adaptive";
        // Smart shadows lift the dark parts per pixel on the GPU instead of brightening everything through the display. ZoomFollow magnifies around the pointer.
        public bool DarkEnabled=false,DarkSmart=false,ZoomFollow=false,RadarOn=false;
        public Look Copy(){return (Look)MemberwiseClone();}
    }
    // A few counts about using Lucent, shown in Settings, Stats. They stay in the settings file on this PC and nothing reads them but Lucent.
    public class UsageStats
    {
        public long Launches,MinutesOpen,ResolutionSwitches,ZoomUses,GamesStarted,ProfileSwitches,MemoryCleanups,CrosshairUses;
        // The day the counting started, as yyyy-MM-dd.
        public string Since="";
        // "45 min", "3 h 12 min", "2 d 5 h".
        public static string Duration(long minutes)
        {
            minutes=Math.Max(0,minutes);
            if(minutes<60)return minutes+" min";
            if(minutes<60*24)return (minutes/60)+" h "+(minutes%60)+" min";
            return (minutes/(60*24))+" d "+((minutes/60)%24)+" h";
        }
    }
    public class GameProfile
    {
        // Launch is how the store starts the game (a steam:// or Epic address). Empty for a game added by hand, which is started from its program file.
        public string Name="New profile",Executable="",Launch="";
        public Look Look=new Look();
    }
    public class Preferences
    {
        public Look Desktop=new Look();
        public List<GameProfile> Profiles=new List<GameProfile>();
        public List<string> Favorites=new List<string>();
        public bool AutoProfiles=false,Motion=true,Artwork=true,MinimizeToTray=false,RiskAccepted=false,HoldMouse5Zoom=false,BreakReminders=false;
        public int SwitchKey=0,CrosshairKey=0,ZoomKey=0,MediaKey=0,PlayKey=0,NextKey=0,PreviousKey=0,DarkKey=0;
        public string BackgroundFile="";
        public List<string> CustomBackgrounds=new List<string>();
        public List<string> BoostExclusions=new List<string>();
        public bool PlayerAlwaysOnTop=true,PlayerBarOpen=false,ShowLyrics=false,ArtistPictures=false;
        public string PlayerPosition="Middle";
        public UsageStats Stats=new UsageStats();
        // Drifting artwork colors behind the app while something plays.
        public bool AnimatedBackground=true;
        // Turns off what is only for show (moving backgrounds, GIF motion) to use less GPU, CPU and memory.
        public bool PerformanceMode=false;
        // The bottom player bar is see-through glass that blurs the page behind it; off, it is a plain colored bar.
        public bool Glass=true;
        // The circles shown on Home, in this order, from: CPU, CPU temp, Memory, GPU, GPU temp, Video memory.
        public string PcRings="CPU,CPU temp,RAM,GPU,GPU temp";
        // Temperatures on Home in Fahrenheit instead of Celsius.
        public bool Fahrenheit=false;
        // How Home is laid out: its cards in order, each "name:h" (half width) or "name:f" (full width), with a "!" in front when hidden. Empty means the standard layout.
        public string HomeLayout="";
        // The mini player: lyrics text size and alignment, which views hide their controls while the pointer rests, how fast its background drifts,
        // and the size each view was last given (0 means the default).
        public double MiniLyricsSize=26,MiniBackdropSpeed=1,MiniCompactWidth,MiniLyricsWidth,MiniLyricsHeight;
        public bool MiniCenterLyrics=true,MiniHideCompact,MiniHideLyrics;
        public int MinimizeBehaviorVersion;
        // Closing the window keeps Lucent running in the tray. On unless the user turns it off. MinimizeToTray above is no longer used.
        public bool CloseToTray=true,TrayHintShown=false,CheckUpdates=true;
        // Where new versions are announced ("owner/repository" or an https address) and the last version the user was told about.
        public string UpdateSource="",NotifiedVersion="",PopupVersion="";
        // Discord Rich Presence (off until switched on): shown only while a game from a game profile is running; the elapsed time and the Get Lucent button.
        public bool DiscordOn=true,DiscordTimeline=true,DiscordButton=true,DiscordIdle=true,DiscordSongs=true,DiscordCompeting=true;
        // Discord Rich Presence is on by default. Settings saved before this was decided are turned on once (version 1); after that the switch in Settings is respected.
        public int DiscordSettingVersion;
        public bool AdaptiveTheme=true;
        // How strongly the artwork colors the app is one fixed amount (there is no setting for it): the colors stay calm, a little like the dark, warm look of a music app.
        public const double FixedIntensity=.3;
        public double ThemeIntensity=FixedIntensity;
        public double BackgroundOpacity=.4;
        public int PairAWidth=1920,PairAHeight=1080,PairBWidth=1440,PairBHeight=1080;
        // The crosshair is one CrosshairSettings now. The old shape, size and color fields are read once to migrate older files.
        public CrosshairSettings Crosshair;
        public List<CrosshairSettings> SavedCrosshairs=new List<CrosshairSettings>();
        public string CrosshairShape="";
        public double CrosshairSize=.6;
        public int ZoomHoldKey=6,BreakMinutes=20;
        // The one zoom bind (a key or a mouse button). HoldMouse5Zoom now means the zoom mode is Hold instead of Toggle.
        // Older versions kept a separate toggle key and hold key; the first load keeps whichever one was in use.
        public int ZoomBind=0;
        public bool ZoomBindMigrated=false;
        public string CrosshairColor="#FFFFFF";
        public bool CrosshairOutline=true;
        public string CrosshairOutlineColor="#000000";
    }
    internal static class StateStore
    {
        public static string Folder=PrepareFolder();
        // Settings live in a folder called Lucent. Earlier versions used another name, so the first run copies that folder across
        // (the old one is left alone, so an older version keeps working) and nothing the user set up is lost.
        static string PrepareFolder()
        {
            string baseFolder=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fresh=Path.Combine(baseFolder,"Lucent"),old=Path.Combine(baseFolder,LegacyNames.DataFolder);
            try{if(!Directory.Exists(fresh)&&Directory.Exists(old))CopyFolder(old,fresh);}
            catch(IOException){}catch(UnauthorizedAccessException){}
            return fresh;
        }
        internal static void CopyFolder(string from,string to)
        {
            Directory.CreateDirectory(to);
            foreach(string file in Directory.GetFiles(from))File.Copy(file,Path.Combine(to,Path.GetFileName(file)),false);
            foreach(string folder in Directory.GetDirectories(from))CopyFolder(folder,Path.Combine(to,Path.GetFileName(folder)));
        }
        public static string Config {get{return Path.Combine(Folder,"preferences.xml");}}
        // Why the last Load could not use the saved file; null when it could.
        public static string LoadProblem;
        public static Preferences Load()
        {
            LoadProblem=null;
            if(!File.Exists(Config)){var fresh=new Preferences();Normalize(fresh);return fresh;}
            var saved=TryRead(Config);if(saved!=null)return saved;
            // A damaged file must never stop the app from opening. Keep it for inspection.
            try{File.Copy(Config,Config+".damaged",true);}catch(IOException){}catch(UnauthorizedAccessException){}
            var backup=TryRead(Config+".bak");
            LoadProblem=backup!=null?"Your settings file was damaged, so the last backup was restored.":"Your settings file was damaged and no backup was usable, so defaults were loaded.";
            if(backup!=null)return backup;
            var defaults=new Preferences();Normalize(defaults);return defaults;
        }
        static Preferences TryRead(string path)
        {
            if(!File.Exists(path))return null;
            try
            {
                using(var stream=File.OpenRead(path))
                {
                    var value=(Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                    Normalize(value);return value;
                }
            }
            catch(InvalidOperationException){return null;}
            catch(IOException){return null;}
            catch(UnauthorizedAccessException){return null;}
        }
        // Everything read from disk is untrusted: fill gaps and pull values back into range.
        internal static void Normalize(Preferences value)
        {
            if(value.MinimizeBehaviorVersion<1){value.MinimizeToTray=false;value.MinimizeBehaviorVersion=1;}
            if(value.Desktop==null)value.Desktop=new Look();
            NormalizeLook(value.Desktop);
            if(value.Profiles==null)value.Profiles=new List<GameProfile>();
            value.Profiles.RemoveAll(profile=>profile==null);
            foreach(var profile in value.Profiles){if(profile.Look==null)profile.Look=new Look();NormalizeLook(profile.Look);}
            if(value.Favorites==null)value.Favorites=new List<string>();
            if(value.CustomBackgrounds==null)value.CustomBackgrounds=new List<string>();
            if(value.BoostExclusions==null)value.BoostExclusions=new List<string>();
            // Removed built-in backgrounds were stored with this prefix.
            value.CustomBackgrounds.RemoveAll(file=>file.StartsWith("library:",StringComparison.Ordinal));
            if(value.BackgroundFile==null||value.BackgroundFile.StartsWith("library:",StringComparison.Ordinal))value.BackgroundFile="";
            value.ThemeIntensity=Preferences.FixedIntensity;
            value.BackgroundOpacity=Clamp(value.BackgroundOpacity,.05,1,.4);
            value.CrosshairSize=Clamp(value.CrosshairSize,.1,4,.6);
            if(value.Crosshair==null)value.Crosshair=CrosshairPresets.FromLegacy(value.CrosshairShape,value.CrosshairSize,value.CrosshairColor,value.CrosshairOutline,value.CrosshairOutlineColor);
            value.Crosshair.Clamp();
            if(value.SavedCrosshairs==null)value.SavedCrosshairs=new List<CrosshairSettings>();
            value.SavedCrosshairs.RemoveAll(saved=>saved==null);
            if(value.SavedCrosshairs.Count>24)value.SavedCrosshairs.RemoveRange(24,value.SavedCrosshairs.Count-24);
            foreach(var saved in value.SavedCrosshairs)saved.Clamp();
            if(value.PlayerPosition!="Left"&&value.PlayerPosition!="Right")value.PlayerPosition="Middle";
            value.PcRings=SystemStats.CleanRings(value.PcRings);
            value.HomeLayout=HomeLayoutText.Clean(value.HomeLayout);
            if(value.DiscordSettingVersion<1){value.DiscordOn=true;value.DiscordSettingVersion=1;}
            if(value.Stats==null)value.Stats=new UsageStats();
            var counts=value.Stats;counts.Launches=Math.Max(0,counts.Launches);counts.MinutesOpen=Math.Max(0,counts.MinutesOpen);counts.ResolutionSwitches=Math.Max(0,counts.ResolutionSwitches);counts.ZoomUses=Math.Max(0,counts.ZoomUses);counts.GamesStarted=Math.Max(0,counts.GamesStarted);counts.ProfileSwitches=Math.Max(0,counts.ProfileSwitches);counts.MemoryCleanups=Math.Max(0,counts.MemoryCleanups);counts.CrosshairUses=Math.Max(0,counts.CrosshairUses);
            if(string.IsNullOrEmpty(counts.Since))counts.Since=DateTime.Now.ToString("yyyy-MM-dd");
            if(!value.ZoomBindMigrated){value.ZoomBind=value.HoldMouse5Zoom?value.ZoomHoldKey:value.ZoomKey;value.ZoomBindMigrated=true;}
            value.MiniLyricsSize=Clamp(value.MiniLyricsSize,14,40,26);value.MiniBackdropSpeed=Clamp(value.MiniBackdropSpeed,0,3,1);
            value.MiniCompactWidth=value.MiniCompactWidth>=300&&value.MiniCompactWidth<=1200?value.MiniCompactWidth:0;
            value.MiniLyricsWidth=value.MiniLyricsWidth>=320&&value.MiniLyricsWidth<=1200?value.MiniLyricsWidth:0;
            value.MiniLyricsHeight=value.MiniLyricsHeight>=420&&value.MiniLyricsHeight<=1800?value.MiniLyricsHeight:0;
        }
        static void NormalizeLook(Look look)
        {
            look.Red=Clamp(look.Red,0,100,0);look.Dim=Clamp(look.Dim,0,85,0);
            look.DarkStrength=Clamp(look.DarkStrength,0,100,0);look.VisionStrength=Clamp(look.VisionStrength,0,100,100);
            look.BlackLevel=Clamp(look.BlackLevel,0,100,0);
            look.Zoom=Clamp(look.Zoom,Look.MinimumZoom,Look.MaximumZoom,2);
            look.ZoomSharpness=Clamp(look.ZoomSharpness,0,100,30);
            if(look.ZoomSharp!="Manual"&&look.ZoomSharp!="Off")look.ZoomSharp="Adaptive";
            look.ZoomFollow=false;   // zoom around the pointer was removed; zoom is always about the middle of the screen
            look.PopStrength=Clamp(look.PopStrength,0,100,60);look.RadarSensitivity=Clamp(look.RadarSensitivity,0,100,60);
            if(Array.IndexOf(new[]{"Off","Red","Orange","Yellow","Green","Cyan","Blue","Magenta"},look.PopColor)<0)look.PopColor="Off";
            if(Array.IndexOf(new[]{"Small","Medium","Large"},look.RadarSize)<0)look.RadarSize="Medium";
            if(Array.IndexOf(new[]{"Center","Top","Bottom"},look.RadarPlace)<0)look.RadarPlace="Center";
            look.Vision=CanonicalVision(look.Vision);if(look.DarkColor==null)look.DarkColor="Green";if(look.DarkColor=="Clean")look.DarkColor="White";   // "Clean" and "White" were the same thing
        }
        // Earlier versions called these Red-weak, Green-weak and Blue-weak; saved profiles still carry those names.
        internal static string CanonicalVision(string value)
        {
            switch(value)
            {
                case "Protanopia":case "Red-weak":return "Protanopia";
                case "Deuteranopia":case "Green-weak":return "Deuteranopia";
                case "Tritanopia":case "Blue-weak":return "Tritanopia";
                default:return "Normal";
            }
        }
        static double Clamp(double value,double min,double max,double fallback)
        {
            return double.IsNaN(value)||double.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value));
        }
        public static void Save(Preferences value){Write(Config,value);}
        public static void Write<T>(string path,T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".new";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))new XmlSerializer(typeof(T)).Serialize(stream,value);
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
        }
    }
}

