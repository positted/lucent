using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ResolutionSwitcher;

namespace Lucent
{
    // The Crosshair page: ready-made styles, a builder for your own, a library of saved looks and shareable codes.
    internal sealed partial class Shell
    {
        readonly List<Action> crosshairViews=new List<Action>(),crosshairSync=new List<Action>();
        bool crosshairSyncing;
        Image crosshairPreview;
        TextBlock crosshairInfo;
        WrapPanel savedCrosshairs;
        readonly List<KeyValuePair<Border,string>> savedTiles=new List<KeyValuePair<Border,string>>();
        CrosshairSettings CrosshairLook {get{return preferences.Crosshair;}}

        FrameworkElement CrosshairPage()
        {
            crosshairViews.Clear();crosshairSync.Clear();savedTiles.Clear();
            // Building the controls reads values back into them; none of that is a change by the user.
            crosshairSyncing=true;
            var page=new StackPanel {Margin=new Thickness(0,16,0,5)};
            page.Children.Add(Text("Crosshair",38,true));
            var sub=Text("Build your own crosshair. The one on screen follows every change.",14);
            sub.Foreground=Muted;sub.Margin=new Thickness(0,6,0,22);
            page.Children.Add(sub);
            // The preview and the saved crosshairs stay on the left; the folders that shape it, and the share codes, sit beside them.
            // On a narrow window everything stacks into one column.
            var left=new StackPanel();
            left.Children.Add(CrosshairPreviewCard());
            left.Children.Add(CrosshairLibrary());
            var right=new StackPanel();
            right.Children.Add(CrosshairShapeCard());
            right.Children.Add(CrosshairLookCard());
            right.Children.Add(CrosshairCodes());
            page.Children.Add(ResponsiveColumns(left,right,.8));
            crosshairSyncing=false;
            RefreshCrosshairViews();
            return page;
        }
        void RefreshCrosshairViews(){foreach(var view in crosshairViews.ToArray())view();}
        // Every change updates the previews, is saved, and reaches the crosshair on screen at once.
        void CrosshairChanged(bool custom)
        {
            if(crosshairSyncing)return;
            if(custom)CrosshairLook.Name="Custom";
            RefreshCrosshairViews();
            SaveSoon();
            if(crosshair!=null)ConfigureCrosshair();
        }
        // After a style, a saved look or an import replaces the settings, every control shows the new values.
        void SyncCrosshairControls()
        {
            crosshairSyncing=true;
            try{foreach(var sync in crosshairSync.ToArray())sync();}
            finally{crosshairSyncing=false;}
        }
        // Just the crosshair on a plain dark stage, and the button that puts it on the screen.
        FrameworkElement CrosshairPreviewCard()
        {
            var card=Section("Preview","");
            var stage=new Border {Height=150,CornerRadius=new CornerRadius(12),Margin=new Thickness(0,8,0,12),ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(22,22,24))};
            crosshairPreview=PixelPicture();
            stage.Child=crosshairPreview;
            card.Children.Add(stage);
            crosshairButton=Action(crosshair==null?"Turn crosshair on":"Turn crosshair off",ToggleCrosshair);
            card.Children.Add(crosshairButton);
            card.Children.Add(CrosshairMonitorChoices());
            crosshairViews.Add(()=>
            {
                var spec=CrosshairLook;
                int side=CrosshairRenderer.Extent(spec)*2+1,zoom=Math.Max(1,Math.Min(4,110/side));
                crosshairPreview.Source=RenderCrosshairPreview(spec,side);
                crosshairPreview.Width=crosshairPreview.Height=side*zoom;
            });
            return card;
        }
        // A card that opens and closes like the Outline one, with its own arrow.
        FrameworkElement Folder(string title,FrameworkElement body,bool open)
        {
            var folder=new Expander {Header=title,Content=body,IsExpanded=open,Foreground=Brushes.White,FontSize=14,Style=(Style)Resources["FolderExpander"],Tag="Folder"};
            return new Border {Background=cardSurface,CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,0,14),Child=Design.Glass(folder,16),Tag=title};
        }
        Image PixelPicture()
        {
            var picture=new Image {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.NearestNeighbor);
            return picture;
        }
        // A small picture of a look in a dark box, drawn without moving it off center.
        void ShowCrosshairPicture(Image picture,CrosshairSettings spec,int box)
        {
            int side=CrosshairRenderer.Extent(spec)*2+1,zoom=Math.Max(1,Math.Min(3,box/side));
            picture.Source=RenderCrosshairPreview(spec,side);
            picture.Width=picture.Height=side*zoom;
        }
        FrameworkElement CrosshairLibrary()
        {
            var card=new StackPanel();
            var save=new DockPanel {Margin=new Thickness(0,6,0,6)};
            var nameBox=new TextBox {MinHeight=36,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,10,4),MaxLength=24,ToolTip="Name this look"};
            System.Windows.Automation.AutomationProperties.SetName(nameBox,"Crosshair name");
            var saveButton=Action("Save current",()=>
            {
                string name=nameBox.Text.Trim();
                if(name.Length==0)name="My crosshair "+(preferences.SavedCrosshairs.Count+1);
                var copy=CrosshairLook.Copy();
                copy.Name=name;copy.Clamp();
                int at=preferences.SavedCrosshairs.FindIndex(x=>x.Name==copy.Name);
                if(at>=0)preferences.SavedCrosshairs[at]=copy;
                else
                {
                    if(preferences.SavedCrosshairs.Count>=24){SetStatus("You can keep up to 24 crosshairs. Delete one first.");return;}
                    preferences.SavedCrosshairs.Add(copy);
                }
                CrosshairLook.Name=copy.Name;
                nameBox.Text="";
                SaveSoon();
                RebuildSavedCrosshairs();
                RefreshCrosshairViews();
                SetStatus("Saved "+copy.Name);
            });
            DockPanel.SetDock(saveButton,Dock.Right);
            save.Children.Add(saveButton);
            save.Children.Add(nameBox);
            card.Children.Add(save);
            savedCrosshairs=new WrapPanel();
            card.Children.Add(savedCrosshairs);
            RebuildSavedCrosshairs();
            crosshairViews.Add(()=>
            {
                foreach(var tile in savedTiles)tile.Key.Background=tile.Value==CrosshairLook.Name?selectionSurface:optionSurface;
            });
            return Folder("Your crosshairs",card,false);
        }
        // Copying and pasting a crosshair as a short code, at the bottom of the page.
        FrameworkElement CrosshairCodes()
        {
            var card=new StackPanel();
            var hint=Text("Share a look as a short code, or paste a friend's.",13);
            hint.Foreground=Muted;
            card.Children.Add(hint);
            var share=new DockPanel {Margin=new Thickness(0,8,0,0)};
            var codeBox=new TextBox {MinHeight=36,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,10,4),ToolTip="Paste a crosshair code here, or copy yours"};
            System.Windows.Automation.AutomationProperties.SetName(codeBox,"Crosshair code");
            var copyButton=Action("Copy my code",()=>
            {
                string code=CrosshairCode.Encode(CrosshairLook);
                codeBox.Text=code;
                if(TestMode)return;
                try{Clipboard.SetText(code);SetStatus("Crosshair code copied");}
                catch(System.Runtime.InteropServices.ExternalException){SetStatus("The clipboard is busy. The code is in the box.");}
            });
            var importButton=Action("Import",()=>
            {
                var imported=CrosshairCode.Decode(codeBox.Text);
                if(imported==null){SetStatus("That is not a crosshair code.");return;}
                preferences.Crosshair=imported;
                codeBox.Text="";
                SyncCrosshairControls();
                CrosshairChanged(false);
                SetStatus("Imported "+imported.Name);
            });
            DockPanel.SetDock(copyButton,Dock.Left);
            DockPanel.SetDock(importButton,Dock.Right);
            copyButton.Margin=new Thickness(0,4,10,4);
            share.Children.Add(copyButton);
            share.Children.Add(importButton);
            share.Children.Add(codeBox);
            card.Children.Add(share);
            return Folder("Crosshair codes",card,false);
        }        void RebuildSavedCrosshairs()
        {
            savedCrosshairs.Children.Clear();savedTiles.Clear();
            if(preferences.SavedCrosshairs.Count==0)
            {
                var empty=Text("Nothing saved yet. Build a look, name it, and press Save current.",12);
                empty.Foreground=Muted;
                savedCrosshairs.Children.Add(empty);
                return;
            }
            foreach(var saved in preferences.SavedCrosshairs.ToArray())
            {
                var item=saved;
                var picture=PixelPicture();
                var shown=item.Copy();shown.OffsetX=shown.OffsetY=0;
                ShowCrosshairPicture(picture,shown,56);
                var box=new Border {Width=64,Height=64,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(Color.FromArgb(80,0,0,0)),Child=picture,HorizontalAlignment=HorizontalAlignment.Center};
                var caption=new TextBlock {Text=item.Name,FontSize=12,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,2)};
                var delete=Action("Delete",()=>
                {
                    preferences.SavedCrosshairs.Remove(item);
                    SaveSoon();
                    RebuildSavedCrosshairs();
                });
                delete.Style=(Style)Resources["TextAction"];delete.FontSize=11;delete.MinHeight=0;delete.Padding=new Thickness(4,2,4,0);delete.Margin=new Thickness(0);
                delete.HorizontalAlignment=HorizontalAlignment.Center;
                System.Windows.Automation.AutomationProperties.SetName(delete,"Delete "+item.Name);
                var content=new StackPanel();
                content.Children.Add(box);content.Children.Add(caption);content.Children.Add(delete);
                var tile=new Border {Width=116,CornerRadius=new CornerRadius(12),Padding=new Thickness(8,10,8,8),Margin=new Thickness(0,0,8,8),Cursor=Cursors.Hand,Tag=item.Name,Child=content,Background=item.Name==CrosshairLook.Name?selectionSurface:optionSurface};
                tile.MouseLeftButtonUp+=(s,e)=>
                {
                    preferences.Crosshair=item.Copy();
                    SyncCrosshairControls();
                    CrosshairChanged(false);
                };
                savedTiles.Add(new KeyValuePair<Border,string>(tile,item.Name));
                savedCrosshairs.Children.Add(tile);
            }
        }
        // One setting as a labeled slider that follows the look and changes it.
        FrameworkElement CrosshairSlider(string label,int min,int max,Func<int> get,Action<int> set,string suffix)
        {
            var row=new Grid {Margin=new Thickness(0,1,0,1)};
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(104)});
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(54)});
            var caption=new TextBlock {Text=label,FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var slider=new Slider {Minimum=min,Maximum=max,Value=get(),SmallChange=1,LargeChange=Math.Max(1,(max-min)/10),TickFrequency=1,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,Height=34,Margin=new Thickness(6,0,10,0),VerticalAlignment=VerticalAlignment.Center};
            var number=new TextBlock {Text=get()+suffix,FontSize=13,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            System.Windows.Automation.AutomationProperties.SetName(slider,"Crosshair "+label.ToLowerInvariant());
            slider.ValueChanged+=(s,e)=>
            {
                if(crosshairSyncing)return;
                int value=(int)Math.Round(slider.Value);
                number.Text=value+suffix;
                if(value==get())return;
                set(value);
                CrosshairChanged(true);
            };
            crosshairSync.Add(()=>{slider.Value=get();number.Text=get()+suffix;});
            row.Children.Add(caption);
            Grid.SetColumn(slider,1);row.Children.Add(slider);
            Grid.SetColumn(number,2);row.Children.Add(number);
            return row;
        }
        CheckBox CrosshairToggle(string label,Func<bool> get,Action<bool> set)
        {
            var box=Toggle(label,get(),value=>{set(value);CrosshairChanged(true);});
            box.Margin=new Thickness(0,8,0,6);
            crosshairSync.Add(()=>box.IsChecked=get());
            return box;
        }
        Button ArmChip(string label,Func<bool> get,Action<bool> set)
        {
            var chip=Action("",()=>{set(!get());CrosshairChanged(true);});
            chip.Padding=new Thickness(12,8,12,8);chip.FontSize=12;chip.Margin=new Thickness(0,0,6,6);
            System.Windows.Automation.AutomationProperties.SetName(chip,label+" arm");
            Action paint=()=>{chip.Content=(get()?"✓  ":"")+label;chip.Background=get()?selectionSurface:optionSurface;};
            chip.Click+=(s,e)=>paint();
            crosshairSync.Add(paint);
            paint();
            return chip;
        }
        FrameworkElement CrosshairShapeCard()
        {
            var card=new StackPanel();
            var arms=new WrapPanel {Margin=new Thickness(0,6,0,4)};
            arms.Children.Add(ArmChip("Top",()=>CrosshairLook.Top,v=>CrosshairLook.Top=v));
            arms.Children.Add(ArmChip("Right",()=>CrosshairLook.Right,v=>CrosshairLook.Right=v));
            arms.Children.Add(ArmChip("Bottom",()=>CrosshairLook.Bottom,v=>CrosshairLook.Bottom=v));
            arms.Children.Add(ArmChip("Left",()=>CrosshairLook.Left,v=>CrosshairLook.Left=v));
            card.Children.Add(arms);
            card.Children.Add(CrosshairSlider("Arm length",0,40,()=>CrosshairLook.Length,v=>CrosshairLook.Length=v,""));
            card.Children.Add(CrosshairSlider("Thickness",1,10,()=>CrosshairLook.Thickness,v=>CrosshairLook.Thickness=v,""));
            card.Children.Add(CrosshairSlider("Gap",0,30,()=>CrosshairLook.Gap,v=>CrosshairLook.Gap=v,""));
            card.Children.Add(CrosshairSlider("Turn",0,45,()=>CrosshairLook.Rotation,v=>CrosshairLook.Rotation=v,"°"));
            card.Children.Add(CrosshairToggle("Center dot",()=>CrosshairLook.Dot,v=>CrosshairLook.Dot=v));
            card.Children.Add(CrosshairSlider("Dot size",1,12,()=>CrosshairLook.DotSize,v=>CrosshairLook.DotSize=v,""));
            card.Children.Add(CrosshairToggle("Square dot",()=>CrosshairLook.SquareDot,v=>CrosshairLook.SquareDot=v));
            return Folder("Shape",card,true);
        }
        FrameworkElement CrosshairLookCard()
        {
            var card=new StackPanel();
            card.Children.Add(CrosshairSlider("Opacity",20,100,()=>CrosshairLook.Opacity,v=>CrosshairLook.Opacity=v,"%"));
            card.Children.Add(CrosshairSlider("Offset X",-80,80,()=>CrosshairLook.OffsetX,v=>CrosshairLook.OffsetX=v,""));
            card.Children.Add(CrosshairSlider("Offset Y",-80,80,()=>CrosshairLook.OffsetY,v=>CrosshairLook.OffsetY=v,""));
            var color=Text("Color",15,true);
            color.Margin=new Thickness(0,14,0,4);
            card.Children.Add(color);
            card.Children.Add(BuildColorEditor(()=>ParseColor(CrosshairLook.FillColor,Colors.White),value=>CrosshairLook.FillColor=CrosshairColors.Format(ToDrawing(value)),()=>CrosshairChanged(true),crosshairSync));
            var outlineBody=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var outlineEditor=BuildColorEditor(()=>ParseColor(CrosshairLook.OutlineColor,Colors.Black),value=>CrosshairLook.OutlineColor=CrosshairColors.Format(ToDrawing(value)),()=>CrosshairChanged(true),crosshairSync);
            var outlineToggle=Toggle("Show outline",CrosshairLook.Outline,value=>
            {
                CrosshairLook.Outline=value;
                outlineEditor.Visibility=value?Visibility.Visible:Visibility.Collapsed;
                CrosshairChanged(true);
            });
            crosshairSync.Add(()=>{outlineToggle.IsChecked=CrosshairLook.Outline;outlineEditor.Visibility=CrosshairLook.Outline?Visibility.Visible:Visibility.Collapsed;});
            outlineEditor.Visibility=CrosshairLook.Outline?Visibility.Visible:Visibility.Collapsed;
            outlineBody.Children.Add(outlineToggle);
            outlineBody.Children.Add(CrosshairSlider("Outline width",1,3,()=>CrosshairLook.OutlineThickness,v=>CrosshairLook.OutlineThickness=v,""));
            outlineBody.Children.Add(outlineEditor);
            card.Children.Add(new Expander {Header="Outline",Content=outlineBody,Margin=new Thickness(0,16,0,4),Foreground=Brushes.White,FontSize=16,Style=(Style)Resources["InlineExpander"]});
            return Folder("Appearance",card,false);
        }

        FrameworkElement BuildColorEditor(Func<Color> getColor,Action<Color> setColor,Action changed,List<Action> sync=null)
        {
            var host=new StackPanel();
            var layout=new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(255)});
            host.Children.Add(layout);
            var field=new CrosshairColorField {Margin=new Thickness(0,0,24,0)};
            layout.Children.Add(field);
            var inputs=new StackPanel();Grid.SetColumn(inputs,1);layout.Children.Add(inputs);
            var sample=new Border {Height=24,CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,0,8)};
            inputs.Children.Add(sample);
            var channels=new UniformGrid {Columns=3};inputs.Children.Add(channels);
            var boxes=new List<TextBox>();
            foreach(string channel in new[]{"R","G","B"})
            {
                var column=new StackPanel {Margin=new Thickness(0,0,6,0)};column.Children.Add(Text(channel,11));
                var box=new TextBox {MaxLength=3,Padding=new Thickness(8),FontSize=14};
                System.Windows.Automation.AutomationProperties.SetName(box,"Crosshair "+channel);
                column.Children.Add(box);channels.Children.Add(column);boxes.Add(box);
            }
            var hex=new TextBox {MaxLength=7,Margin=new Thickness(0,8,6,0),Padding=new Thickness(8),FontSize=14};
            System.Windows.Automation.AutomationProperties.SetName(hex,"Crosshair hex color");inputs.Children.Add(hex);
            bool syncing=false;
            Action<Color,bool> applyColor=(color,fromField)=>
            {
                syncing=true;
                setColor(color);
                boxes[0].Text=color.R.ToString();
                boxes[1].Text=color.G.ToString();
                boxes[2].Text=color.B.ToString();
                hex.Text="#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");
                foreach(var box in boxes)box.BorderBrush=new SolidColorBrush(Color.FromRgb(68,68,68));
                hex.BorderBrush=new SolidColorBrush(Color.FromRgb(68,68,68));
                sample.Background=new SolidColorBrush(color);if(!fromField)field.SetColor(color);
                syncing=false;changed();
            };
            field.Changed+=color=>applyColor(color,true);
            Action commitRgb=()=>
            {
                byte r,g,b;
                if(byte.TryParse(boxes[0].Text,out r)&&byte.TryParse(boxes[1].Text,out g)&&byte.TryParse(boxes[2].Text,out b))applyColor(Color.FromRgb(r,g,b),false);
                else applyColor(getColor(),false);
            };
            foreach(var box in boxes)
            {
                box.LostKeyboardFocus+=(sender,args)=>{if(!syncing)commitRgb();};
                box.KeyDown+=(sender,args)=>{if(args.Key==Key.Enter){commitRgb();args.Handled=true;}};
            }
            Action commitHex=()=>
            {
                string value=hex.Text.Trim().TrimStart('#');int rgb;
                if(value.Length==6&&int.TryParse(value,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out rgb))
                    applyColor(Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb),false);
                else applyColor(getColor(),false);
            };
            hex.LostKeyboardFocus+=(sender,args)=>{if(!syncing)commitHex();};
            hex.KeyDown+=(sender,args)=>{if(args.Key==Key.Enter){commitHex();args.Handled=true;}};
            var swatches=new WrapPanel {Margin=new Thickness(0,10,0,0)};host.Children.Add(swatches);
            foreach(var color in new[]{Colors.White,Colors.Black,Colors.Lime,Colors.Cyan,Colors.Yellow,Colors.Magenta,Colors.Red})
            {
                Color chosen=color;var swatch=Action("",()=>applyColor(chosen,false));
                swatch.Content=new Border {Width=22,Height=22,CornerRadius=new CornerRadius(11),Background=new SolidColorBrush(chosen)};
                swatch.Padding=new Thickness(7);swatch.ToolTip=chosen.ToString();
                System.Windows.Automation.AutomationProperties.SetName(swatch,"Crosshair color "+chosen.ToString());
                swatches.Children.Add(swatch);
            }
            if(sync!=null)sync.Add(()=>applyColor(getColor(),false));
            applyColor(getColor(),false);return host;
        }
        // The look drawn at its own size in its own bitmap, ready to enlarge without smoothing.
        static ImageSource RenderCrosshairPreview(CrosshairSettings spec,int side)
        {
            using(var bitmap=new System.Drawing.Bitmap(side,side))
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            using(var stream=new MemoryStream())
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                var centered=spec.Copy();
                centered.OffsetX=centered.OffsetY=0;
                CrosshairRenderer.Draw(graphics,new System.Drawing.Rectangle(0,0,side,side),centered);
                bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                stream.Position=0;
                var frame=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
        }
        static Color ParseColor(string value,Color fallback)
        {
            var parsed=CrosshairColors.Parse(value,System.Drawing.Color.FromArgb(fallback.R,fallback.G,fallback.B));
            return Color.FromRgb(parsed.R,parsed.G,parsed.B);
        }
        static System.Drawing.Color ToDrawing(Color color){return System.Drawing.Color.FromArgb(color.R,color.G,color.B);}
        void ConfigureCrosshair(){crosshair.Configure(CrosshairLook);}
        // With more than one display, a choice of which one the crosshair is drawn on.
        readonly List<Button> crosshairMonitorButtons=new List<Button>();
        FrameworkElement CrosshairMonitorChoices()
        {
            var box=new StackPanel {Margin=new Thickness(0,14,0,0)};
            crosshairMonitorButtons.Clear();
            var screens=System.Windows.Forms.Screen.AllScreens;
            if(screens.Length<2)return box;
            box.Children.Add(Step("Show the crosshair on"));
            var row=new WrapPanel();
            for(int i=0;i<screens.Length;i++)
            {
                var screen=screens[i];string device=screen.DeviceName;
                var chip=Action("Display "+(i+1)+(screen.Primary?" (main)":"")+"  \u00B7  "+screen.Bounds.Width+" \u00D7 "+screen.Bounds.Height,()=>ChooseCrosshairMonitor(device));
                chip.Tag=device;chip.Padding=new Thickness(16,8,16,8);chip.FontSize=13;chip.MinHeight=0;chip.Margin=new Thickness(0,0,6,6);
                System.Windows.Automation.AutomationProperties.SetName(chip,"Crosshair on display "+(i+1));
                crosshairMonitorButtons.Add(chip);row.Children.Add(chip);
            }
            box.Children.Add(row);
            ShowCrosshairMonitor();
            return box;
        }
        // The saved display, or the main one when none is saved or it is not connected.
        string ChosenCrosshairMonitor()
        {
            var screens=System.Windows.Forms.Screen.AllScreens;
            foreach(var screen in screens)if(string.Equals(screen.DeviceName,preferences.CrosshairMonitor,StringComparison.OrdinalIgnoreCase))return screen.DeviceName;
            foreach(var screen in screens)if(screen.Primary)return screen.DeviceName;
            return "";
        }
        void ShowCrosshairMonitor(){string chosen=ChosenCrosshairMonitor();foreach(var chip in crosshairMonitorButtons)StyleChoice(chip,Equals(chip.Tag,chosen));}
        internal void ChooseCrosshairMonitor(string device)
        {
            preferences.CrosshairMonitor=device;
            SaveSoon();
            if(crosshair!=null)crosshair.SetMonitor(device);
            ShowCrosshairMonitor();
            SetStatus("Crosshair moved to that display");
        }
        Button crosshairButton;
        void ToggleCrosshair()
        {
            if(TestMode)return;
            if(crosshair==null){crosshair=new CrosshairOverlay();crosshair.SetMonitor(preferences.CrosshairMonitor);ConfigureCrosshair();crosshair.Show();Count(c=>c.CrosshairUses++);SetStatus("Crosshair on");}
            else{crosshair.Dispose();crosshair=null;SetStatus("Crosshair off");}
            crosshairButton.Content=crosshair==null?"Turn crosshair on":"Turn crosshair off";
            UpdateHome();
        }
    }
}
