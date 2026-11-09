using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shell;
using Forms=System.Windows.Forms;
using ResolutionSwitcher;
using System.Windows.Media.Imaging;

namespace Lucent
{
    // Game profiles: the Profiles page, saved looks and automatic switching.
    internal sealed partial class Shell
    {
        FrameworkElement ProfilesPage()
        {
            var stack=Page(TabTitle("Profiles"),"Saved settings: your colors, Dark Boost, zoom, Color pop and the sound radar. Use one any time, or tie one to a game so it switches by itself.");
            var about=Section("What a profile saves","A profile is a saved set of settings. Everything works on the desktop as usual; a profile just remembers it so you can bring it back with one click.");
            about.Children.Add(Explain("Saved in a profile: your colors (red filter, dimming, vibrance and color vision with its strength), Dark Boost (on or off, mode, tint, strength, black level), the zoom level and sharpening, Color pop, and the sound radar."));
            about.Children.Add(Explain("Use applies a profile now, Update look saves what you have now into it, and for a game, Play starts it through its launcher. A profile for a game switches on while that game runs and goes back to Desktop afterwards."));
            about.Children.Add(Explain("It never starts, closes or changes a game by itself, and it leaves your display mode, crosshair and shortcuts alone."));
            stack.Children.Add(about);
            var save=Section("Save your settings","Name what you have set up right now and keep it as a profile, with or without a game.");
            var saveRow=new DockPanel {Margin=new Thickness(0,6,0,2)};
            var nameBox=new TextBox {MinHeight=36,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,10,4),MaxLength=24,ToolTip="Name this profile"};
            System.Windows.Automation.AutomationProperties.SetName(nameBox,"Profile name");
            var saveButton=Action("Save current settings",()=>{SaveCurrentAsProfile(nameBox.Text);nameBox.Text="";});
            DockPanel.SetDock(saveButton,Dock.Right);
            saveRow.Children.Add(saveButton);saveRow.Children.Add(nameBox);
            save.Children.Add(saveRow);
            stack.Children.Add(save);
            var auto=Section("Switch automatically","Lucent only changes colors. It never starts, closes or touches a game.");
            auto.Children.Add(AutoToggle("Use a game's look while it is running"));
            stack.Children.Add(auto);
            stack.Children.Add(GameLibraryCard());
            var looks=Section("Your profiles","Choose the profile to use right now. A new profile starts with the settings you have at the moment.");
            profilesList=new StackPanel();
            looks.Children.Add(profilesList);
            stack.Children.Add(looks);
            RefreshProfiles();
            return stack;
        }
        TextBlock Explain(string text)
        {
            var line=Text(text,13);
            line.Foreground=Muted;line.TextWrapping=TextWrapping.Wrap;line.Margin=new Thickness(0,8,0,0);
            return line;
        }
        // The games found on this PC, each a tile with its icon. Picking one gives it its own look, which then appears under Your looks.
        List<LibraryGame> libraryGames=new List<LibraryGame>();
        WrapPanel libraryPanel;
        TextBlock libraryNote;
        bool libraryScanned,libraryScanning;
        FrameworkElement GameLibraryCard()
        {
            var card=Section("Your games","Found on this PC from Steam, Epic, GOG, EA, Ubisoft, Battle.net, Riot and Xbox. Click a game to give it its own look, starting from the colors you have now. Play starts it through its launcher where it can, otherwise from its program file.");
            libraryNote=Text("",13);
            libraryNote.Foreground=Muted;
            card.Children.Add(libraryNote);
            libraryPanel=new WrapPanel {Margin=new Thickness(0,4,0,4)};
            card.Children.Add(libraryPanel);
            var actions=new WrapPanel();
            actions.Children.Add(Action("Scan again",()=>ScanGames(true)));
            actions.Children.Add(Action("+   Add another game",AddGame));
            card.Children.Add(actions);
            ShowLibrary();
            return card;
        }
        // Only for the pictures of the app (tests): lets a test-mode window show the games' own icons, which it leaves out otherwise.
        internal static bool ShowGameIcons;
        void ScanGames(bool force)
        {
            if(TestMode||libraryScanning||(libraryScanned&&!force))return;
            libraryScanning=true;
            libraryNote.Text="Looking for your games\u2026";
            Task.Run(()=>GameLibrary.Scan()).ContinueWith(task=>Dispatcher.BeginInvoke(new Action(()=>
            {
                libraryScanning=false;libraryScanned=true;
                libraryGames=task.Status==TaskStatus.RanToCompletion?task.Result:new List<LibraryGame>();
                ShowLibrary();
            })));
        }
        internal void SetLibrary(List<LibraryGame> games){libraryGames=games;libraryScanned=true;ShowLibrary();}
        GameProfile ProfileFor(LibraryGame game){return preferences.Profiles.FirstOrDefault(p=>string.Equals(p.Executable,game.Executable,StringComparison.OrdinalIgnoreCase));}
        void ShowLibrary()
        {
            if(libraryPanel==null)return;
            libraryPanel.Children.Clear();tilePlay.Clear();
            libraryNote.Text=libraryScanning?"Looking for your games\u2026":libraryGames.Count==0?(libraryScanned?"No games found from Steam, Epic, GOG, EA, Ubisoft, Battle.net, Riot or Xbox. Use Add another game for anything else.":""):"";
            libraryNote.Visibility=libraryNote.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
            foreach(var game in libraryGames)libraryPanel.Children.Add(LibraryTile(game));
        }
        FrameworkElement LibraryTile(LibraryGame game)
        {
            bool added=ProfileFor(game)!=null;
            var face=new Grid {Width=46,Height=46,HorizontalAlignment=HorizontalAlignment.Center};
            var placeholder=Design.Icon("Profiles",26);
            placeholder.Opacity=.5;
            placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;
            var picture=new Image {Stretch=Stretch.Uniform};
            RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.HighQuality);
            face.Children.Add(placeholder);
            face.Children.Add(picture);
            if(!TestMode||ShowGameIcons)GameLibrary.LoadIcon(game.Executable,Dispatcher,source=>{picture.Source=source;placeholder.Visibility=Visibility.Collapsed;});
            var content=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};
            content.Children.Add(face);
            content.Children.Add(new TextBlock {Text=game.Name,FontSize=12,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=104,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)});
            content.Children.Add(new TextBlock {Text=added?"\u2713  Has a look":game.Store,FontSize=11,Foreground=added?Brushes.White:Muted,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,0)});
            // The top of the tile gives the game its own look; the button under it starts the game.
            var look=Action("",()=>AddLibraryGame(game));
            look.Content=content;
            // No glow of its own: the whole tile lights up as one piece (below), apart from the Play button.
            look.Style=(Style)Resources["PlainAction"];look.HorizontalContentAlignment=HorizontalAlignment.Center;
            look.Padding=new Thickness(6,10,6,6);look.Margin=new Thickness(0);look.Width=124;
            look.Tag=game.Name;
            System.Windows.Automation.AutomationProperties.SetName(look,game.Name);
            var play=Action("\u25B6  Play",()=>PlayGame(game.Launch,game.Executable,game.Name));
            tilePlay.Add(new PlayButton {Button=play,Executable=game.Executable});ShowPlayState(tilePlay[tilePlay.Count-1]);
            play.Margin=new Thickness(8,0,8,8);play.Padding=new Thickness(0,7,0,7);play.MinHeight=0;play.FontSize=12;play.HorizontalContentAlignment=HorizontalAlignment.Center;play.HorizontalAlignment=HorizontalAlignment.Stretch;
            System.Windows.Automation.AutomationProperties.SetName(play,"Play "+game.Name);
            var wash=new Border {Background=new SolidColorBrush(Color.FromArgb(24,255,255,255)),CornerRadius=new CornerRadius(12),Opacity=0,IsHitTestVisible=false};
            var column=new StackPanel();
            column.Children.Add(look);column.Children.Add(play);
            var tile=new Grid();
            tile.Children.Add(wash);tile.Children.Add(column);
            var card=new Border {Width=124,Margin=new Thickness(0,0,8,8),CornerRadius=new CornerRadius(12),Background=added?selectionSurface:optionSurface,Child=tile,Tag=game.Name};
            // The glow covers the whole tile except the Play button; over Play only Play glows.
            Action<bool> light=on=>Design.Animate(wash,OpacityProperty,wash.Opacity,on?1:0,on?100:170);
            card.MouseEnter+=(s,e)=>{if(!play.IsMouseOver)light(true);};
            card.MouseLeave+=(s,e)=>light(false);
            play.MouseEnter+=(s,e)=>light(false);
            play.MouseLeave+=(s,e)=>{if(card.IsMouseOver)light(true);};
            return card;
        }
        internal string lastLaunch;
        // Starts a game the way its launcher would. Lucent never closes or changes a game that is running.
        void PlayGame(string launch,string executable,string name)
        {
            if(TestMode){lastLaunch=string.IsNullOrEmpty(launch)?executable:launch;SetStatus("Starting "+name);MarkStarting(executable);return;}
            string problem=GameLibrary.Start(launch,executable);
            SetStatus(problem??"Starting "+name+"\u2026");
            if(problem==null)MarkStarting(executable);
        }
        // ---- What the Play buttons say: Play, then Starting while the launcher gets going, then Playing while the game is running. The last two are green.
        sealed class PlayButton{public Button Button;public string Executable;}
        readonly List<PlayButton> tilePlay=new List<PlayButton>(),rowPlay=new List<PlayButton>();
        readonly Dictionary<string,DateTime> startingUntil=new Dictionary<string,DateTime>();
        HashSet<string> runningGames=new HashSet<string>();
        // Tests replace this so no process is looked at.
        internal Func<string,bool> IsGameRunning=executable=>IsRunning(executable);
        static readonly SolidColorBrush PlayGreen=new SolidColorBrush(Color.FromRgb(36,138,76));
        static string GameKey(string executable){return (executable??"").ToLowerInvariant();}
        void MarkStarting(string executable)
        {
            Count(c=>c.GamesStarted++);
            startingUntil[GameKey(executable)]=DateTime.UtcNow.AddSeconds(25);
            ShowPlayStates();
        }
        void ShowPlayStates(){foreach(var entry in tilePlay.Concat(rowPlay).ToArray())ShowPlayState(entry);}
        void ShowPlayState(PlayButton entry)
        {
            string key=GameKey(entry.Executable);DateTime until;
            bool running=runningGames.Contains(key),starting=!running&&startingUntil.TryGetValue(key,out until)&&DateTime.UtcNow<until;
            var button=entry.Button;
            if(running||starting)
            {
                button.Content=running?"\u25CF  Playing":"Starting\u2026";
                button.Background=PlayGreen;button.Foreground=Brushes.White;
            }
            else
            {
                button.Content="\u25B6  Play";
                button.ClearValue(Button.BackgroundProperty);button.ClearValue(Button.ForegroundProperty);
            }
        }
        // Looks at which of the listed games are running, off the UI thread, and updates the buttons.
        internal async Task RefreshPlayStates()
        {
            if(closing)return;
            var games=tilePlay.Concat(rowPlay).Select(p=>p.Executable).Where(e=>!string.IsNullOrEmpty(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(games.Count==0)return;
            var check=IsGameRunning;
            var found=await Task.Run(()=>games.Where(e=>check(e)).Select(GameKey).ToList());
            if(closing)return;
            runningGames=new HashSet<string>(found);
            foreach(string key in found)startingUntil.Remove(key);
            ShowPlayStates();
        }
        void AddLibraryGame(LibraryGame game)
        {
            if(ProfileFor(game)!=null){SetStatus(game.Name+" already has its own look. Change it under Your looks.");return;}
            string name=game.Name;int copy=2;
            while(preferences.Profiles.Any(p=>p.Name==name||name=="Desktop"))name=game.Name+" ("+copy++ +")";
            preferences.Profiles.Add(new GameProfile {Name=name,Executable=game.Executable,Launch=game.Launch??"",Look=current.Copy()});
            SaveSoon();
            RefreshProfiles();
            SetStatus("Added "+name+" with your current colors");
        }
        // Keeps the settings in use now as a profile that is not tied to a game (no program file, so it never switches by itself).
        // A name that is already a saved profile of this kind updates it; any other clash gets a number.
        internal void SaveCurrentAsProfile(string text)
        {
            string name=(text??"").Trim();
            if(name.Length==0)name="My profile "+(preferences.Profiles.Count(p=>string.IsNullOrEmpty(p.Executable))+1);
            var same=preferences.Profiles.FirstOrDefault(p=>string.IsNullOrEmpty(p.Executable)&&p.Name==name);
            if(same!=null){same.Look=current.Copy();SaveSoon();SetStatus("Updated "+name);RefreshProfiles();return;}
            string unique=name;int copy=2;
            while(unique=="Desktop"||preferences.Profiles.Any(p=>p.Name==unique))unique=name+" ("+copy++ +")";
            preferences.Profiles.Add(new GameProfile {Name=unique,Executable="",Look=current.Copy()});
            SaveSoon();
            RefreshProfiles();
            SetStatus("Saved "+unique);
        }
        void AddGame()
        {
            var open=new Microsoft.Win32.OpenFileDialog {Filter="Game executable|*.exe",Title="Choose game executable"};
            if(open.ShowDialog()!=true)return;
            preferences.Profiles.Add(new GameProfile {Name=Path.GetFileNameWithoutExtension(open.FileName),Executable=open.FileName,Look=current.Copy()});
            SaveSoon();
            RefreshProfiles();
        }
        void RefreshProfiles()
        {
            if(profilesList==null)return;
            profilesList.Children.Clear();rowPlay.Clear();
            profilesList.Children.Add(ProfileRow("Desktop","Your normal look, used whenever no game is running",null));
            foreach(var saved in preferences.Profiles.ToArray())profilesList.Children.Add(ProfileRow(saved.Name,string.IsNullOrEmpty(saved.Executable)?"Saved settings, not tied to a game":Path.GetFileName(saved.Executable),saved));
            if(preferences.Profiles.Count==0)
            {
                var hint=Text("No profiles yet. Set things up on the Color and Vision tabs, then save them above, or add a game here and Lucent will use its profile whenever the game is running.",13);
                hint.Foreground=Muted;
                hint.Margin=new Thickness(2,4,0,10);
                profilesList.Children.Add(hint);
            }
            ShowLibrary();
        }
        // One look: who it is for, whether it is the one in use, and the few things you can do with it.
        FrameworkElement ProfileRow(string name,string detail,GameProfile profile)
        {
            bool active=activeProfile==name;
            Look look=profile==null?preferences.Desktop:profile.Look;
            var row=new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            var icon=Design.Icon(profile==null?"Monitor":"Profiles",20);
            icon.HorizontalAlignment=HorizontalAlignment.Center;
            icon.VerticalAlignment=VerticalAlignment.Center;
            // A game shows its own icon once it has been read from the program's file.
            var iconFace=new Grid();
            iconFace.Children.Add(icon);
            if(profile!=null&&!TestMode&&!string.IsNullOrEmpty(profile.Executable))
            {
                var gameIcon=new Image {Width=28,Height=28,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                RenderOptions.SetBitmapScalingMode(gameIcon,BitmapScalingMode.HighQuality);
                iconFace.Children.Add(gameIcon);
                GameLibrary.LoadIcon(profile.Executable,Dispatcher,source=>{gameIcon.Source=source;icon.Visibility=Visibility.Collapsed;});
            }
            row.Children.Add(new Border {Width=40,Height=40,CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(Color.FromArgb((byte)(active?46:22),255,255,255)),Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Center,Child=iconFace});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};
            labels.Children.Add(new TextBlock {Text=name,FontSize=15,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});
            var sub=new TextBlock {Text=detail,FontSize=12,Foreground=Muted,TextTrimming=TextTrimming.CharacterEllipsis};
            if(profile!=null)sub.ToolTip=profile.Executable;
            labels.Children.Add(sub);
            Grid.SetColumn(labels,1);
            row.Children.Add(labels);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(actions,2);
            row.Children.Add(actions);
            if(active)
                // Centered on the row whatever else sits beside it; stretching it to the height of the buttons left its text at the top.
                actions.Children.Add(new Border {CornerRadius=new CornerRadius(9),Background=Brushes.White,Padding=new Thickness(12,0,12,0),Height=30,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock {Text="In use",FontSize=12,FontWeight=FontWeights.Bold,Foreground=Brushes.Black,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});
            else
            {
                var use=Action("Use",()=>LoadLook(look,name));
                use.Padding=new Thickness(16,7,16,7);use.MinHeight=0;use.Margin=new Thickness(0,0,8,0);
                actions.Children.Add(use);
            }
            if(profile!=null)
            {
                if(!string.IsNullOrEmpty(profile.Executable))
                {
                    var playRow=Action("\u25B6  Play",()=>PlayGame(profile.Launch,profile.Executable,profile.Name));
                    rowPlay.Add(new PlayButton {Button=playRow,Executable=profile.Executable});ShowPlayState(rowPlay[rowPlay.Count-1]);
                    playRow.Padding=new Thickness(14,7,14,7);playRow.MinHeight=0;playRow.Margin=new Thickness(0,0,6,0);
                    System.Windows.Automation.AutomationProperties.SetName(playRow,"Play "+profile.Name);
                    actions.Children.Add(playRow);
                }
                var update=Action("Update look",()=>
                {
                    profile.Look=current.Copy();
                    SaveSoon();
                    SetStatus("Saved the current colors for "+profile.Name);
                });
                update.Padding=new Thickness(14,7,14,7);update.MinHeight=0;update.Margin=new Thickness(0,0,6,0);
                actions.Children.Add(update);
                var remove=Action("",()=>
                {
                    preferences.Profiles.Remove(profile);
                    SaveSoon();
                    if(activeProfile==profile.Name)LoadLook(preferences.Desktop,"Desktop");
                    else RefreshProfiles();
                });
                remove.Style=(Style)Resources["TrashAction"];
                remove.Content=Design.Icon("Trash",16);
                remove.Width=36;remove.Height=32;remove.MinHeight=0;remove.Padding=new Thickness(0);remove.Margin=new Thickness(0);
                remove.HorizontalContentAlignment=HorizontalAlignment.Center;
                remove.ToolTip="Remove "+profile.Name;
                System.Windows.Automation.AutomationProperties.SetName(remove,"Remove "+profile.Name);
                actions.Children.Add(remove);
            }
            return new Border {Background=active?selectionSurface:optionSurface,CornerRadius=new CornerRadius(14),Padding=new Thickness(14,12,12,12),Margin=new Thickness(0,0,0,8),Child=row};
        }
        string lastAutoProfile;
        // Auto-switch only reacts when a game starts or exits, so a profile picked by hand stays put in between.
        async void PollProfiles()
        {
            if(!preferences.AutoProfiles||profilePolling)return;
            profilePolling=true;
            try
            {
                var profiles=preferences.Profiles.ToArray();
                var match=await Task.Run(()=>FindRunningProfile(profiles));
                if(closing)return;
                string running=match==null?null:match.Executable;
                if(running==lastAutoProfile)return;
                lastAutoProfile=running;
                string name=match==null?"Desktop":match.Name;
                if(name!=activeProfile)LoadLook(match==null?preferences.Desktop:match.Look,name);
            }
            finally{profilePolling=false;}
        }
        internal static GameProfile FindRunningProfile(IEnumerable<GameProfile> profiles)
        {
            // A saved profile that is not tied to a game has no program and never matches.
            return profiles.FirstOrDefault(profile=>!string.IsNullOrEmpty(profile.Executable)&&IsRunning(profile.Executable));
        }
        static bool IsRunning(string executable)
        {
            Process[] processes;
            try{processes=Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));}
            catch(ArgumentException){return false;}
            try
            {
                // Never read a game's modules: anti-cheat blocks it, and it would touch the protected process.
                // The image path needs only limited rights. If even that is denied, the name match stands.
                return processes.Any(process=>
                {
                    string path=ProcessIdentity.ImagePath(process.Id);
                    return path==null||string.Equals(path,executable,StringComparison.OrdinalIgnoreCase);
                });
            }
            finally{foreach(var process in processes)process.Dispose();}
        }
        void LoadLook(Look value,string name)
        {
            if(name!="Desktop"&&name!=activeProfile)Count(c=>c.ProfileSwitches++);
            current=value.Copy();
            if(current.DarkEnabled&&!preferences.RiskAccepted)current.DarkEnabled=false;
            activeProfile=name;
            building=true;
            foreach(var entry in sliders)
            {
                entry.Value.Value=LookValue(entry.Key);
            }
            building=false;
            darkBoostToggle.IsChecked=current.DarkEnabled;
            if(radarToggle!=null)radarToggle.IsChecked=current.RadarOn;
            UpdateLookChoices();
            applyLook=true;
            UpdateEffects();
            RefreshProfiles();
            UpdateHome();
            SetStatus("Loaded "+name+" profile");
        }
        readonly List<Button> visionChoices=new List<Button>(),darkColorChoices=new List<Button>();
        // The strength of the color blindness aid shows only while one of the aids is chosen.
        StackPanel visionStrengthBox;
        void UpdateLookChoices()
        {
            if(visionStrengthBox!=null)visionStrengthBox.Visibility=StateStore.CanonicalVision(current.Vision)=="Normal"?Visibility.Collapsed:Visibility.Visible;
            foreach(var chip in visionChoices)StyleChoice(chip,Equals(chip.Tag,current.Vision));
            foreach(var chip in darkColorChoices)StyleChoice(chip,Equals(chip.Tag,current.DarkColor));
            foreach(var chip in popChoices)StyleChoice(chip,Equals(chip.Tag,current.PopColor));
            foreach(var chip in zoomSharpChoices)StyleChoice(chip,Equals(chip.Tag,current.ZoomSharp));
            if(zoomManualBox!=null)zoomManualBox.Visibility=current.ZoomSharp=="Manual"?Visibility.Visible:Visibility.Collapsed;
            foreach(var chip in darkModeChoices)StyleChoice(chip,Equals(chip.Tag,current.DarkSmart?"Smart shadows":"Fast"));
            foreach(var chip in radarSizeChoices)StyleChoice(chip,Equals(chip.Tag,current.RadarSize));
            foreach(var chip in radarPlaceChoices)StyleChoice(chip,Equals(chip.Tag,current.RadarPlace));
        }
        double LookValue(string key)
        {
            switch(key)
            {
                case "Vibrance":return current.Saturation;
                case "Red Filter":return current.Red;
                case "Screen Dim":return current.Dim;
                case "Strength":return current.DarkStrength;
                case "Vision strength":return current.VisionStrength;
                case "Black level":return current.BlackLevel;
                case "Zoom level":return current.Zoom;
                case "Sharpness":return current.ZoomSharpness;
                case "Pop strength":return current.PopStrength;
                case "Radar sensitivity":return current.RadarSensitivity;
                default:return 0;
            }
        }
    }
}
