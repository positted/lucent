using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Lucent
{
    internal sealed partial class Shell
    {
        async Task CaptureModernPages()
        {
            if(!TestMode||effects!=null||gpuZoom!=null||crosshair!=null)throw new Exception("Live features enabled by UI capture.");
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews"));
            // The eye-break reminder as it appears in the window (Windows also shows the same words from the tray icon).
            ShowPage("Video");SettlePage();
            preferences.BreakReminders=true;
            typeof(Shell).GetField("breakElapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(this,preferences.BreakMinutes-1);
            BreakTick();
            await Task.Delay(500);UpdateLayout();
            CaptureFrame("Eye-break-reminder");
            preferences.BreakReminders=false;HideStatus();
            // Fixture numbers, so the rings of Home can be looked at.
            ShowSystemStats(new SystemSnapshot{Cpu=37,CpuTemp=-1,RamUsedGb=14.2,RamTotalGb=31.9,Gpu=64,GpuTemp=71,VramUsedGb=6.1,VramTotalGb=12,GpuName="Fixture"});
            await Task.Delay(800);
            preferences.Stats=new UsageStats{Launches=42,MinutesOpen=1260,ResolutionSwitches=18,ZoomUses=57,GamesStarted=9,ProfileSwitches=14,MemoryCleanups=6,CrosshairUses=23,Since="2026-11-06"};statsRefresh();
            foreach(string name in pages.Keys.Where(x=>x!="Settings"))
            {
                ShowPage(name);
                SettlePage();
                contentScroll.ScrollToTop();
                UpdateLayout();
                CaptureFrame(name.Replace(" ","-"));
                if(contentScroll.ScrollableHeight>0){contentScroll.ScrollToEnd();UpdateLayout();CaptureFrame(name.Replace(" ","-")+"-bottom");}
            }
            ShowPage("Video");
            contentScroll.ScrollToTop();
            UpdateLayout();
            SetSettingsOpen(true);
            await Task.Delay(420);
            UpdateLayout();
            CaptureFrame("Settings-floating");
            foreach(string name in new[]{"Shortcuts","Mini Player","General","About","Stats","Backgrounds","Discord RPC"})
            {
                SelectSettingsCategory(name);
                await Task.Delay(300);
                UpdateLayout();
                CaptureFrame("Settings-"+name);
            }
            SetSettingsOpen(false);await Task.Delay(360);
            if(settingsOpen||settingsLayer.Visibility!=Visibility.Hidden||page!="Video")throw new Exception("Settings close changed the current page.");
            mediaAccent=Color.FromRgb(190,122,52);
            UpdateAmbientColor();
            await Task.Delay(950);
            UpdateLayout();
            CaptureFrame("Video-adaptive-warm");
            mediaAccent=Color.FromRgb(75,90,190);
            UpdateAmbientColor();
            await Task.Delay(950);
            UpdateLayout();
            CaptureFrame("Video-adaptive-cool");
            foreach(var fixture in new[]{new{Name="sunset",A=Color.FromRgb(238,92,44),B=Color.FromRgb(66,38,150),C=Color.FromRgb(250,196,70)},new{Name="ocean",A=Color.FromRgb(16,120,170),B=Color.FromRgb(10,40,70),C=Color.FromRgb(70,210,170)},new{Name="pale",A=Color.FromRgb(244,240,232),B=Color.FromRgb(214,206,196),C=Color.FromRgb(250,250,250)}})
            {
                var art=new DrawingVisual();
                using(var dc=art.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(fixture.B),null,new Rect(0,0,128,128));
                    dc.DrawEllipse(new SolidColorBrush(fixture.A),null,new Point(30,34),52,48);
                    dc.DrawEllipse(new SolidColorBrush(fixture.C),null,new Point(102,100),40,44);
                }
                var pixels=new System.Windows.Media.Imaging.RenderTargetBitmap(128,128,96,96,PixelFormats.Pbgra32);
                pixels.Render(art);
                pixels.Freeze();
                paletteArtwork=new byte[1];
                paletteBitmap=pixels;
                mediaAccent=Design.Palette(pixels);
                UpdateAmbientColor();
                await Task.Delay(950);
                UpdateLayout();
                CaptureFrame("Video-artwork-"+fixture.Name);
                if(fixture.Name=="sunset")
                {
                    SetSettingsOpen(true);
                    await Task.Delay(420);
                    UpdateLayout();
                    CaptureFrame("Settings-over-artwork");
                    SetSettingsOpen(false);
                    await Task.Delay(360);
                }
            }
            foreach(string referenceArt in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"capture-art-*.png"))
            {
                var reference=new System.Windows.Media.Imaging.BitmapImage();
                reference.BeginInit();
                reference.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                reference.DecodePixelWidth=128;
                reference.UriSource=new Uri(referenceArt);
                reference.EndInit();
                reference.Freeze();
                paletteArtwork=new byte[1];
                paletteBitmap=reference;
                mediaAccent=Design.Palette(reference);
                UpdateAmbientColor();
                await Task.Delay(950);
                UpdateLayout();
                CaptureFrame("Video-artwork-"+Path.GetFileNameWithoutExtension(referenceArt).Substring(12));
            }
            paletteArtwork=null;paletteBitmap=null;
            mediaAccent=Color.FromRgb(46,46,48);
            UpdateAmbientColor();
            Width=MinWidth;
            Height=MinHeight;
            UpdateLayout();
            CaptureFrame("Video-compact");
            Width=1200;
            Height=780;
            UpdateLayout();
            var detected=await Task.Run(()=>boost.DetectCandidates());
            boostResults.Clear();
            boostResults.AddRange(detected);
            RenderBoostRows();
            boostSummary.Text=detected.Count(x=>!x.IsProtected)+" app groups · "+detected.Count(x=>x.IsProtected)+" protected groups";
            ShowPage("Game Boost");
            SettlePage();
            contentScroll.ScrollToEnd();
            UpdateLayout();
            CaptureFrame("Game-Boost-detected");
            boostFilter="Protected";
            RenderBoostRows();
            UpdateLayout();
            CaptureFrame("Game-Boost-protected");
            boostFilter="Apps";
            RenderBoostRows();
            using(var cover=new MemoryStream())
            {
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen())
                {
                    dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(30,60,170),Color.FromRgb(20,30,70),45),null,new Rect(0,0,80,80));
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(240,110,50)),null,new Point(30,34),26,26);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(60,210,170)),null,new Point(58,54),20,20);
                    dc.DrawEllipse(Brushes.Black,new Pen(Brushes.White,2),new Point(40,40),9,9);
                }
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(80,80,96,96,PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                png.Save(cover);
                var track=new MediaTrack{Title="Get Busy",Artist="Yeat",Source="Lucent UI fixture",Playing=true,CanPause=true,CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Position=42,Duration=162,Artwork=cover.ToArray()};
                var player=new MediaPlayerWindow(this,preferences,true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
                player.Show();
                player.SetTrack(track);
                player.UpdateLayout();
                await Task.Delay(320);
                CapturePlayer(player,"Mini-player-playing");
                track.Playing=false;
                player.SetTrack(track);
                await Task.Delay(320);
                CapturePlayer(player,"Mini-player-paused");
                track.Position=145;
                player.SetTrack(track);
                CapturePlayer(player,"Mini-player-progress-controls");
                track.Position=162;
                player.SetTrack(track);
                CapturePlayer(player,"Mini-player-progress-complete");
                track.Playing=true;track.Position=72;
                player.SetTrack(track);
                // Lyrics, the settings, and a larger window. The lyrics are fixture text; nothing goes online.
                player.ToggleLyrics();
                player.Lyrics.SetLyrics(LyricsService.Parse("[00:10.00] First line of the song\n[00:30.00] A second line follows it\n[00:50.00] The third line is longer so it wraps onto two rows\n[01:10.00] This is the line being sung now\n[01:30.00] Then the next line\n[01:50.00] And another one after that\n[02:10.00] The last line",null));
                player.Lyrics.Update(72);
                player.UpdateLayout();
                await Task.Delay(260);
                CapturePlayer(player,"Mini-player-lyrics");
                // The same view after a few idle seconds with hiding switched on for lyrics: the bar and buttons are gone, the lyrics stay.
                preferences.MiniHideLyrics=true;player.TickIdle(DateTime.UtcNow.AddSeconds(30));player.UpdateLayout();
                CapturePlayer(player,"Mini-player-lyrics-idle");
                player.Wake();preferences.MiniHideLyrics=false;player.UpdateLayout();
                player.UpdateLayout();
                await Task.Delay(260);
                player.ToggleLyrics();
                player.Width=720;
                player.UpdateLayout();
                await Task.Delay(200);
                CapturePlayer(player,"Mini-player-large");
                player.Width=400;player.UpdateLayout();
                preferences.MiniHideCompact=true;player.TickIdle(DateTime.UtcNow.AddSeconds(30));player.UpdateLayout();
                CapturePlayer(player,"Mini-player-tucked");
                preferences.MiniHideCompact=false;
                player.Close();
                CaptureIcons();
                bitmap.Freeze();
                paletteArtwork=new byte[1];
                paletteBitmap=bitmap;
                mediaAccent=Design.Palette(bitmap);
                UpdateAmbientColor();
                SetBigPlayerOpen(true);
                bigPlayerView.Core.SetTrack(track);
                await Task.Delay(950);
                UpdateLayout();
                CaptureFrame("Big-player");
                SetBigPlayerOpen(false);
                await Task.Delay(260);
                SetMediaAvailable(true);
                SetPlayerBarOpen(true);
                playerBar.Core.SetTrack(track);
                await Task.Delay(700);
                UpdateLayout();
                CaptureFrame("Player-bar");
                SetBigPlayerOpen(true);
                bigPlayerView.Core.SetTrack(track);
                await Task.Delay(520);
                UpdateLayout();
                CaptureFrame("Big-player-with-bar");
                SetBigPlayerOpen(false);
                await Task.Delay(260);
                SetPlayerBarOpen(false);
                await Task.Delay(320);
                var popped=new BigPlayerWindow(this,true,()=>{});
                popped.Show();
                popped.View.Core.SetTrack(track);
                await Task.Delay(700);
                CapturePlayer(popped,"Big-player-window");
                popped.Close();
                paletteArtwork=null;paletteBitmap=null;
                mediaAccent=Color.FromRgb(46,46,48);
                UpdateAmbientColor();
            }
            foreach(var tip in new[]{new{Name="Tooltip-rail",Tip=(ToolTip)navigationButtons[0].ToolTip},new{Name="Tooltip-player",Tip=(ToolTip)playerDockButton.ToolTip}})
            {
                tip.Tip.IsOpen=true;await Task.Delay(360);tip.Tip.UpdateLayout();
                var frame=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(tip.Tip.ActualWidth),(int)Math.Ceiling(tip.Tip.ActualHeight),96,96,PixelFormats.Pbgra32);
                frame.Render(tip.Tip);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
                using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews",tip.Name+".png")))png.Save(file);
                tip.Tip.IsOpen=false;
            }
            SetMediaAvailable(true);UpdateLayout();await Task.Delay(320);CaptureFrame("Player-dock");
            var cleanButton=new Button {Style=(Style)Resources["PrimaryAction"],Width=132,Height=38,MinHeight=0,Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
            windowSurface.Children.Add(cleanButton);
            var stopBroom=ShowMemoryCleanup(cleanButton);
            for(int frame=0;frame<3;frame++)
            {
                await Task.Delay(190);
                cleanButton.UpdateLayout();
                var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)cleanButton.ActualWidth,(int)cleanButton.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(cleanButton);
                var encoded=new System.Windows.Media.Imaging.PngBitmapEncoder();encoded.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews","Broom-"+frame+".png")))encoded.Save(file);
            }
            stopBroom();windowSurface.Children.Remove(cleanButton);cleaningMemory=false;memoryMeter.BeginAnimation(OpacityProperty,null);
            if(Title!="Lucent"||workspace.ColumnDefinitions[0].Width.Value!=70)throw new Exception("Brand or compact rail changed.");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"capture-checks.txt"),"PASS: compact rail, feature pages, floating settings groups, close restoration and adaptive theme. No live display writes or process termination.");
        }
        // The icons that must be told apart, and the three speakers at the size they are used, so their sizes can be compared by eye.
        static void CaptureIcons()
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,Background=new SolidColorBrush(Color.FromRgb(20,20,22)),Margin=new Thickness(0)};
            foreach(string name in new[]{"Volume","VolumeLow","Mute","PlayerBar","OpenPlayer","BigPlayer","Compare","Swap","More","Lyrics","Eye","Visual tools"})
            {
                var icon=Design.Icon(name,32);icon.Margin=new Thickness(14,16,14,16);row.Children.Add(icon);
            }
            var solidEye=Design.SettingsIcon("Visual tools",32);solidEye.Margin=new Thickness(14,16,14,16);row.Children.Add(solidEye);
            row.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));row.Arrange(new Rect(row.DesiredSize));row.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(row.ActualWidth),(int)Math.Ceiling(row.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(row);
            var png=new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews","Icons.png")))png.Save(file);
        }
        static void CapturePlayer(Window player,string name)
        {
            player.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(player.ActualWidth),(int)Math.Ceiling(player.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render((Visual)player.Content);
            var png=new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews",name+".png")))png.Save(file);
        }
    }
}
