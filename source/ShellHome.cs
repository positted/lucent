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
            stack.Children.Add(HomeEditBar());
            stack.Children.Add(HomeUpdateCard());
            // The cards flow in the order and widths you chose (Customize Home): half-width cards sit two to a row, full-width ones take a row.
            homeFlow=new HomeFlow();
            homeSlots.Clear();
            var cards=new Dictionary<string,FrameworkElement>{{"display",HomeDisplayCard()},{"pc",HomePcCard()},{"playing",HomeNowPlaying()},{"shortcuts",HomeShortcuts()}};
            foreach(var key in HomeLayoutText.Keys)homeSlots.Add(MakeHomeSlot(key,cards[key]));
            stack.Children.Add(homeFlow);
            ApplyHomeLayout();
            UpdateHome();
            return stack;
        }
        // ---- Customize Home: hide, reorder and resize the cards. Saved in preferences.HomeLayout.
        HomeFlow homeFlow;
        readonly List<HomeSlot> homeSlots=new List<HomeSlot>();
        bool homeEditing;
        Button homeEditButton,homeResetButton;
        FrameworkElement HomeEditBar()
        {
            var bar=new WrapPanel {Margin=new Thickness(0,-12,0,14)};
            homeEditButton=Action("Customize Home",()=>SetHomeEditing(!homeEditing));
            homeResetButton=Action("Reset layout",ResetHomeLayout);
            homeResetButton.Visibility=Visibility.Collapsed;
            System.Windows.Automation.AutomationProperties.SetName(homeEditButton,"Customize Home");
            bar.Children.Add(homeEditButton);bar.Children.Add(homeResetButton);
            return bar;
        }
        internal void SetHomeEditing(bool on)
        {
            homeEditing=on;
            homeEditButton.Content=on?"Done":"Customize Home";
            homeResetButton.Visibility=on?Visibility.Visible:Visibility.Collapsed;
            if(homeFlow!=null)homeFlow.Editing=on;
            foreach(var slot in homeSlots)slot.ShowTools(on);
            ApplyHomeLayout();
        }
        void ResetHomeLayout()
        {
            preferences.HomeLayout=HomeLayoutText.Clean("");
            var entries=HomeLayoutText.Parse(preferences.HomeLayout);
            homeSlots.Sort((a,b)=>entries.FindIndex(x=>x.Key==a.Key).CompareTo(entries.FindIndex(x=>x.Key==b.Key)));
            foreach(var slot in homeSlots){var entry=entries.First(x=>x.Key==slot.Key);slot.Full=entry.Full;slot.Hidden=entry.Hidden;}
            SaveSoon();ApplyHomeLayout();
        }
        HomeSlot MakeHomeSlot(string key,FrameworkElement card)
        {
            var slot=new HomeSlot(key,card);
            slot.Earlier=Action("‹",()=>MoveHomeSlot(slot,-1));
            slot.Later=Action("›",()=>MoveHomeSlot(slot,1));
            slot.Size=Action("",()=>{slot.Full=!slot.Full;HomeLayoutChanged();});
            slot.Visible=Action("",()=>{slot.Hidden=!slot.Hidden;HomeLayoutChanged();});
            foreach(var button in new[]{slot.Earlier,slot.Later,slot.Size,slot.Visible}){button.Padding=new Thickness(11,4,11,4);button.MinHeight=0;button.FontSize=12;button.Margin=new Thickness(4,0,0,0);}
            System.Windows.Automation.AutomationProperties.SetName(slot.Earlier,"Move "+HomeLayoutText.Title(key)+" earlier");
            System.Windows.Automation.AutomationProperties.SetName(slot.Later,"Move "+HomeLayoutText.Title(key)+" later");
            System.Windows.Automation.AutomationProperties.SetName(slot.Size,"Resize "+HomeLayoutText.Title(key));
            System.Windows.Automation.AutomationProperties.SetName(slot.Visible,"Hide or show "+HomeLayoutText.Title(key));
            slot.BuildTools();
            var entry=HomeLayoutText.Parse(preferences.HomeLayout).First(x=>x.Key==key);
            slot.Full=entry.Full;slot.Hidden=entry.Hidden;
            return slot;
        }
        void MoveHomeSlot(HomeSlot slot,int direction)
        {
            int at=homeSlots.IndexOf(slot),to=at+direction;
            if(to<0||to>=homeSlots.Count)return;
            homeSlots.RemoveAt(at);homeSlots.Insert(to,slot);
            HomeLayoutChanged();
        }
        void HomeLayoutChanged()
        {
            preferences.HomeLayout=HomeLayoutText.Serialize(homeSlots.Select(s=>new HomeLayoutText.Entry {Key=s.Key,Full=s.Full,Hidden=s.Hidden}));
            SaveSoon();ApplyHomeLayout();
        }
        // Puts the slots into the panel in order, with their widths and labels.
        void ApplyHomeLayout()
        {
            if(homeFlow==null)return;
            homeFlow.Children.Clear();
            for(int i=0;i<homeSlots.Count;i++)
            {
                var slot=homeSlots[i];
                HomeFlow.SetFull(slot,slot.Full);
                slot.Refresh(i>0,i<homeSlots.Count-1,homeEditing);
                homeFlow.Children.Add(slot);
            }
            homeFlow.InvalidateMeasure();
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

    // The standard Home layout, and how a layout is written down in the settings file: "display:h;!pc:f;..." (h half width, f full width, ! hidden).
    internal static class HomeLayoutText
    {
        internal sealed class Entry {public string Key;public bool Full,Hidden;}
        internal static readonly string[] Keys={"display","playing","pc","shortcuts"};
        internal static string Title(string key)
        {
            switch(key)
            {
                case "display":return "Your display";
                case "playing":return "Now playing";
                case "pc":return "Your PC";
                default:return "Shortcuts";
            }
        }
        static bool DefaultFull(string key){return false;}
        // Anything unknown or repeated is dropped and anything missing is added back, so a damaged value can never lose a card.
        internal static List<Entry> Parse(string text)
        {
            var result=new List<Entry>();
            foreach(string raw in (text??"").Split(';'))
            {
                string token=raw.Trim();
                if(token.Length==0)continue;
                bool hidden=token.StartsWith("!");
                if(hidden)token=token.Substring(1);
                string[] parts=token.Split(':');
                string key=parts[0].Trim().ToLowerInvariant();
                if(Array.IndexOf(Keys,key)<0||result.Any(e=>e.Key==key))continue;
                bool full=parts.Length>1?parts[1].Trim().ToLowerInvariant()=="f":DefaultFull(key);
                result.Add(new Entry {Key=key,Full=full,Hidden=hidden});
            }
            foreach(string key in Keys)if(!result.Any(e=>e.Key==key))result.Add(new Entry {Key=key,Full=DefaultFull(key)});
            return result;
        }
        internal static string Serialize(IEnumerable<Entry> entries){return string.Join(";",entries.Select(e=>(e.Hidden?"!":"")+e.Key+":"+(e.Full?"f":"h")));}
        internal static string Clean(string text){return Serialize(Parse(text));}
    }

    // One card on Home, with the small controls that appear over it while Home is being customized.
    internal sealed class HomeSlot:Grid
    {
        public readonly string Key;
        public readonly FrameworkElement Card;
        public bool Full,Hidden;
        public Button Earlier,Later,Size,Visible;
        StackPanel tools;
        public HomeSlot(string key,FrameworkElement card)
        {
            Key=key;Card=card;ClipToBounds=true;Children.Add(card);
            // A card that steps aside or comes back by itself (Now playing) makes the cards around it flow again.
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty,typeof(UIElement)).AddValueChanged(card,(s,e)=>
            {
                var flow=VisualTreeHelper.GetParent(this) as HomeFlow;
                if(flow!=null)flow.InvalidateMeasure();
            });
        }
        public void BuildTools()
        {
            tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,14,14,0),Visibility=Visibility.Collapsed};
            tools.Children.Add(Earlier);tools.Children.Add(Later);tools.Children.Add(Size);tools.Children.Add(Visible);
            Children.Add(tools);
        }
        public void ShowTools(bool on){if(tools!=null)tools.Visibility=on?Visibility.Visible:Visibility.Collapsed;}
        public void Refresh(bool canEarlier,bool canLater,bool editing)
        {
            Earlier.IsEnabled=canEarlier;Later.IsEnabled=canLater;
            Size.Content=Full?"Half width":"Full width";
            Visible.Content=Hidden?"Show":"Hide";
            // While customizing, a hidden card stays on the page, dimmed, so it can be brought back.
            Card.Opacity=Hidden?.4:1;
            Visibility=Hidden&&!editing?Visibility.Collapsed:Visibility.Visible;
            ShowTools(editing);
        }
        // A card that is hidden, or has nothing to show (Now playing with nothing playing), takes no room.
        public bool Absent(bool editing){return (Hidden&&!editing)||Card.Visibility==Visibility.Collapsed;}
    }

    // Lays Home's cards out: half-width ones two to a row, full-width ones on a row of their own, everything in one column when the window is narrow.
    internal sealed class HomeFlow:Panel
    {
        public static readonly DependencyProperty FullProperty=DependencyProperty.RegisterAttached("Full",typeof(bool),typeof(HomeFlow),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsParentMeasure));
        public static bool GetFull(DependencyObject element){return (bool)element.GetValue(FullProperty);}
        public static void SetFull(DependencyObject element,bool value){element.SetValue(FullProperty,value);}
        bool editing;
        public bool Editing{get{return editing;}set{editing=value;InvalidateMeasure();}}
        const double Gap=16,Narrow=760;
        readonly List<List<HomeSlot>> rows=new List<List<HomeSlot>>();
        readonly List<double> heights=new List<double>();
        protected override Size MeasureOverride(Size available)
        {
            double width=double.IsInfinity(available.Width)?820:available.Width;
            bool narrow=width<Narrow;
            rows.Clear();heights.Clear();
            List<HomeSlot> open=null;
            foreach(UIElement element in InternalChildren)
            {
                var slot=element as HomeSlot;
                if(slot==null)continue;
                if(slot.Absent(editing)){slot.Measure(new Size(0,0));continue;}
                if(narrow||GetFull(slot)){rows.Add(new List<HomeSlot> {slot});open=null;}
                else if(open!=null){open.Add(slot);open=null;}
                else{open=new List<HomeSlot> {slot};rows.Add(open);}
            }
            double half=Math.Max(0,(width-Gap)/2),total=0;
            foreach(var row in rows)
            {
                double rowHeight=0;
                foreach(var slot in row)
                {
                    slot.Measure(new Size(narrow||GetFull(slot)?width:half,double.PositiveInfinity));
                    rowHeight=Math.Max(rowHeight,slot.DesiredSize.Height);
                }
                heights.Add(rowHeight);total+=rowHeight;
            }
            return new Size(width,total);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            bool narrow=finalSize.Width<Narrow;
            double y=0;
            for(int i=0;i<rows.Count;i++)
            {
                double x=0;
                foreach(var slot in rows[i])
                {
                    double width=narrow||GetFull(slot)?finalSize.Width:Math.Max(0,(finalSize.Width-Gap)/2);
                    slot.Arrange(new Rect(x,y,width,heights[i]));
                    x+=width+Gap;
                }
                y+=heights[i];
            }
            foreach(UIElement element in InternalChildren)
            {
                var slot=element as HomeSlot;
                if(slot!=null&&slot.Absent(editing))slot.Arrange(new Rect(0,0,0,0));
            }
            return finalSize;
        }
    }
}
