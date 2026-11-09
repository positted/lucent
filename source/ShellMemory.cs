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
                    cleaningMemory=false;clean.Content="Clean again";clean.IsHitTestVisible=true;
                }
            };
            // A fixed size, so swapping the label for the broom cannot resize the button.
            clean.Width=132;clean.Height=38;clean.MinHeight=0;clean.Padding=new Thickness(0);clean.HorizontalContentAlignment=HorizontalAlignment.Center;clean.Margin=new Thickness(18,0,0,0);
            clean.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(clean,Dock.Right);
            readingRow.Children.Add(clean);readingRow.Children.Add(memoryUsed);
            memoryResult.Visibility=memoryDetail.Visibility=Visibility.Collapsed;
            card.Children.Add(memoryResult);
            card.Children.Add(memoryDetail);
            return card;
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
            cleaningMemory=true;clean.IsHitTestVisible=false;
            Action stopBroom;
            var busy=new StackPanel {Orientation=Orientation.Horizontal};
            busy.Children.Add(new TextBlock {Text="Cleaning",FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
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
            const double PivotX=19,PivotY=1;
            var scene=new Canvas {Width=24,Height=20,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,LayoutTransform=new ScaleTransform(.9,.9)};
            var swing=new RotateTransform(0,PivotX,PivotY);
            var broom=new Canvas {Width=28,Height=22,RenderTransform=swing};
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
                Canvas.SetLeft(puff,4+i*2.5);Canvas.SetTop(puff,18-i);
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
