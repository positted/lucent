using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ResolutionSwitcher;
namespace Lucent
{
    // The background apps on the Game Boost page, shown as a board of tiles instead of a long list:
    // heaviest first, each with its share of memory, and the many tiny ones folded away.
    internal sealed partial class Shell
    {
        const long SmallApp=100L*1048576;
        const int FoldAfter=6,HeaviestCount=5;
        readonly List<BoostCandidate> boostResults=new List<BoostCandidate>();
        readonly Dictionary<string,Button> boostFilters=new Dictionary<string,Button>();
        string boostFilter="Apps";
        bool boostShowSmall;
        UniformGrid boostBoard;
        TextBlock boostSummary,boostSelection,boostEmpty;
        TextBox boostSearch;
        Button boostScanButton,boostCloseButton,boostRestoreButton;
        bool boostWorking;
        // Checks survive searching, filtering and excluding, but a new scan starts clean.
        readonly HashSet<string> boostSelected=new HashSet<string>();
        int scanMark;
        // Black like the text on the button.
        static void Ink(DependencyObject element,Brush brush)
        {
            var shape=element as System.Windows.Shapes.Shape;
            if(shape!=null){if(shape.Fill!=null)shape.Fill=brush;if(shape.Stroke!=null)shape.Stroke=brush;}
            foreach(object child in LogicalTreeHelper.GetChildren(element)){var next=child as DependencyObject;if(next!=null)Ink(next,brush);}
        }
        void SetScanText(string text)
        {
            var icon=Design.Icon("Search",16);Ink(icon,Brushes.Black);icon.Margin=new Thickness(0,0,9,0);icon.VerticalAlignment=VerticalAlignment.Center;
            var row=new StackPanel {Orientation=Orientation.Horizontal};
            row.Children.Add(icon);row.Children.Add(new TextBlock {Text=text,FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            boostScanButton.Content=row;
        }
        FrameworkElement BuildBoostWorkspace()
        {
            var page=Page(TabTitle("Game Boost"),"Your memory and background apps. Review what to close, and keep what matters.");
            page.Children.Add(BuildMemoryDashboard());
            var card=Section("Background apps","Heaviest first. Click apps to select them, then close them. Games, Vanguard, Windows, security and driver components stay protected.");
            page.Children.Add(card);
            var tools=new DockPanel {Margin=new Thickness(0,5,0,12)};
            // The scan button is on the left of the search box and looks like a search: the primary button style, a magnifier and the word.
            boostScanButton=Action("",async()=>await ScanWorkspace());
            boostScanButton.Style=(Style)Resources["PrimaryAction"];
            boostScanButton.Padding=new Thickness(16,0,18,0);boostScanButton.MinHeight=0;boostScanButton.Height=34;boostScanButton.Margin=new Thickness(0,4,10,4);
            System.Windows.Automation.AutomationProperties.SetName(boostScanButton,"Scan apps");
            SetScanText("Scan");
            DockPanel.SetDock(boostScanButton,Dock.Left);
            tools.Children.Add(boostScanButton);
            boostSearch=new TextBox {Height=34,MinHeight=0,FontFamily=Design.Font,FontSize=13,Padding=new Thickness(14,8,12,8),VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,0,4),ToolTip="Search by app, executable, publisher or service"};
            System.Windows.Automation.AutomationProperties.SetName(boostSearch,"Search detected apps");
            boostSearch.TextChanged+=(s,e)=>RenderBoostRows();
            var searchArea=new Grid();
            searchArea.Children.Add(boostSearch);
            var hint=new TextBlock {Text="Search apps and processes…",Foreground=Muted,FontSize=13,Margin=new Thickness(16,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
            searchArea.Children.Add(hint);
            boostSearch.TextChanged+=(s,e)=>hint.Visibility=string.IsNullOrWhiteSpace(boostSearch.Text)?Visibility.Visible:Visibility.Collapsed;
            tools.Children.Add(searchArea);
            card.Children.Add(tools);
            var bar=new DockPanel {Margin=new Thickness(0,0,0,10)};
            var picks=new WrapPanel();
            DockPanel.SetDock(picks,Dock.Right);
            foreach(string name in new[]{"Heaviest "+HeaviestCount,"Select all"})
            {
                string pick=name;
                var button=Action(name,()=>PickBoostApps(pick));
                button.FontSize=12;button.Padding=new Thickness(10,8,10,8);button.MinHeight=30;button.Margin=new Thickness(7,4,0,4);
                picks.Children.Add(button);
            }
            bar.Children.Add(picks);
            var filters=new WrapPanel();
            foreach(string name in new[]{"Apps","Protected","Excluded"})
            {
                string filter=name;
                var button=Action(name,()=>
                {
                    boostFilter=filter;
                    RenderBoostRows();
                });
                boostFilters[name]=button;
                filters.Children.Add(button);
            }
            bar.Children.Add(filters);
            card.Children.Add(bar);
            boostSummary=Text("Scan to see what's running. Nothing is selected automatically.",12);
            boostSummary.Foreground=Muted;
            boostSummary.Margin=new Thickness(0,0,0,10);
            card.Children.Add(boostSummary);
            boostBoard=new UniformGrid {Columns=4,Margin=new Thickness(0,0,-8,0)};
            // As many columns as fit, so the tiles use the full width at any window size.
            boostBoard.SizeChanged+=(s,e)=>{int columns=Math.Max(2,(int)((e.NewSize.Width+8)/222));if(boostBoard.Columns!=columns)boostBoard.Columns=columns;};
            card.Children.Add(boostBoard);
            boostEmpty=new TextBlock {Foreground=Muted,FontSize=13,Margin=new Thickness(0,18,0,22),Visibility=Visibility.Collapsed};
            card.Children.Add(boostEmpty);
            var footer=new DockPanel {Margin=new Thickness(0,10,0,0)};
            boostSelection=Text("0 selected",12);
            boostSelection.Foreground=Muted;
            boostSelection.VerticalAlignment=VerticalAlignment.Center;
            DockPanel.SetDock(boostSelection,Dock.Right);
            footer.Children.Add(boostSelection);
            var actions=new WrapPanel();
            boostCloseButton=Action("Close selected",async()=>await CloseWorkspaceSelection());
            boostCloseButton.Style=(Style)Resources["PrimaryAction"];
            boostCloseButton.IsEnabled=false;
            actions.Children.Add(boostCloseButton);
            boostRestoreButton=Action("Reopen closed apps",async()=>
            {
                if(boostWorking)return;
                boostWorking=true;
                try
                {
                    var result=await Task.Run(()=>boost.RestoreStopped());
                    SetStatus(result.Message);
                }
                finally
                {
                    boostWorking=false;
                }
                await ScanWorkspace();
            });
            boostRestoreButton.IsEnabled=false;
            actions.Children.Add(boostRestoreButton);
            footer.Children.Add(actions);
            card.Children.Add(footer);
            RenderBoostRows();return page;
        }
        async Task ScanWorkspace()
        {
            if(scanning||boostWorking||TestMode)return;
            scanning=true;
            boostScanButton.IsEnabled=false;
            boostCloseButton.IsEnabled=false;
            boostRestoreButton.IsEnabled=false;
            SetScanText("Scanning…");
            try
            {
                var result=await Task.Run(()=>boost.DetectCandidates());if(closing)return;
                boostResults.Clear();boostResults.AddRange(result);boostSelected.Clear();RenderBoostRows();
                boostSummary.Text=boostResults.Count(x=>!x.IsProtected)+" app groups · "+boostResults.Count(x=>x.IsProtected)+" protected groups · sampled CPU + physical RAM";
            }
            catch(Exception error){boostSummary.Text="Scan could not finish: "+error.Message;}
            finally{scanning=false;boostScanButton.IsEnabled=true;SetScanText("Scanned");
                // "Scanned" stays five seconds, then the button is ready to scan again.
                int shown=++scanMark;
                Task.Delay(5000).ContinueWith(_=>Dispatcher.BeginInvoke(new Action(()=>{if(shown==scanMark&&!scanning&&!closing)SetScanText("Scan");})));UpdateBoostSelection();}
        }
        static string BoostMemory(long bytes)
        {
            return bytes>=1073741824L?(bytes/1073741824d).ToString("0.0")+" GB":Math.Max(0,bytes/1048576d).ToString("N0")+" MB";
        }
        bool BoostExcluded(BoostCandidate item){return preferences.BoostExclusions.Contains(item.Definition.Id);}
        // The apps that can be picked: protected and excluded ones never are.
        IEnumerable<BoostCandidate> BoostPickable(){return boostResults.Where(x=>!x.IsProtected&&!BoostExcluded(x));}
        void PickBoostApps(string pick)
        {
            if(pick=="Clear")boostSelected.Clear();
            else
            {
                var chosen=pick=="Select all"?BoostPickable():BoostPickable().OrderByDescending(x=>x.MemoryBytes).Take(HeaviestCount);
                boostSelected.Clear();
                foreach(var item in chosen)boostSelected.Add(item.Definition.Id);
            }
            RenderBoostRows();
        }
        void RenderBoostRows()
        {
            if(boostBoard==null)return;
            boostBoard.Children.Clear();candidates.Clear();string query=boostSearch==null?"":boostSearch.Text.Trim();
            foreach(var filter in boostFilters)
            {
                int count=boostResults.Count(x=>filter.Key=="Protected"?x.IsProtected:filter.Key=="Excluded"?BoostExcluded(x):!x.IsProtected&&!BoostExcluded(x));
                filter.Value.Content=filter.Key+"  "+count;filter.Value.Background=filter.Key==boostFilter?selectionSurface:optionSurface;
            }
            var results=boostResults.Where(x=>boostFilter=="Protected"?x.IsProtected:boostFilter=="Excluded"?BoostExcluded(x):!x.IsProtected&&!BoostExcluded(x));
            if(query.Length>0)results=results.Where(x=>(x.Definition.DisplayName+" "+x.Definition.Reason+" "+string.Join(" ",x.Evidence)).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
            var shown=results.OrderByDescending(x=>x.MemoryBytes).ThenBy(x=>x.Definition.DisplayName).ToList();
            long peak=Math.Max(1,shown.Count==0?1:shown.Max(x=>x.MemoryBytes));
            // Many tiny apps would bury the heavy ones, so they fold into one tile until asked for.
            var small=shown.Where(x=>x.MemoryBytes<SmallApp).ToList();
            bool foldable=query.Length==0&&small.Count>=FoldAfter;
            foreach(var item in shown)
            {
                if(foldable&&!boostShowSmall&&item.MemoryBytes<SmallApp)continue;
                boostBoard.Children.Add(BoostTile(item,peak));
            }
            if(foldable)boostBoard.Children.Add(FoldTile(small));
            boostEmpty.Text=boostResults.Count==0?"Your detected apps will appear here.":"No apps match this view.";
            boostEmpty.Visibility=boostBoard.Children.Count==0?Visibility.Visible:Visibility.Collapsed;
            UpdateBoostSelection();
        }
        static readonly Dictionary<string,ImageSource> boostIcons=new Dictionary<string,ImageSource>();
        // Finds the program behind the app's running processes and takes its icon. Only the file path is read, never the process itself.
        void LoadBoostIcon(BoostCandidate item,Image target)
        {
            string id=item.Definition.Id;ImageSource known;
            if(boostIcons.TryGetValue(id,out known)){target.Source=known;return;}
            if(TestMode||item.Definition.ProcessNames==null)return;
            var names=item.Definition.ProcessNames;
            Task.Run(()=>
            {
                try
                {
                    foreach(string processName in names)
                    {
                        System.Diagnostics.Process[] running=System.Diagnostics.Process.GetProcessesByName(processName);
                        try
                        {
                            foreach(var process in running)
                            {
                                string path=ProcessIdentity.ImagePath(process.Id);
                                if(path==null||!System.IO.File.Exists(path))continue;
                                using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(path))
                                {
                                    var picture=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(48,48));
                                    picture.Freeze();
                                    return (ImageSource)picture;
                                }
                            }
                        }
                        finally{foreach(var process in running)process.Dispose();}
                    }
                }
                catch(Exception){}
                return null;
            }).ContinueWith(task=>Dispatcher.BeginInvoke(new Action(()=>
            {
                var picture=task.Status==TaskStatus.RanToCompletion?task.Result:null;
                boostIcons[id]=picture;
                if(picture!=null)target.Source=picture;
            })));
        }
        Border NewBoostTile()
        {
            return new Border {Tag="BoostTile",CornerRadius=new CornerRadius(14),Background=new SolidColorBrush(Color.FromArgb(22,255,255,255)),Margin=new Thickness(0,0,8,8),Padding=new Thickness(14,12,12,12),MinHeight=104};
        }
        static void Shade(Border tile,byte alpha){tile.Background.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Color.FromArgb(alpha,255,255,255),TimeSpan.FromMilliseconds(110)));}
        FrameworkElement BoostTile(BoostCandidate item,long peak)
        {
            bool excluded=BoostExcluded(item);
            bool pickable=!item.IsProtected&&!excluded;
            var tile=NewBoostTile();
            tile.Cursor=pickable?Cursors.Hand:Cursors.Arrow;
            tile.ToolTip=string.Join("\n\n",item.Evidence);
            tile.Opacity=pickable?1:.72;
            var grid=new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            for(int row=0;row<4;row++)grid.RowDefinitions.Add(new RowDefinition {Height=row==0?GridLength.Auto:row==3?new GridLength(1,GridUnitType.Star):GridLength.Auto});
            var name=new TextBlock {Text=item.Definition.DisplayName,Foreground=Brushes.White,FontSize=13,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
            // The app's own icon beside its name, so a row can be recognised at a glance before it is closed.
            var logo=new Image {Width=22,Height=22,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center,Tag="BoostLogo"};
            RenderOptions.SetBitmapScalingMode(logo,BitmapScalingMode.HighQuality);
            var head=new DockPanel();
            DockPanel.SetDock(logo,Dock.Left);
            head.Children.Add(logo);
            head.Children.Add(name);
            grid.Children.Add(head);
            LoadBoostIcon(item,logo);
            CheckBox check=null;
            if(pickable)
            {
                check=new CheckBox {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(8,0,0,0),Style=(Style)Resources["CandidateCheck"]};
                check.IsChecked=boostSelected.Contains(item.Definition.Id);
                var picked=check;
                Action paint=()=>Shade(tile,(byte)(picked.IsChecked==true?38:tile.IsMouseOver?30:22));
                check.Checked+=(s,e)=>{boostSelected.Add(item.Definition.Id);paint();UpdateBoostSelection();};
                check.Unchecked+=(s,e)=>{boostSelected.Remove(item.Definition.Id);paint();UpdateBoostSelection();};
                candidates[check]=item;
                System.Windows.Automation.AutomationProperties.SetName(check,"Select "+item.Definition.DisplayName);
                Grid.SetColumn(check,1);
                grid.Children.Add(check);
                if(check.IsChecked==true)((SolidColorBrush)tile.Background).Color=Color.FromArgb(38,255,255,255);
                tile.MouseEnter+=(s,e)=>paint();
                tile.MouseLeave+=(s,e)=>paint();
                tile.MouseLeftButtonUp+=(s,e)=>{picked.IsChecked=!(picked.IsChecked==true);};
            }
            else if(item.IsProtected)
            {
                var lockIcon=Design.Icon("Lock",15);
                lockIcon.Opacity=.5;
                lockIcon.VerticalAlignment=VerticalAlignment.Center;
                Grid.SetColumn(lockIcon,1);
                grid.Children.Add(lockIcon);
            }
            var amount=new TextBlock {Text=BoostMemory(item.MemoryBytes),Foreground=Brushes.White,FontSize=20,FontWeight=FontWeights.Bold,Margin=new Thickness(0,8,0,0)};
            Grid.SetRow(amount,1);Grid.SetColumnSpan(amount,2);
            grid.Children.Add(amount);
            string detail=item.IsProtected?item.ProtectionReason:excluded?"Excluded from cleanup and closure":item.Instances+" process"+(item.Instances==1?"":"es")+" · "+item.CpuPercent.ToString("0.0")+"% CPU";
            var info=new TextBlock {Text=detail,Foreground=Muted,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,0)};
            Grid.SetRow(info,2);Grid.SetColumnSpan(info,2);
            grid.Children.Add(info);
            // Its share of the heaviest app's memory, so the big ones stand out at a glance.
            double share=Math.Max(.03,Math.Min(1,item.MemoryBytes/(double)peak));
            var meter=new Grid {Height=3,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,10,0,0)};
            meter.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(share,GridUnitType.Star)});
            meter.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1-share,GridUnitType.Star)});
            meter.Background=new SolidColorBrush(Color.FromArgb(28,255,255,255));
            meter.Children.Add(new Border {Background=new SolidColorBrush(Color.FromArgb(item.IsProtected?(byte)90:(byte)200,255,255,255)),CornerRadius=new CornerRadius(1.5)});
            Grid.SetRow(meter,3);Grid.SetColumnSpan(meter,2);
            grid.Children.Add(meter);
            if(!item.IsProtected)
            {
                var exclude=Action(excluded?"Include":"Exclude",()=>
                {
                    if(excluded)preferences.BoostExclusions.Remove(item.Definition.Id);
                    else
                    {
                        preferences.BoostExclusions.Add(item.Definition.Id);
                        boostSelected.Remove(item.Definition.Id);
                    }
                    SaveSoon();
                    RenderBoostRows();
                });
                exclude.Style=(Style)Resources["TextAction"];
                exclude.FontSize=11;
                exclude.Padding=new Thickness(6,2,0,2);
                exclude.MinHeight=0;
                exclude.Margin=new Thickness(0);
                exclude.HorizontalAlignment=HorizontalAlignment.Right;
                exclude.VerticalAlignment=VerticalAlignment.Bottom;
                Grid.SetRow(exclude,2);Grid.SetColumn(exclude,1);
                // Shown on hover, but always for apps that are excluded, so they can be brought back.
                exclude.Opacity=excluded?1:0;
                if(!excluded)
                {
                    tile.MouseEnter+=(s,e)=>Design.Animate(exclude,OpacityProperty,exclude.Opacity,1,110);
                    tile.MouseLeave+=(s,e)=>Design.Animate(exclude,OpacityProperty,exclude.Opacity,0,140);
                }
                grid.Children.Add(exclude);
            }
            tile.Child=grid;
            return tile;
        }
        // One tile standing in for the tiny apps, which opens them up or folds them away again.
        FrameworkElement FoldTile(List<BoostCandidate> small)
        {
            var tile=NewBoostTile();
            tile.Cursor=Cursors.Hand;
            var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            stack.Children.Add(new TextBlock {Text=boostShowSmall?"Hide small apps":small.Count+" small apps",Foreground=Brushes.White,FontSize=13,FontWeight=FontWeights.SemiBold});
            stack.Children.Add(new TextBlock {Text=boostShowSmall?"Under 100 MB each":BoostMemory(small.Sum(x=>x.MemoryBytes))+" together · click to show",Foreground=Muted,FontSize=11,Margin=new Thickness(0,4,0,0),TextTrimming=TextTrimming.CharacterEllipsis});
            tile.Child=stack;
            tile.MouseEnter+=(s,e)=>Shade(tile,30);
            tile.MouseLeave+=(s,e)=>Shade(tile,22);
            tile.MouseLeftButtonUp+=(s,e)=>{boostShowSmall=!boostShowSmall;RenderBoostRows();};
            return tile;
        }
        void UpdateBoostSelection()
        {
            if(boostCloseButton==null)return;
            var ids=SelectedBoostIds();
            long bytes=boostResults.Where(x=>ids.Contains(x.Definition.Id)).Sum(x=>x.MemoryBytes);
            boostSelection.Text=ids.Length==0?"0 selected":ids.Length+" selected · about "+BoostMemory(bytes);
            boostCloseButton.IsEnabled=ids.Length>0&&!scanning&&!boostWorking;
            boostRestoreButton.IsEnabled=boost.HasRestorableApps&&!scanning&&!boostWorking;
        }
        string[] SelectedBoostIds()
        {
            return boostResults.Where(x=>!x.IsProtected&&boostSelected.Contains(x.Definition.Id)&&!preferences.BoostExclusions.Contains(x.Definition.Id)).Select(x=>x.Definition.Id).Distinct().ToArray();
        }
        async Task CloseWorkspaceSelection()
        {
            if(scanning||boostWorking||TestMode)return;
            var selected=SelectedBoostIds();if(selected.Length==0)return;
            if(selected.Any(x=>x.Equals("codex",StringComparison.OrdinalIgnoreCase))&&MessageBox.Show(this,"Closing Codex interrupts active chats, terminals and agent work. Close it with the selected apps?","Close Codex?",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            boostWorking=true;boostScanButton.IsEnabled=false;UpdateBoostSelection();
            try
            {
                var excluded=preferences.BoostExclusions.ToArray();
                var result=await Task.Run(()=>boost.StopSelected(selected,excluded));
                SetStatus(result.Message);
            }
            catch(Exception error){SetStatus(error.Message);}
            finally{boostWorking=false;boostScanButton.IsEnabled=true;}await ScanWorkspace();
        }
    }
}
