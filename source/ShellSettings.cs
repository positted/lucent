using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using ResolutionSwitcher;
namespace Lucent
{
    internal sealed partial class Shell
    {
        SectionPanel SettingsSection(string name){var section=Section(name,"",true);settingsSections[name]=section;return section;}
        Border SettingsGroup(StackPanel content)
        {
            return new Border{Background=settingsGroupSurface,CornerRadius=new CornerRadius(15),Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,2,0,16),Child=content};
        }
        FrameworkElement SettingsRow(string label,string description,FrameworkElement control=null)
        {
            var grid=new Grid {MinHeight=58,Margin=new Thickness(0,4,0,4)};
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var copy=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,18,4)};
            copy.Children.Add(new TextBlock{Text=label,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap});
            if(!string.IsNullOrEmpty(description))copy.Children.Add(new TextBlock{Text=description,FontSize=13,FontWeight=FontWeights.Medium,Foreground=new SolidColorBrush(Color.FromRgb(137,137,140)),Margin=new Thickness(0,4,0,0),TextWrapping=TextWrapping.Wrap,LineHeight=18});
            grid.Children.Add(copy);
            if(control!=null)
            {
                control.VerticalAlignment=VerticalAlignment.Center;
                Grid.SetColumn(control,1);
                grid.Children.Add(control);
            }
            return grid;
        }
        FrameworkElement SettingsSwitch(string label,string description,bool value,Action<bool> changed)
        {
            CheckBox ignored;return SettingsSwitch(label,description,value,changed,out ignored);
        }
        FrameworkElement SettingsSwitch(string label,string description,bool value,Action<bool> changed,out CheckBox created)
        {
            var toggle=Toggle("",value,changed);
            created=toggle;
            toggle.Width=50;
            toggle.Margin=new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(toggle,label);
            return SettingsRow(label,description,toggle);
        }
        void ResetMiniSizes()
        {
            if(mediaPlayer!=null){mediaPlayer.ResetSizes();return;}
            preferences.MiniCompactWidth=preferences.MiniLyricsWidth=preferences.MiniLyricsHeight=0;
            SaveSoon();
        }
        FrameworkElement PolishedSettingsPage()
        {
            var appearance=SettingsSection("Appearance");var theme=new StackPanel();
            theme.Children.Add(SettingsSwitch("Media controls","The player bar, the mini player and the player for what is playing on your PC, and the colors of the music in the app. Off, Lucent does not read what is playing at all.",preferences.MediaControls,v=>
            {
                preferences.MediaControls=v;
                ApplyMediaControls();
                SaveSoon();
            }));
            theme.Children.Add(SettingsSwitch("Adaptive colors","Blend colors from the current artwork into the interface.",preferences.AdaptiveTheme,v=>
            {
                preferences.AdaptiveTheme=v;
                UpdateAmbientColor();
                SaveSoon();
            }));
            theme.Children.Add(SettingsSwitch("Animated background","Let the colors of the artwork drift slowly behind the app while something is playing.",preferences.AnimatedBackground,v=>
            {
                preferences.AnimatedBackground=v;
                UpdateFlowBackdrop();
                SaveSoon();
            }));
            theme.Children.Add(SettingsSwitch("Glass","The player bar at the bottom is see-through and blurs the page behind it. Off, it is a plain colored bar and the page ends where it begins.",preferences.Glass,v=>
            {
                preferences.Glass=v;
                ApplyGlass();
                SaveSoon();
            }));
            theme.Children.Add(SettingsSwitch("Artist pictures","Show the artist\u2019s picture beside their name in the player and the player bar. Finding it sends the artist\u2019s name to Deezer\u2019s public search, so it stays off until you switch it on.",preferences.ArtistPictures,v=>{preferences.ArtistPictures=v;ArtistPictures.Enabled=()=>preferences.ArtistPictures;bigPlayerView.RefreshArtistPicture();playerBar.RefreshArtistPicture();SaveSoon();}));
            theme.Children.Add(SettingsSwitch("Performance mode","Turns off almost everything that is only for show: the colors of the music in the interface, the moving colors behind the app, the mini player and the player, the glass of the player bar, the fades and glides (tabs, minimizing, hovering) and the motion of GIF backgrounds. Less to draw means less graphics card, processor and memory use while you play.",preferences.PerformanceMode,v=>
            {
                preferences.PerformanceMode=v;
                UpdateFlowBackdrop();
                ApplyAppearance();
                ApplyGlass();
                bigPlayerView.RefreshPerformance();
                if(mediaPlayer!=null)mediaPlayer.RefreshFromPreferences();
                SaveSoon();
            }));
            appearance.Children.Add(SettingsGroup(theme));
            // Which circles Home shows for the PC.
            var circles=new StackPanel();
            circles.Children.Add(SettingsRow("Circles on Home","Choose which numbers about your PC Home shows."));
            foreach(string[] ring in new[]{new[]{"CPU","CPU load","How busy the processor is."},new[]{"CPU temp","CPU temperature","Windows does not share this on most PCs. Lucent\u2019s setup can add CPU temperature support, and this circle appears once Lucent can read it."},new[]{"RAM","RAM","How much of your RAM is in use."},new[]{"GPU","GPU load","How busy the graphics card is. NVIDIA cards."},new[]{"GPU temp","GPU temperature","How warm the graphics card is. NVIDIA cards."},new[]{"Video memory","Video memory","How much of the graphics card\u2019s memory is in use. NVIDIA cards."}})
            {
                string key=ring[0];
                circles.Children.Add(SettingsSwitch(ring[1],ring[2],PcRingChosen(key),v=>
                {
                    var chosenRings=new System.Collections.Generic.List<string>(preferences.PcRings.Split(new[]{","},StringSplitOptions.RemoveEmptyEntries));
                    if(v&&!chosenRings.Contains(key))chosenRings.Add(key);
                    if(!v)chosenRings.Remove(key);
                    preferences.PcRings=SystemStats.CleanRings(string.Join(",",chosenRings.ToArray()));
                    UpdatePcRings();
                    SaveSoon();
                }));
            }
            circles.Children.Add(SettingsSwitch("Temperatures in Fahrenheit","Shows F instead of C. Celsius is the default.",preferences.Fahrenheit,v=>{preferences.Fahrenheit=v;UpdatePcRings();SaveSoon();}));
            appearance.Children.Add(SettingsGroup(circles));
            var art=new StackPanel();
            artworkToggle=Toggle("",preferences.Artwork,v=>
            {
                preferences.Artwork=v;
                ApplyAppearance();
                UpdateBackgroundButtons();
                SaveSoon();
            });
            artworkToggle.Width=50;
            artworkToggle.Margin=new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(artworkToggle,"Custom background");
            art.Children.Add(SettingsRow("Custom background","Show your selected GIF behind the app.",artworkToggle));
            art.Children.Add(SettingsSwitch("Background motion","Animate the GIF while the app is active.",preferences.Motion,v=>
            {
                preferences.Motion=v;
                ApplyAppearance();
                SaveSoon();
            }));
            appearance.Children.Add(SettingsGroup(art));
            var shortcuts=SettingsSection("Shortcuts");
            BindRow(shortcuts,"Switch resolution",()=>preferences.SwitchKey,v=>preferences.SwitchKey=v,TogglePair);
            BindRow(shortcuts,"Toggle crosshair",()=>preferences.CrosshairKey,v=>preferences.CrosshairKey=v,ToggleCrosshair);
            AddZoomBind(shortcuts,"Zoom bind");
            BindRow(shortcuts,"Music mini player",()=>preferences.MediaKey,v=>preferences.MediaKey=v,MediaPopup);
            BindRow(shortcuts,"Play / pause",()=>preferences.PlayKey,v=>preferences.PlayKey=v,()=>SendMedia(0xB3));
            BindRow(shortcuts,"Next track",()=>preferences.NextKey,v=>preferences.NextKey=v,()=>SendMedia(0xB0));
            BindRow(shortcuts,"Previous track",()=>preferences.PreviousKey,v=>preferences.PreviousKey=v,()=>SendMedia(0xB1));
            ArrangeShortcutTiles(shortcuts);
            var overlay=SettingsSection("Mini Player");
            var player=new StackPanel();
            var open=Action("Open mini player",MediaPopup);
            open.Margin=new Thickness(0);
            open.Padding=new Thickness(12,7,12,7);
            player.Children.Add(SettingsRow("Music player","Show the floating miniature player above other windows.",open));
            player.Children.Add(SettingsSwitch("Always on top","Keep the miniature player above other windows.",preferences.PlayerAlwaysOnTop,v=>
            {
                preferences.PlayerAlwaysOnTop=v;
                if(mediaPlayer!=null)mediaPlayer.SetOnTop(v);
                SaveSoon();
            }));
            var positions=new StackPanel{Orientation=Orientation.Horizontal};
            foreach(string option in new[]{"Left","Middle","Right"})
            {
                string chosen=option;
                var button=Action(option,()=>
                {
                    preferences.PlayerPosition=chosen;
                    foreach(Button b in positions.Children)b.Background=Equals(b.Tag,chosen)?settingsSelectionSurface:settingsOptionSurface;
                    if(mediaPlayer!=null)mediaPlayer.PlaceAtPreferredPosition();
                    SaveSoon();
                });
                button.Tag=option;
                button.Padding=new Thickness(10,6,10,6);
                button.Margin=new Thickness(2,0,0,0);
                button.MinHeight=30;
                button.Background=preferences.PlayerPosition==option?settingsSelectionSurface:settingsOptionSurface;
                positions.Children.Add(button);
            }
            player.Children.Add(SettingsRow("Position","Choose where the player opens on your display.",positions));
            overlay.Children.Add(SettingsGroup(player));
            // How the player looks and behaves. This is the only place the mini player is set up; its dots button opens this tab.
            var feel=new StackPanel();
            var speed=new Slider{Minimum=0,Maximum=2,Value=preferences.MiniBackdropSpeed,Height=28,Margin=new Thickness(0,2,0,13),SmallChange=.05,LargeChange=.25};
            feel.Children.Add(SettingsRow("Background movement","How fast the colors of the cover drift behind the player. All the way left holds them still. They only drift while something is playing."));
            feel.Children.Add(speed);
            speed.ValueChanged+=(s,e)=>
            {
                preferences.MiniBackdropSpeed=Math.Round(speed.Value*20)/20;
                if(mediaPlayer!=null)mediaPlayer.RefreshFromPreferences();
                SaveSoon();
            };
            overlay.Children.Add(SettingsGroup(feel));
            var lyricsLook=new StackPanel();
            lyricsLook.Children.Add(SettingsSwitch("Center lyrics","Center each lyric line instead of lining them up on the left.",preferences.MiniCenterLyrics,v=>
            {
                preferences.MiniCenterLyrics=v;
                if(mediaPlayer!=null)mediaPlayer.RefreshFromPreferences();
                SaveSoon();
            }));
            var lyricSize=new Slider{Minimum=14,Maximum=40,Value=preferences.MiniLyricsSize,Height=28,Margin=new Thickness(0,2,0,13),SmallChange=1,LargeChange=4,IsSnapToTickEnabled=true,TickFrequency=1};
            lyricsLook.Children.Add(SettingsRow("Lyrics size","The size of the lyric lines. Lyrics are looked up on lrclib.net, and only while lyrics are switched on in the player."));
            lyricsLook.Children.Add(lyricSize);
            lyricSize.ValueChanged+=(s,e)=>
            {
                preferences.MiniLyricsSize=lyricSize.Value;
                if(mediaPlayer!=null)mediaPlayer.RefreshFromPreferences();
                SaveSoon();
            };
            overlay.Children.Add(SettingsGroup(lyricsLook));
            var idleLook=new StackPanel();
            idleLook.Children.Add(SettingsSwitch("Hide the controls of the player","After a few seconds without the mouse moving, the progress bar and buttons fade and the window shrinks to the cover and the title.",preferences.MiniHideCompact,v=>{preferences.MiniHideCompact=v;SaveSoon();}));
            idleLook.Children.Add(SettingsSwitch("Hide the controls with lyrics","The same, while the lyrics are showing.",preferences.MiniHideLyrics,v=>{preferences.MiniHideLyrics=v;SaveSoon();}));
            overlay.Children.Add(SettingsGroup(idleLook));
            var sizes=new StackPanel();
            var resetSizes=Action("Reset",ResetMiniSizes);
            resetSizes.Margin=new Thickness(0);
            resetSizes.Padding=new Thickness(12,7,12,7);
            sizes.Children.Add(SettingsRow("Window sizes","Each view of the player remembers the size you give it. Reset puts them all back.",resetSizes));
            overlay.Children.Add(SettingsGroup(sizes));
            var general=SettingsSection("General");var behavior=new StackPanel();
            behavior.Children.Add(SettingsSwitch("Launch with Windows","Start lucent in the background when you sign in.",!TestMode&&DesktopTools.Startup,v=>
            {
                if(!TestMode)DesktopTools.Startup=v;
            }));
            behavior.Children.Add(SettingsSwitch("Close to tray","Closing the window keeps Lucent running in the system tray. Exit from the tray icon.",preferences.CloseToTray,v=>
            {
                preferences.CloseToTray=v;
                SaveSoon();
            }));
            general.Children.Add(SettingsGroup(behavior));
            var threshold=new Slider{Minimum=.1,Maximum=1,Value=.5,Height=28,Margin=new Thickness(0,2,0,10)};
            var sound=new StackPanel();
            CheckBox limitToggle=null;
            var limitRow=SettingsSwitch("Limit loud sounds","Lowers system volume, including voice chat. Restores it when disabled.",false,v=>
            {
                if(v)
                {
                    if(TestMode)return;
                    limiter=new AudioLimiter{Limit=(float)threshold.Value};
                    limiter.Status=message=>Dispatcher.BeginInvoke(new Action(()=>SetStatus(message)));
                    limiter.Stopped=()=>Dispatcher.BeginInvoke(new Action(()=>
                    {
                        limitToggle.IsChecked=false;
                        if(limiter!=null)
                        {
                            limiter.Dispose();
                            limiter=null;
                        }
                    }));
                    limiter.Start();
                }
                else if(limiter!=null)
                {
                    limiter.Dispose();
                    limiter=null;
                }
            },
            out limitToggle);
            sound.Children.Add(limitRow);
            sound.Children.Add(SettingsRow("Loud-sound threshold","Lower levels hold down more of the system audio."));
            sound.Children.Add(threshold);
            threshold.ValueChanged+=(s,e)=>
            {
                if(limiter!=null)limiter.Limit=(float)threshold.Value;
            };
            general.Children.Add(SettingsGroup(sound));
            var about=SettingsSection("About");
            about.Children.Add(AboutCard());
            var backgrounds=SettingsSection("Backgrounds");
            backgrounds.Children.Add(BackgroundsContent());
            var stats=SettingsSection("Stats");
            BuildStatsSection(stats);
            BuildDiscordSection(SettingsSection("Discord RPC"));
            return new StackPanel();
        }
        // About: a centered introduction (the mark, the name, the version and what you can do about it), then the updates and a grid of places to go.
        Border AboutCard()
        {
            var page=new StackPanel();

            // ---- The introduction.
            var hero=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,26,0,26)};
            var mark=Art.Logo(68);
            mark.HorizontalAlignment=HorizontalAlignment.Center;
            hero.Children.Add(mark);
            hero.Children.Add(new TextBlock {Text="lucent",FontSize=34,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,12,0,0)});
            hero.Children.Add(new TextBlock {Text="Your display, your games and your music, in one window.",FontSize=14,Foreground=Muted,TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,16)});
            var badge=new StackPanel {Orientation=Orientation.Horizontal};
            badge.Children.Add(new System.Windows.Shapes.Ellipse {Width=7,Height=7,Fill=new SolidColorBrush(Color.FromRgb(46,204,113)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
            badge.Children.Add(new TextBlock {Text="v"+Design.Version,FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            hero.Children.Add(new Border {Child=badge,Background=new SolidColorBrush(Color.FromArgb(38,255,255,255)),CornerRadius=new CornerRadius(15),Padding=new Thickness(13,6,15,6),HorizontalAlignment=HorizontalAlignment.Center});
            string built=System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("d MMM yyyy",System.Globalization.CultureInfo.InvariantCulture);
            hero.Children.Add(new TextBlock {Text="Version "+Design.Version+(Design.Snapshot==null?"":" \u00B7 Snapshot "+Design.Snapshot)+" \u00B7 Built "+built,FontSize=12,Foreground=Muted,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,14)});
            var copyLabel=new TextBlock {Text="Copy version info",FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var copyIcon=Design.SettingsIcon("Copy",16,new SolidColorBrush(Color.FromRgb(17,17,17)));
            copyIcon.Margin=new Thickness(0,0,10,0);
            var copyRow=new StackPanel {Orientation=Orientation.Horizontal};
            copyRow.Children.Add(copyIcon);
            copyRow.Children.Add(copyLabel);
            var copy=Action("",async()=>
            {
                if(!TestMode)Clipboard.SetText("Lucent "+Design.Version+(Design.Snapshot==null?"":" snapshot "+Design.Snapshot)+" ("+Environment.OSVersion.VersionString+")");
                copyLabel.Text="Copied";
                await System.Threading.Tasks.Task.Delay(1400);
                copyLabel.Text="Copy version info";
            });
            copy.Style=(Style)Resources["PrimaryAction"];
            copy.Content=copyRow;
            copy.Padding=new Thickness(20,10,22,10);

            // Copy the version, or go to the project: both sit under the version where they can be seen straight away.
            var githubIcon=Design.SettingsIcon("GitHub",16);
            githubIcon.Margin=new Thickness(0,0,9,0);
            var githubRow=new StackPanel {Orientation=Orientation.Horizontal};
            githubRow.Children.Add(githubIcon);
            githubRow.Children.Add(new TextBlock {Text="GitHub",FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var github=Action("",()=>{if(!TestMode)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/"+Updates.DefaultSource){UseShellExecute=true});});
            github.Content=githubRow;
            github.Padding=new Thickness(18,10,20,10);github.Margin=new Thickness(10,0,0,0);
            System.Windows.Automation.AutomationProperties.SetName(github,"GitHub");
            var heroButtons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            heroButtons.Children.Add(copy);heroButtons.Children.Add(github);
            hero.Children.Add(heroButtons);
            var heroCard=new Border {Child=hero,CornerRadius=new CornerRadius(20),Margin=new Thickness(0,0,0,14),Background=new RadialGradientBrush(Color.FromArgb(34,255,255,255),Color.FromArgb(0,255,255,255)){Center=new Point(.5,.28),GradientOrigin=new Point(.5,.28),RadiusX=.75,RadiusY=.95}};
            page.Children.Add(heroCard);

            // ---- Updates, in a card of their own.
            var updates=UpdatesBlock();
            updates.Margin=new Thickness(0);
            page.Children.Add(new Border {Child=updates,Background=settingsGroupSurface,CornerRadius=new CornerRadius(18),Padding=new Thickness(22,20,22,20),Margin=new Thickness(0,0,0,14)});

            // ---- Places to go: a grid of tiles.
            page.Children.Add(new TextBlock {Text="Links and folders",FontSize=15,FontWeight=FontWeights.Bold,Margin=new Thickness(4,6,0,10)});
            var tiles=new WrapPanel();
            tiles.Children.Add(AboutTile("Support","Support on Ko-fi","Optional, and always free to skip","https://ko-fi.com/lucent_"));
            tiles.Children.Add(AboutTile("About","Report a bug","Opens the issues page on GitHub","https://github.com/"+Updates.DefaultSource+"/issues"));
            tiles.Children.Add(AboutTile("Folder","App folder","Where Lucent is installed",AppDomain.CurrentDomain.BaseDirectory));
            tiles.Children.Add(AboutTile("Folder","Settings folder","Your saved settings and profiles",StateStore.Folder));
            tiles.Children.Add(AboutTile("Document","Font license","The license of the fonts Lucent uses",System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts","LICENSE.txt")));
            tiles.SizeChanged+=(s,e)=>
            {
                int across=e.NewSize.Width>620?3:2;
                double width=Math.Max(120,Math.Floor((e.NewSize.Width-across*10)/across));
                foreach(FrameworkElement child in tiles.Children)child.Width=width;
            };
            page.Children.Add(tiles);
            page.Children.Add(new TextBlock {Text="Made by positted, with Claude. Free and open source (MIT).",FontSize=12,Foreground=Muted,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,14,0,4)});
            return new Border {Child=page,Background=Brushes.Transparent};
        }
        // A tile of the About grid, built like the cards of the settings grid: its icon in a rounded square, its name and a line about it.
        // Opens a folder, a file or a page of this project.
        Border AboutTile(string icon,string text,string detail,string target)
        {
            var mark=Design.SettingsIcon(icon,20);
            mark.HorizontalAlignment=HorizontalAlignment.Center;mark.VerticalAlignment=VerticalAlignment.Center;
            var tile=new Border {Width=42,Height=42,CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(Color.FromArgb(26,255,255,255)),Margin=new Thickness(0,0,13,0),VerticalAlignment=VerticalAlignment.Center,Child=mark};
            var copy=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            copy.Children.Add(new TextBlock {Text=text,FontSize=14,FontWeight=FontWeights.Bold,TextTrimming=TextTrimming.CharacterEllipsis});
            copy.Children.Add(new TextBlock {Text=detail,FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0)});
            var row=new DockPanel {LastChildFill=true};
            DockPanel.SetDock(tile,Dock.Left);row.Children.Add(tile);row.Children.Add(copy);
            var link=Action("",()=>{if(!TestMode)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target){UseShellExecute=true});});
            link.Content=row;
            link.Style=(Style)Resources["SettingsRow"];
            link.Padding=new Thickness(14,12,14,12);link.MinHeight=72;
            link.HorizontalContentAlignment=HorizontalAlignment.Stretch;link.VerticalContentAlignment=VerticalAlignment.Center;
            System.Windows.Automation.AutomationProperties.SetName(link,text);
            return new Border {Child=link,Background=settingsGroupSurface,CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,10,10),ClipToBounds=true};
        }    }
}
