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
        Border AboutCard()
        {
            var surface=new Grid();
            var watermark=Art.Logo(440);
            watermark.Opacity=.04;
            watermark.HorizontalAlignment=HorizontalAlignment.Right;
            watermark.VerticalAlignment=VerticalAlignment.Center;
            watermark.Margin=new Thickness(0,-70,-80,-70);
            watermark.IsHitTestVisible=false;
            surface.Children.Add(watermark);
            var page=new StackPanel {Margin=new Thickness(28,26,28,26)};
            surface.Children.Add(page);

            var top=new DockPanel();
            var badge=new StackPanel {Orientation=Orientation.Horizontal};
            badge.Children.Add(new System.Windows.Shapes.Ellipse {Width=7,Height=7,Fill=new SolidColorBrush(Color.FromRgb(46,204,113)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
            badge.Children.Add(new TextBlock {Text="v"+Design.Version,FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var version=new Border {Child=badge,Background=new SolidColorBrush(Color.FromRgb(56,56,58)),CornerRadius=new CornerRadius(15),Padding=new Thickness(12,6,14,6),VerticalAlignment=VerticalAlignment.Top};
            DockPanel.SetDock(version,Dock.Right);
            top.Children.Add(version);
            var brand=new StackPanel();
            var mark=Art.Logo(46);
            mark.HorizontalAlignment=HorizontalAlignment.Left;
            brand.Children.Add(mark);
            brand.Children.Add(new TextBlock {Text="lucent",FontSize=26,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont,Margin=new Thickness(0,10,0,0)});
            top.Children.Add(brand);
            page.Children.Add(top);

            string built=System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("d MMM yyyy",System.Globalization.CultureInfo.InvariantCulture);
            page.Children.Add(new TextBlock {Text="Version "+Design.Version+" \u00B7 Built "+built,FontSize=13,Foreground=Muted,Margin=new Thickness(0,64,0,12)});

            var copyLabel=new TextBlock {Text="Copy version info",FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var copyIcon=Design.SettingsIcon("Copy",16,new SolidColorBrush(Color.FromRgb(17,17,17)));
            copyIcon.Margin=new Thickness(0,0,10,0);
            var copyRow=new StackPanel {Orientation=Orientation.Horizontal};
            copyRow.Children.Add(copyIcon);
            copyRow.Children.Add(copyLabel);
            var copy=Action("",async()=>
            {
                if(!TestMode)Clipboard.SetText("Lucent "+Design.Version+" ("+Environment.OSVersion.VersionString+")");
                copyLabel.Text="Copied";
                await System.Threading.Tasks.Task.Delay(1400);
                copyLabel.Text="Copy version info";
            });
            copy.Style=(Style)Resources["PrimaryAction"];
            copy.Content=copyRow;
            copy.Padding=new Thickness(18,11,20,11);
            copy.Margin=new Thickness(0,0,0,22);
            page.Children.Add(UpdatesBlock());
            page.Children.Add(copy);

            var links=new WrapPanel();
            links.Children.Add(AboutLink("GitHub","GitHub","https://github.com/"+Updates.DefaultSource));
            links.Children.Add(AboutLink("Support","Support on Ko-fi","https://ko-fi.com/lucent_"));
            links.Children.Add(AboutLink("About","Report a bug","https://github.com/"+Updates.DefaultSource+"/issues"));
            links.Children.Add(AboutLink("Folder","App folder",AppDomain.CurrentDomain.BaseDirectory));
            links.Children.Add(AboutLink("Folder","Settings folder",StateStore.Folder));
            links.Children.Add(AboutLink("Document","Font license",System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts","LICENSE.txt")));
            page.Children.Add(links);

            surface.SizeChanged+=(s,e)=>surface.Clip=new RectangleGeometry(new Rect(e.NewSize),20,20);
            return new Border {Child=surface,Background=settingsGroupSurface,CornerRadius=new CornerRadius(20)};
        }
        Button AboutLink(string icon,string text,string target)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal};
            var mark=Design.SettingsIcon(icon,15);
            mark.Margin=new Thickness(0,0,9,0);
            row.Children.Add(mark);
            row.Children.Add(new TextBlock {Text=text,FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var link=Action("",()=>{if(!TestMode)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target){UseShellExecute=true});});
            link.Content=row;
            link.Background=new SolidColorBrush(Color.FromRgb(52,52,54));
            link.Padding=new Thickness(13,9,14,9);
            link.Margin=new Thickness(0,0,8,8);
            return link;
        }
    }
}
