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
    // The Video page: display cards, vibrance, eye care, color vision and the profile summary.
    internal sealed partial class Shell
    {
        FrameworkElement VideoPage()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 5) };
            
            stack.Children.Add(Text(TabTitle("Video"), 34, true));
            var header = new WrapPanel();
            displayStatus.Text = CurrentModeText();
            displayStatus.FontSize = 14;displayStatus.Foreground=Muted;
            displayStatus.VerticalAlignment = VerticalAlignment.Center;
            displayStatus.Margin = new Thickness(0, 0, 18, 0);
            header.Children.Add(displayStatus);
            var compare = Action("Compare", () => { });
            compare.Background=Brushes.Transparent;
            compare.Content=IconLabel("Compare","Compare",13);
            compare.ToolTip = "Hold to compare with your desktop baseline";
            compare.PreviewMouseLeftButtonDown += (sender, args) =>
            {
                comparing=true;
                UpdateEffects();
            };
            compare.LostMouseCapture += (sender, args) =>
            {
                comparing=false;
                UpdateEffects();
            };
            header.Children.Add(compare);
            stack.Children.Add(header);

            var monitors = new WrapPanel { Margin = new Thickness(0, 26, 0, 16) };
            monitorCards=monitors;monitorModeLabels.Clear();
            foreach (var display in displays)
            {
                var monitor = display;
                var mode = DisplayService.GetCurrentMode(monitor.DeviceName);
                var card = Action("", () => SelectDisplay(monitor));
                card.Tag = monitor;
                card.Width=264;card.Height=68;card.Padding=new Thickness(16,7,14,7);
                card.Margin=new Thickness(0,0,14,8);card.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                var row = new DockPanel();
                var mark=new Border
                {
                    Width=22,Height=22,CornerRadius=new CornerRadius(11),Background=Brushes.White,VerticalAlignment=VerticalAlignment.Center,
                    Child=new Viewbox {Width=12,Height=12,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=new Canvas {Width=20,Height=20,Children={new System.Windows.Shapes.Path {Data=Geometry.Parse("M3,10.5 L8,15.5 L17,5"),Stroke=Brushes.Black,StrokeThickness=3.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round}}}}
                    };
                DockPanel.SetDock(mark,Dock.Right);row.Children.Add(mark);
                var monitorIcon=Art.Icon("Monitor",29);DockPanel.SetDock(monitorIcon,Dock.Left);row.Children.Add(monitorIcon);
                var labels = new StackPanel { VerticalAlignment=VerticalAlignment.Center };
                string name=monitor.MonitorName;
                if(string.IsNullOrWhiteSpace(name)||name.StartsWith("Generic",StringComparison.OrdinalIgnoreCase))
                    name="Display "+(displays.IndexOf(monitor)+1);
                if(monitor.IsPrimary)name+=" · Primary";
                labels.Children.Add(new TextBlock {Text=name,FontSize=17,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});
                var modeLabel=new TextBlock {Text=DisplayModeText(mode),FontSize=14};
                monitorModeLabels[monitor]=modeLabel;
                labels.Children.Add(modeLabel);
                row.Children.Add(labels);card.ToolTip=monitor.ToString();card.Content=row;
                StyleMonitorCard(card,monitor==selected);
                monitors.Children.Add(card);
            }
            stack.Children.Add(monitors);

            // The look controls on the left; quick looks and eye breaks fill the space on the right.
            var main = new StackPanel();
            var aside = new StackPanel();
            // Vibrance and the quick looks on one row; Eye Care and Color Blindness on the next, in the same two columns, so Eye Care lines up under Vibrance.
            var mainLow = new StackPanel();
            var asideLow = new StackPanel();
            stack.Children.Add(ResponsiveColumns(main, aside, 1.3));
            stack.Children.Add(ResponsiveColumns(mainLow, asideLow, 1.3));
            aside.Children.Add(PresetsCard());

            var vibrance = Section("Vibrance", "");
            main.Children.Add(vibrance);
            AddSlider(vibrance, "Vibrance", Look.MinimumVibrance, Look.MaximumVibrance, current.Saturation, value => current.Saturation = value, "%");

            var scale = new Grid { Margin = new Thickness(13, 0, 13, 0) };
            for (int step = 0; step < 3; step++)
            {
                var tick = Text(step==0?"100% · Neutral":step==2?"300% · Max":(100 + step * 100).ToString() + "%", 13);
                tick.Foreground = Muted;
                tick.HorizontalAlignment = step == 0 ? HorizontalAlignment.Left
                    : step == 1 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                scale.Children.Add(tick);
            }
            vibrance.Children.Add(scale);
            var care = VideoCard("Eye Care", "EyeCare");
            mainLow.Children.Add(care);
            AddSlider(care, "Red Filter", 0, 100, current.Red, value => current.Red = value, "%");
            AddSlider(care, "Screen Dim", 0, 85, current.Dim, value => current.Dim = value, "%");
            var vision = Section("Color Blindness", "");
            asideLow.Children.Add(vision);
            vision.Content.Margin=new Thickness(20,15,20,15);
            vision.Children.Clear();
            var visionRow=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            var visionTitle=IconLabel("Vision","Color Blindness",16,true);
            visionTitle.Margin=new Thickness(0,0,0,10);
            visionRow.Children.Add(visionTitle);
            var chips = new WrapPanel();
            foreach (string value in new[] { "Normal", "Protanopia", "Deuteranopia", "Tritanopia" })
            {
                string choice = value;
                var chip = Action(value, () =>
                {
                    current.Vision = choice;
                    UpdateLookChoices();
                    Changed();
                });
                chip.Tag=value;chip.Padding=new Thickness(10,9,10,9);chip.FontSize=12;
                chip.ToolTip=value=="Normal"?"No color correction":value=="Protanopia"?"Protanopia (red-blind): colors that look alike to you, like red and green, are pulled apart. A display aid, not a medical correction.":value=="Deuteranopia"?"Deuteranopia (green-blind): colors that look alike to you, like red and green, are pulled apart. A display aid, not a medical correction.":"Tritanopia (blue-blind): colors that look alike to you, like blue and green, are pulled apart. A display aid, not a medical correction.";
                StyleChoice(chip,value==current.Vision);
                visionChoices.Add(chip);
                chips.Children.Add(chip);
            }
            visionRow.Children.Add(chips);vision.Children.Add(visionRow);
            // Below the choices, how strong the aid is: appears once one is chosen.
            visionStrengthBox=new StackPanel {Margin=new Thickness(0,4,0,0)};
            AddSlider(visionStrengthBox,"Vision strength",0,100,current.VisionStrength,value=>current.VisionStrength=value,"%");
            vision.Children.Add(visionStrengthBox);
            visionStrengthBox.Visibility=StateStore.CanonicalVision(current.Vision)=="Normal"?Visibility.Collapsed:Visibility.Visible;

            if (File.Exists(Path.Combine(StateStore.Folder, "software-recovery.xml")))
            {
                Button recover=null;
                recover=Action("Recover colors", () =>
                {
                    NativeEffects.Recover();
                    recover.Visibility=Visibility.Collapsed;
                    SetStatus("Software color recovery completed");
                });
                stack.Children.Add(recover);
            }
            return stack;
        }
        void StyleMonitorCard(Button card, bool active)
        {
            card.Background=active?selectedDisplaySurface:displaySurface;
            card.BorderBrush=active?Brushes.White:new SolidColorBrush(Color.FromArgb(20,255,255,255));
            card.BorderThickness=new Thickness(0);
            card.Effect=null;
            ((DockPanel)card.Content).Children[0].Visibility=active?Visibility.Visible:Visibility.Hidden;
        }
        static void StyleChoice(Button button,bool active)
        {
            button.SetResourceReference(Control.BackgroundProperty,active?"AdaptiveSelectionBrush":"AdaptiveControlBrush");
            button.Content=(active?"✓  ":"")+(string)button.Tag;
        }
        static StackPanel IconLabel(string glyph,string title,double size,bool bold=false)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            row.Children.Add(Art.Icon(glyph,size+2));
            row.Children.Add(new TextBlock {Text=title,FontSize=Math.Max(12,size),FontWeight=bold?FontWeights.Bold:FontWeights.Medium,VerticalAlignment=VerticalAlignment.Center});
            return row;
        }
        SectionPanel VideoCard(string title,string glyph,FrameworkElement accessory=null)
        {
            var card=Section(title,"");
            var header=new DockPanel {Margin=new Thickness(0,0,0,12)};
            if(accessory!=null)
            {
                accessory.Margin=new Thickness(5,0,0,0);DockPanel.SetDock(accessory,Dock.Right);header.Children.Add(accessory);
                header.SizeChanged+=(sender,args)=>
                {
                    bool compact=args.NewSize.Width<290;
                    DockPanel.SetDock(accessory,compact?Dock.Bottom:Dock.Right);
                    accessory.Margin=compact?new Thickness(0,10,0,0):new Thickness(5,0,0,0);
                };
            }
            header.Children.Add(IconLabel(glyph,title,20,true));
            card.Children.RemoveAt(0);card.Children.Insert(0,header);
            return card;
        }
    }
}
