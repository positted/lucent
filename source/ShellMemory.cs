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
    // The memory dashboard on the Game Boost page.
    internal sealed partial class Shell
    {
        FrameworkElement BoostPage(){return BuildBoostWorkspace();}
        readonly DispatcherTimer memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        TextBlock memoryUsed, memoryAvailable, memoryResult;
        ProgressBar memoryMeter;

        bool cleaningMemory,memoryInitialized;
        TextBlock memoryDetail;
        public static readonly DependencyProperty DisplayedMemoryProperty=DependencyProperty.Register(
            "DisplayedMemory",typeof(double),typeof(Shell),new PropertyMetadata(0d,(owner,args)=>
            {
                var shell=(Shell)owner;
                if(shell.memoryUsed!=null)shell.memoryUsed.Text=((double)args.NewValue).ToString("0.00")+" GB";
            }));
        void AnimateMemory(RamCleanup.MemoryReading reading)
        {
            double used=reading.Used/1073741824d;
            var duration=TimeSpan.FromMilliseconds(650);
            var easing=new CubicEase {EasingMode=EasingMode.EaseOut};
            if(memoryInitialized)BeginAnimation(DisplayedMemoryProperty,new DoubleAnimation((double)GetValue(DisplayedMemoryProperty),used,duration) {EasingFunction=easing,FillBehavior=FillBehavior.Stop});
            SetValue(DisplayedMemoryProperty,used);
            if(memoryInitialized)memoryMeter.BeginAnimation(ProgressBar.ValueProperty,new DoubleAnimation(memoryMeter.Value,reading.Load,duration) {EasingFunction=easing,FillBehavior=FillBehavior.Stop});
            memoryMeter.Value=reading.Load;
            memoryInitialized=true;
            memoryAvailable.Text=reading.Load+"% of "+FormatRam(reading.Total)+" used     ·     "+FormatRam(reading.Available)+" available";
        }
        FrameworkElement BuildMemoryDashboard()
        {
            var card=Section("Memory","Physical RAM · live Windows readings");
            memoryUsed=Text("",44,true);
            memoryAvailable=Text("",14);memoryAvailable.Foreground=Muted;
            memoryMeter=new ProgressBar {Height=12,Maximum=100,Margin=new Thickness(0,7,0,15),Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(51,51,51))};
            memoryResult=Text("",22,true);
            memoryResult.Margin=new Thickness(0,16,0,3);
            memoryDetail=Text("",13);
            memoryDetail.Foreground=Muted;
            var readingRow=new DockPanel {Margin=new Thickness(0,5,0,6)};
            card.Children.Add(readingRow);card.Children.Add(memoryMeter);card.Children.Add(memoryAvailable);
            var clean=Action("Clean RAM",()=>{});
            clean.Style=(Style)Resources["PrimaryAction"];


            clean.Click+=async(sender,args)=>
            {
                if(TestMode||cleaningMemory)return;
                cleanedMark++;
                var stopSweeping=ShowMemoryCleanup(clean);
                try
                {
                    var before=RamCleanup.ReadMemory();
                    var detail=await Task.Run(()=>RamCleanup.RunAutomatically(preferences.BoostExclusions));
                    // Windows keeps releasing trimmed memory for a while, so wait for the reading to settle.
                    memoryDetail.Text="Measuring how much was freed…";
                    var settling=new MemorySettler(RamCleanup.ReadMemory().Used);
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    var after=RamCleanup.ReadMemory();
                    while(!closing&&!settling.Add(clock.Elapsed.TotalSeconds,after.Used))
                    {
                        AnimateMemory(after);
                        await Task.Delay(MemorySettler.SampleMilliseconds);
                        after=RamCleanup.ReadMemory();
                    }
                    if(closing)return;
                    double change=((double)before.Used-settling.Lowest)/1073741824d;
                    Count(c=>c.MemoryCleanups++);
                    memoryResult.Text=change>0?change.ToString("0.00")+" GB reclaimed":"Cleanup complete";
                    memoryDetail.Text="Before  "+FormatRam(before.Used)+"    →    After  "+FormatRam(settling.Lowest)
                        +(change<0?"  ·  RAM use rose during cleanup.":"");
                    memoryDetail.ToolTip=detail;
                    AnimateMemory(after);
                    memoryResult.BeginAnimation(OpacityProperty,new DoubleAnimation(.25,1,TimeSpan.FromMilliseconds(350)));
                }
                catch(Exception error){memoryResult.Text="Cleanup could not finish";memoryDetail.Text=error.Message;}
                finally
                {
                    stopSweeping();memoryMeter.BeginAnimation(OpacityProperty,null);
                    cleaningMemory=false;clean.Content=CleanedContent();clean.IsEnabled=true;
                    // "Cleaned" stays for five seconds, then the button is ready to clean again.
                    int shown=++cleanedMark;
                    Task.Delay(5000).ContinueWith(_=>Dispatcher.BeginInvoke(new Action(()=>{if(shown==cleanedMark&&!cleaningMemory&&!closing)clean.Content="Clean RAM";})));
                }
            };
            // A fixed size, so swapping the label for the broom cannot resize the button.
            clean.MinWidth=150;clean.Height=46;clean.FontSize=15;clean.MinHeight=0;clean.Padding=new Thickness(20,0,22,0);clean.HorizontalContentAlignment=HorizontalAlignment.Center;clean.Margin=new Thickness(18,0,0,0);
            clean.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(clean,Dock.Right);
            readingRow.Children.Add(clean);readingRow.Children.Add(memoryUsed);
            memoryResult.Visibility=memoryDetail.Visibility=Visibility.Collapsed;
            card.Children.Add(memoryResult);
            card.Children.Add(memoryDetail);
            return card;
        }
        int cleanedMark;
        // "Cleaned RAM" with a green check in front of it.
        static FrameworkElement CleanedContent()
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            row.Children.Add(GreenCheck(16,new Thickness(0,0,8,0)));
            row.Children.Add(new TextBlock {Text="Cleaned RAM",FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            return row;
        }
        // A check mark drawn as a shape, so it is exactly centered whatever the font does.
        internal static FrameworkElement GreenCheck(double size,Thickness margin)
        {
            var mark=new System.Windows.Shapes.Path {Data=Geometry.Parse("M3,10.5 L8,15.5 L17,5"),Stroke=new SolidColorBrush(Color.FromRgb(36,170,92)),StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
            var canvas=new Canvas {Width=20,Height=20};canvas.Children.Add(mark);
            return new Viewbox {Width=size,Height=size,Margin=margin,VerticalAlignment=VerticalAlignment.Center,Child=canvas};
        }
        static string FormatRam(ulong bytes){return(bytes/1073741824d).ToString("0.00")+" GB";}
        void RefreshMemoryDashboard()
        {
            if(memoryUsed==null||cleaningMemory)return;
            try{AnimateMemory(RamCleanup.ReadMemory());}
            catch(Exception error){memoryAvailable.Text=error.Message;}
        }
        // Puts the sweeping broom on the button. The returned action stops every animation,
        // since a repeating one would otherwise keep the window redrawing after it is gone.
        Action ShowMemoryCleanup(Button clean)
        {
            cleaningMemory=true;clean.IsEnabled=false;
            Action stopBroom;
            var busy=new StackPanel {Orientation=Orientation.Horizontal};
            busy.Children.Add(SweepingBroom(out stopBroom));
            clean.Content=busy;
            memoryResult.Visibility=memoryDetail.Visibility=Visibility.Visible;
            memoryResult.Text="Reclaiming memory";
            memoryDetail.Text="Checking eligible background apps…";
            memoryMeter.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(600)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever});
            return stopBroom;
        }
        // A broom swinging from the top of its handle, kicking up puffs of dust ahead of it.
        static FrameworkElement SweepingBroom(out Action stop)
        {
            const double PivotX=19,PivotY=1;   // the pivot is in the broom's own canvas
            // The canvas is larger than the broom and the broom sits inside it, so the swing and the dust are never cut off at the edge.
            var scene=new Canvas {Width=34,Height=34,Margin=new Thickness(0),VerticalAlignment=VerticalAlignment.Center,ClipToBounds=false,LayoutTransform=new ScaleTransform(1.2,1.2)};
            var swing=new RotateTransform(0,PivotX,PivotY);
            var broom=new Canvas {Width=28,Height=22,RenderTransform=swing};Canvas.SetLeft(broom,3);Canvas.SetTop(broom,5);
            broom.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M19,1 L12,11"),Stroke=Brushes.Black,StrokeThickness=2.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            broom.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M9,9.5 L15.5,13.5 L12.5,21 Q7,20.5 3.5,17.5 Z"),Fill=Brushes.Black,Stroke=Brushes.Black,StrokeThickness=1,StrokeLineJoin=PenLineJoin.Round});
            broom.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M8.6,13.5 L6.4,17.5 M11.6,14.8 L9.8,19"),Stroke=Brushes.White,StrokeThickness=1,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            scene.Children.Add(broom);
            swing.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(-15,15,TimeSpan.FromMilliseconds(380)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
            var puffs=new List<UIElement>();var drifts=new List<TranslateTransform>();
            for(int i=0;i<3;i++)
            {
                var drift=new TranslateTransform();
                var puff=new System.Windows.Shapes.Ellipse {Width=3,Height=3,Fill=Brushes.Black,Opacity=0,RenderTransform=drift};
                Canvas.SetLeft(puff,7+i*2.5);Canvas.SetTop(puff,25-i);
                scene.Children.Add(puff);puffs.Add(puff);drifts.Add(drift);
                var begin=TimeSpan.FromMilliseconds(i*250);
                var fade=new DoubleAnimationUsingKeyFrames {BeginTime=begin,RepeatBehavior=RepeatBehavior.Forever,Duration=TimeSpan.FromMilliseconds(760)};
                fade.KeyFrames.Add(new LinearDoubleKeyFrame(.55,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760))));
                puff.BeginAnimation(OpacityProperty,fade);
                drift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,-5-i,TimeSpan.FromMilliseconds(760)){BeginTime=begin,RepeatBehavior=RepeatBehavior.Forever});
                drift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-4-i,TimeSpan.FromMilliseconds(760)){BeginTime=begin,RepeatBehavior=RepeatBehavior.Forever});
            }
            stop=()=>
            {
                swing.BeginAnimation(RotateTransform.AngleProperty,null);
                foreach(var puff in puffs)puff.BeginAnimation(OpacityProperty,null);
                foreach(var drift in drifts){drift.BeginAnimation(TranslateTransform.XProperty,null);drift.BeginAnimation(TranslateTransform.YProperty,null);}
            };
            return scene;
        }
    }
}
