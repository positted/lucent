using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Lucent
{
    // Settings, Stats: a few friendly numbers about how Lucent is used, kept on this PC.
    internal sealed partial class Shell
    {
        readonly DispatcherTimer statsClock=new DispatcherTimer {Interval=TimeSpan.FromMinutes(1)};
        Action statsRefresh;
        void Count(Action<UsageStats> add)
        {
            add(preferences.Stats);
            SaveSoon();
            if(statsRefresh!=null&&settingsCategory=="Stats")statsRefresh();
        }
        // Counts the minutes Lucent has been running, tray or not.
        void StartStatsClock()
        {
            if(TestMode)return;
            statsClock.Tick+=(s,e)=>Count(c=>c.MinutesOpen++);
            statsClock.Start();
        }
        // A light-hearted title for whatever the user does most.
        internal static string StatsTitle(UsageStats stats)
        {
            if(stats.MinutesOpen>=6000)return "Lucent lifer";
            if(stats.ZoomUses>=200)return "Eagle eye";
            if(stats.ResolutionSwitches>=100)return "Resolution hopper";
            if(stats.GamesStarted>=50)return "Game night regular";
            if(stats.MemoryCleanups>=25)return "Tidy desk";
            if(stats.Launches>=10||stats.MinutesOpen>=120)return "Getting comfortable";
            return "Just getting started";
        }
        void BuildStatsSection(SectionPanel section)
        {
            var title=new TextBlock {FontSize=30,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont,Margin=new Thickness(0,2,0,2)};
            var since=new TextBlock {FontSize=13,Foreground=Muted,Margin=new Thickness(0,0,0,14)};
            section.Children.Add(title);
            section.Children.Add(since);
            var tiles=new WrapPanel();
            var shown=new List<KeyValuePair<TextBlock,Func<UsageStats,string>>>();
            Action<string,Func<UsageStats,string>> tile=(label,value)=>
            {
                var number=new TextBlock {FontSize=26,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont};
                var caption=new TextBlock {Text=label,FontSize=12,Foreground=Muted,Margin=new Thickness(0,3,0,0),TextWrapping=TextWrapping.Wrap};
                var inner=new StackPanel();inner.Children.Add(number);inner.Children.Add(caption);
                tiles.Children.Add(new Border {Width=170,MinHeight=84,Background=settingsGroupSurface,CornerRadius=new CornerRadius(14),Padding=new Thickness(16,14,14,12),Margin=new Thickness(0,0,10,10),Child=inner});
                shown.Add(new KeyValuePair<TextBlock,Func<UsageStats,string>>(number,value));
            };
            tile("Time with Lucent running",s=>UsageStats.Duration(s.MinutesOpen));
            tile("Times opened",s=>s.Launches.ToString("N0",CultureInfo.CurrentCulture));
            tile("Resolution switches",s=>s.ResolutionSwitches.ToString("N0",CultureInfo.CurrentCulture));
            tile("Times you zoomed in",s=>s.ZoomUses.ToString("N0",CultureInfo.CurrentCulture));
            tile("Games started from Lucent",s=>s.GamesStarted.ToString("N0",CultureInfo.CurrentCulture));
            tile("Game looks switched on",s=>s.ProfileSwitches.ToString("N0",CultureInfo.CurrentCulture));
            tile("RAM cleanups",s=>s.MemoryCleanups.ToString("N0",CultureInfo.CurrentCulture));
            tile("Crosshair turned on",s=>s.CrosshairUses.ToString("N0",CultureInfo.CurrentCulture));
            section.Children.Add(tiles);
            var note=new TextBlock {Text="These numbers are only counted and kept on this PC. Nothing is sent anywhere.",FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,10)};
            section.Children.Add(note);
            var reset=Action("Reset stats",()=>{});
            // Resetting asks first, so a stray click cannot wipe the numbers.
            var confirm=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed};
            confirm.Children.Add(new TextBlock {Text="Reset all the numbers to zero?",FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)});
            var yes=Action("Yes, reset",()=>{});
            yes.Style=(Style)Resources["PrimaryAction"];
            var no=Action("Cancel",()=>{});
            confirm.Children.Add(yes);confirm.Children.Add(no);
            reset.Click+=(s,e)=>{reset.Visibility=Visibility.Collapsed;confirm.Visibility=Visibility.Visible;};
            no.Click+=(s,e)=>{confirm.Visibility=Visibility.Collapsed;reset.Visibility=Visibility.Visible;};
            // The numbers are sucked into a trash can, and zeros pop up where they were.
            var trash=new Border {Width=46,Height=46,CornerRadius=new CornerRadius(14),Background=new SolidColorBrush(Color.FromArgb(34,255,255,255)),Opacity=0,Visibility=Visibility.Collapsed,
                RenderTransformOrigin=new Point(.5,.82),RenderTransform=new TransformGroup {Children=new TransformCollection {new ScaleTransform(1,1),new RotateTransform(0)}}};
            var trashIcon=Design.Icon("Trash",24);trashIcon.HorizontalAlignment=HorizontalAlignment.Center;trashIcon.VerticalAlignment=VerticalAlignment.Center;trash.Child=trashIcon;
            bool vacuuming=false;
            Action finishReset=()=>
            {
                preferences.Stats=new UsageStats {Since=DateTime.Now.ToString("yyyy-MM-dd")};
                SaveSoon();statsRefresh();
            };
            Action vacuum=async()=>
            {
                if(vacuuming)return;
                vacuuming=true;
                var group=(TransformGroup)trash.RenderTransform;var pulse=(ScaleTransform)group.Children[0];var wiggle=(RotateTransform)group.Children[1];
                trash.Visibility=Visibility.Visible;
                trash.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
                var center=new Point(trash.Width/2,trash.Height/2);
                Point target=trash.TranslatePoint(center,section);
                int index=0;
                foreach(var entry in shown)
                {
                    var number=entry.Key;
                    var move=new TranslateTransform();var shrink=new ScaleTransform(1,1);
                    number.RenderTransformOrigin=new Point(.5,.5);number.RenderTransform=new TransformGroup {Children=new TransformCollection {shrink,move}};
                    Point from=number.TranslatePoint(new Point(number.ActualWidth/2,number.ActualHeight/2),section);
                    double dx=target.X-from.X,dy=target.Y-from.Y;
                    var delay=TimeSpan.FromMilliseconds(index*70);index++;
                    // Sideways and downward speed up differently, so each number bends in toward the can as it is pulled.
                    move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,dx,TimeSpan.FromMilliseconds(640)){BeginTime=delay,EasingFunction=new PowerEase {EasingMode=EasingMode.EaseIn,Power=2.4}});
                    move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,dy,TimeSpan.FromMilliseconds(640)){BeginTime=delay,EasingFunction=new PowerEase {EasingMode=EasingMode.EaseIn,Power=1.6}});
                    var small=new DoubleAnimation(1,.12,TimeSpan.FromMilliseconds(640)){BeginTime=delay,EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
                    shrink.BeginAnimation(ScaleTransform.ScaleXProperty,small);shrink.BeginAnimation(ScaleTransform.ScaleYProperty,small);
                    number.BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(220)){BeginTime=delay+TimeSpan.FromMilliseconds(470)});
                }
                await System.Threading.Tasks.Task.Delay(shown.Count*70+720);
                // The can takes a gulp.
                var gulp=new DoubleAnimation(1,1.28,TimeSpan.FromMilliseconds(130)){AutoReverse=true,EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
                pulse.BeginAnimation(ScaleTransform.ScaleXProperty,gulp);pulse.BeginAnimation(ScaleTransform.ScaleYProperty,gulp);
                wiggle.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimationUsingKeyFrames {KeyFrames=new DoubleKeyFrameCollection {new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0)),new LinearDoubleKeyFrame(-10,KeyTime.FromPercent(.25)),new LinearDoubleKeyFrame(8,KeyTime.FromPercent(.6)),new LinearDoubleKeyFrame(0,KeyTime.FromPercent(1))},Duration=TimeSpan.FromMilliseconds(380)});
                await System.Threading.Tasks.Task.Delay(300);
                finishReset();
                // Zeros appear one after another.
                int pop=0;
                foreach(var entry in shown)
                {
                    var number=entry.Key;
                    number.RenderTransform=Transform.Identity;number.BeginAnimation(OpacityProperty,null);number.Opacity=1;
                    var grow=new ScaleTransform(.3,.3);number.RenderTransform=grow;
                    var spring=new DoubleAnimation(.3,1,TimeSpan.FromMilliseconds(380)){BeginTime=TimeSpan.FromMilliseconds(pop*60),EasingFunction=new BackEase {EasingMode=EasingMode.EaseOut,Amplitude=.9}};pop++;
                    grow.BeginAnimation(ScaleTransform.ScaleXProperty,spring);grow.BeginAnimation(ScaleTransform.ScaleYProperty,spring);
                    number.Opacity=0;number.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(200)){BeginTime=TimeSpan.FromMilliseconds(pop*60-60)});
                }
                await System.Threading.Tasks.Task.Delay(shown.Count*60+450);
                var gone=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(220));
                gone.Completed+=(a,b)=>trash.Visibility=Visibility.Collapsed;
                trash.BeginAnimation(OpacityProperty,gone);
                vacuuming=false;
            };
            yes.Click+=(s,e)=>
            {
                confirm.Visibility=Visibility.Collapsed;reset.Visibility=Visibility.Visible;
                if(TestMode)finishReset();else vacuum();
            };
            var actions=new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            var buttons=new StackPanel {VerticalAlignment=VerticalAlignment.Center};buttons.Children.Add(reset);buttons.Children.Add(confirm);
            actions.Children.Add(buttons);
            trash.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(trash,2);actions.Children.Add(trash);
            section.Children.Add(actions);            statsRefresh=()=>
            {
                var stats=preferences.Stats;
                title.Text=StatsTitle(stats);
                DateTime day;
                since.Text=DateTime.TryParseExact(stats.Since,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)?"Counting since "+day.ToString("d MMM yyyy",CultureInfo.CurrentCulture):"";
                foreach(var entry in shown){entry.Key.Text=entry.Value(stats);entry.Key.FontSize=entry.Key.Text.Length>8?20:26;}   // a long time such as "13 h 44 min" is smaller so it fits its tile
            };
            statsRefresh();
        }
    }
}
