using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ResolutionSwitcher;

namespace Lucent
{
    // The Home page: where the display is right now, the few things worth switching quickly, what is playing and the main shortcuts.
    internal sealed partial class Shell
    {
        // Everything on Home that can change while it is open. Each entry re-reads its own value.
        readonly List<Action> homeUpdaters=new List<Action>();
        MediaTrack ambientTrack;
        FrameworkElement HomePage()
        {
            var stack=Page("Home","What is on right now, and the quickest ways to change it.");
            var main=new StackPanel();
            main.Children.Add(HomeUpdateCard());
            main.Children.Add(HomeDisplayCard());
            main.Children.Add(HomePcCard());
            main.Children.Add(HomeTiles());
            var aside=new StackPanel();
            aside.Children.Add(HomeNowPlaying());
            aside.Children.Add(HomeShortcuts());
            stack.Children.Add(ResponsiveColumns(main,aside,1.45));
            UpdateHome();
            return stack;
        }
        internal void UpdateHome()
        {
            foreach(var update in homeUpdaters)update();
        }
        static void SetText(TextBlock label,string text){if(label.Text!=text)label.Text=text;}
        // Only there when a newer version is out.
        FrameworkElement HomeUpdateCard()
        {
            var card=Section("Update available","");
            var line=new TextBlock {FontSize=14,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
            card.Children.Add(line);
            var buttons=new WrapPanel();
            var get=Action("Get the update",OpenUpdatePage);
            get.Style=(Style)Resources["PrimaryAction"];
            buttons.Children.Add(get);
            buttons.Children.Add(Action("See what is new",()=>{SetSettingsOpen(true);SelectSettingsCategory("About");}));
            card.Children.Add(buttons);
            card.Visibility=Visibility.Collapsed;
            homeUpdaters.Add(()=>
            {
                var info=updateInfo;
                card.Visibility=info==null?Visibility.Collapsed:Visibility.Visible;
                if(info!=null)SetText(line,"Lucent "+Updates.Show(info.Latest)+" is out. You have "+Design.Version+".");
            });
            return card;
        }
        // ---- Your PC: how busy and how warm it is, as rings. Everything is read, nothing is changed.
        SystemStats systemStats;
        readonly Dictionary<string,StatRing> pcRings=new Dictionary<string,StatRing>();
        readonly Dictionary<string,FrameworkElement> pcTiles=new Dictionary<string,FrameworkElement>();
        TextBlock pcNote;
        bool samplingPc;
        int pcSkip;
        SectionPanel pcCard;
        SystemSnapshot lastSnapshot;
        bool PcRingChosen(string name){return Array.IndexOf(preferences.PcRings.Split(new[]{","},StringSplitOptions.RemoveEmptyEntries),name)>=0;}
        // The circles the user chose in Settings; the graphics ones also need a card that can be read.
        // 62 C or 144 F: just the number and the letter, Celsius unless Fahrenheit is chosen in Settings.
        string Degrees(double celsius){return preferences.Fahrenheit?Math.Round(celsius*9/5+32)+"\u00B0F":Math.Round(celsius)+"\u00B0C";}
        internal void UpdatePcRings()
        {
            if(pcCard==null)return;
            ShowSystemStats(lastSnapshot);
        }
        FrameworkElement HomePcCard()
        {
            var card=Section("Your PC","");
            pcCard=card;
            var rings=new WrapPanel {Margin=new Thickness(0,12,0,0)};
            foreach(string name in SystemStats.RingNames)
            {
                var ring=new StatRing();
                var caption=new TextBlock {Text=name,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Muted,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0)};
                var tile=new StackPanel {Margin=new Thickness(0,0,14,14),Width=84};
                tile.Children.Add(ring);tile.Children.Add(caption);
                rings.Children.Add(tile);pcRings[name]=ring;pcTiles[name]=tile;
            }
            pcNote=Text("",12);pcNote.Foreground=Muted;pcNote.TextWrapping=TextWrapping.Wrap;pcNote.Margin=new Thickness(0,0,0,2);
            card.Children.Add(rings);card.Children.Add(pcNote);
            ShowSystemStats(null);
            return card;
        }
        internal void ShowSystemStats(SystemSnapshot snapshot)
        {
            Action<string,double,string> put=(name,fraction,text)=>pcRings[name].Set(fraction,text);
            lastSnapshot=snapshot;
            pcCard.Visibility=preferences.PcRings.Length==0?Visibility.Collapsed:Visibility.Visible;
            if(snapshot==null)
            {
                foreach(string name in SystemStats.RingNames){put(name,-1,"\u2014");pcTiles[name].Visibility=PcRingChosen(name)&&(name=="CPU"||name=="RAM")?Visibility.Visible:Visibility.Collapsed;}
                pcNote.Text="";
                return;
            }
            put("CPU",snapshot.Cpu<0?-1:snapshot.Cpu/100,snapshot.Cpu<0?"\u2014":Math.Round(snapshot.Cpu)+"%");
            put("CPU temp",snapshot.CpuTemp<0?-1:snapshot.CpuTemp/100,snapshot.CpuTemp<0?"\u2014":Degrees(snapshot.CpuTemp));
            // Shown only when Windows shares the temperature on this PC.
            pcTiles["CPU temp"].Visibility=snapshot.CpuTemp>=0&&PcRingChosen("CPU temp")?Visibility.Visible:Visibility.Collapsed;
            put("RAM",snapshot.RamFraction,snapshot.RamFraction<0?"\u2014":Math.Round(snapshot.RamFraction*100)+"%");
            bool graphics=snapshot.Gpu>=0||snapshot.GpuTemp>=0;
            foreach(string name in new[]{"GPU","GPU temp","Video memory"})pcTiles[name].Visibility=graphics&&PcRingChosen(name)?Visibility.Visible:Visibility.Collapsed;
            foreach(string name in new[]{"CPU","RAM"})pcTiles[name].Visibility=PcRingChosen(name)?Visibility.Visible:Visibility.Collapsed;
            put("GPU",snapshot.Gpu<0?-1:snapshot.Gpu/100,snapshot.Gpu<0?"\u2014":Math.Round(snapshot.Gpu)+"%");
            put("GPU temp",snapshot.GpuTemp<0?-1:snapshot.GpuTemp/100,snapshot.GpuTemp<0?"\u2014":Degrees(snapshot.GpuTemp));
            put("Video memory",snapshot.VramFraction,snapshot.VramFraction<0?"\u2014":Math.Round(snapshot.VramFraction*100)+"%");
            pcNote.Text="";
        }
        // Looks at the PC every couple of seconds while Home is showing, off the UI thread.
        internal async Task RefreshSystemStats()
        {
            if(TestMode||closing||samplingPc||page!="Home"||settingsOpen||!IsVisible||WindowState==WindowState.Minimized)return;
            // Performance mode looks at the PC a third as often.
            if(preferences.PerformanceMode&&(++pcSkip%3)!=0)return;
            samplingPc=true;
            try
            {
                if(systemStats==null)systemStats=new SystemStats();
                var reader=systemStats;
                var snapshot=await Task.Run(()=>reader.Sample());
                if(!closing)ShowSystemStats(snapshot);
            }
            catch(Exception){}
            finally{samplingPc=false;}
        }
        FrameworkElement HomeDisplayCard()
        {
            var card=Section("Your display","");
            var size=new TextBlock {FontSize=40,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont,Margin=new Thickness(0,6,0,2)};
            var facts=new TextBlock {FontSize=14,Foreground=Muted,Margin=new Thickness(0,0,0,16)};
            card.Children.Add(size);
            card.Children.Add(facts);
            var buttons=new WrapPanel();
            var switchButton=Action("Switch resolution",()=>{TogglePair();UpdateHome();});
            switchButton.Style=(Style)Resources["PrimaryAction"];
            switchButton.ToolTip="Swap between your two quick-switch sizes";
            buttons.Children.Add(switchButton);
            buttons.Children.Add(Action("Open display",()=>ShowPage("Resolution")));
            card.Children.Add(buttons);
            homeUpdaters.Add(()=>
            {
                var mode=selected==null?null:DisplayService.GetCurrentMode(selected.DeviceName);
                if(mode==null){SetText(size,"Display unavailable");SetText(facts,"");return;}
                SetText(size,SizeLabel(mode));
                SetText(facts,mode.Frequency+" Hz  ·  "+mode.BitsPerPixel+"-bit color  ·  "+AspectName(mode.Width,mode.Height)+(displays.Count>1?"  ·  "+displays.Count+" displays":""));
            });
            return card;
        }
        FrameworkElement HomeTiles()
        {
            var card=Section("Quick actions","");
            var tiles=new WrapPanel();
            card.Children.Add(tiles);
            tiles.Children.Add(HomeTile("Crosshair","Crosshair",()=>crosshair==null?"Off · click to turn on":"On · click to turn off",ToggleCrosshair));
            tiles.Children.Add(HomeTile("Profiles","Profiles",()=>preferences.AutoProfiles?"Switching automatically · "+activeProfile:"Automatic switching is off",()=>ShowPage("Profiles")));
            tiles.Children.Add(HomeTile("Game Boost","Boost",()=>"Free memory before a game",()=>ShowPage("Game Boost")));
            tiles.Children.Add(HomeTile("Video","Color",()=>current.Red>0?"Red filter at "+(int)current.Red+"%":"Vibrance, red filter, dimming",()=>ShowPage("Video")));
            return card;
        }
        Button HomeTile(string icon,string title,Func<string> state,Action click)
        {
            var tile=Action("",click);
            tile.Width=276;tile.Height=78;tile.Margin=new Thickness(0,0,12,12);tile.Padding=new Thickness(16,0,12,0);
            tile.HorizontalContentAlignment=HorizontalAlignment.Left;
            var row=new DockPanel {VerticalAlignment=VerticalAlignment.Center};
            var glyph=Design.Icon(icon,22);
            glyph.Margin=new Thickness(0,0,14,0);
            DockPanel.SetDock(glyph,Dock.Left);
            row.Children.Add(glyph);
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            labels.Children.Add(new TextBlock {Text=title,FontSize=15,FontWeight=FontWeights.SemiBold});
            var line=new TextBlock {FontSize=12,Foreground=Muted,TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=FontWeights.Medium};
            labels.Children.Add(line);
            row.Children.Add(labels);
            tile.Content=row;
            System.Windows.Automation.AutomationProperties.SetName(tile,title);
            homeUpdaters.Add(()=>{string text=state();SetText(line,text);tile.ToolTip=text;});
            return tile;
        }
        FrameworkElement HomeNowPlaying()
        {
            var card=Section("Now playing","");
            // The cover on the left, the track beside it.
            var row=new Grid {Margin=new Thickness(0,6,0,12)};
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var cover=new Grid {Width=92,Height=92,Background=new SolidColorBrush(Color.FromRgb(38,38,41)),Clip=new RectangleGeometry(new Rect(0,0,92,92),14,14),Margin=new Thickness(0,0,16,0)};
            var placeholder=Design.Icon("Music",34);
            placeholder.Opacity=.5;
            var picture=new Image {Stretch=Stretch.UniformToFill};
            RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.HighQuality);
            cover.Children.Add(placeholder);
            cover.Children.Add(picture);
            row.Children.Add(cover);
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            var title=new TextBlock {FontSize=17,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};
            var artist=new TextBlock {FontSize=13,Foreground=Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)};
            labels.Children.Add(title);
            labels.Children.Add(artist);
            Grid.SetColumn(labels,1);
            row.Children.Add(labels);
            card.Children.Add(row);
            var open=Action("Open player",ToggleBigPlayer);
            card.Children.Add(open);
            byte[] shownArtwork=null;
            homeCover=picture;
            homeUpdaters.Add(()=>
            {
                var track=ambientTrack;
                bool any=track!=null&&!string.IsNullOrEmpty(track.Title);
                SetText(title,any?track.Title:"Nothing playing");
                SetText(artist,any?(string.IsNullOrEmpty(track.Artist)?track.Source:track.Artist):"Play something in any app and it shows up here.");
                open.Visibility=any?Visibility.Visible:Visibility.Collapsed;
                // With nothing playing there is nothing to show, so the whole card steps aside.
                card.Visibility=any?Visibility.Visible:Visibility.Collapsed;
                // Decode the cover only when it changes. This runs every couple of seconds while media is detected.
                byte[] artwork=any?track.Artwork:null;
                if(MediaSession.SameArtwork(shownArtwork,artwork))return;
                shownArtwork=artwork;
                picture.Source=artwork==null?null:DecodeCover(artwork);
                placeholder.Visibility=picture.Source==null?Visibility.Visible:Visibility.Collapsed;
            });
            return card;
        }
        Image homeCover;
        static BitmapSource DecodeCover(byte[] bytes)
        {
            try
            {
                using(var stream=new System.IO.MemoryStream(bytes))
                {
                    var image=new BitmapImage();
                    image.BeginInit();
                    image.CacheOption=BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth=184;
                    image.StreamSource=stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch(Exception){return null;}
        }        FrameworkElement HomeShortcuts()
        {
            var card=Section("Shortcuts","");
            var rows=new StackPanel();
            card.Children.Add(rows);
            foreach(var entry in new[]{new KeyValuePair<string,Func<int>>("Switch resolution",()=>preferences.SwitchKey),new KeyValuePair<string,Func<int>>("Crosshair",()=>preferences.CrosshairKey),new KeyValuePair<string,Func<int>>("Play or pause",()=>preferences.PlayKey)})
            {
                var read=entry.Value;
                var row=new DockPanel {Margin=new Thickness(0,3,0,3)};
                var key=new TextBlock {FontSize=13,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center};
                DockPanel.SetDock(key,Dock.Right);
                row.Children.Add(key);
                row.Children.Add(new TextBlock {Text=entry.Key,FontSize=14,VerticalAlignment=VerticalAlignment.Center});
                rows.Children.Add(row);
                homeUpdaters.Add(()=>SetText(key,BindText(read())));
            }
            var change=Action("Change shortcuts  →",()=>{SetSettingsOpen(true);SelectSettingsCategory("Shortcuts");});
            change.Style=(Style)Resources["TextAction"];
            change.Padding=new Thickness(0,10,0,0);
            card.Children.Add(change);
            return card;
        }
    }
}
