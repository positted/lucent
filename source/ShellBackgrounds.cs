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
    // Custom GIF backgrounds: gallery, preview, import and removal.
    internal sealed partial class Shell
    {
        StackPanel customGallery;
        void ApplyAppearance()
        {
            if(previewPopup==null)return;
            bool showGif=preferences.Artwork&&customBackgroundLoaded;
            bool animate=preferences.Motion&&!preferences.PerformanceMode&&IsVisible&&WindowState!=WindowState.Minimized&&IsActive&&!settingsOpen;
            Panel.SetZIndex(animatedBackground,1);animatedBackground.Visibility=showGif?Visibility.Visible:Visibility.Collapsed;
            animatedBackground.Opacity=double.IsNaN(preferences.BackgroundOpacity)?.4:Math.Max(.05,Math.Min(1,preferences.BackgroundOpacity));
            animatedBackground.SetPlaying(showGif&&animate);
            if(gifShade!=null)gifShade.Visibility=showGif?Visibility.Visible:Visibility.Collapsed;
            previewPopup.Visibility=previewingBackground?Visibility.Visible:Visibility.Collapsed;
            workspace.IsEnabled=!previewingBackground;
            backgroundPreview.Visibility=previewingBackground?Visibility.Visible:Visibility.Collapsed;
            backgroundPreview.Opacity=PreviewVisibility;
            backgroundPreview.SetPlaying(previewingBackground&&preferences.Motion&&IsVisible&&WindowState!=WindowState.Minimized&&IsActive);
            UpdateAmbientColor();
        }
        CheckBox artworkToggle;

        // The controls of Settings, Backgrounds: add a GIF, preview it, then use it across Lucent.
        FrameworkElement BackgroundsContent()
        {
            var content=new StackPanel();
            content.Children.Add(BackgroundControls());
            customGallery=new StackPanel();
            content.Children.Add(customGallery);
            RefreshCustomGallery();
            return content;
        }
        static System.Windows.Media.Imaging.BitmapSource LoadBackgroundThumbnail(string path)
        {
            var frame=new System.Windows.Media.Imaging.BitmapImage();
            frame.BeginInit();
            frame.UriSource=new Uri(path);
            frame.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            frame.DecodePixelWidth=440;
            frame.EndInit();

            frame.Freeze();
            return frame;
        }

        void BuildPreviewPopup()
        {
            previewPopup=new Grid
            {
                Background=new SolidColorBrush(Color.FromArgb(185,0,0,0)),
                Visibility=Visibility.Collapsed
            };
            KeyboardNavigation.SetTabNavigation(previewPopup,KeyboardNavigationMode.Cycle);
            var panel=new StackPanel {Margin=new Thickness(20)};
            previewTitle=Text("",24,true);
            panel.Children.Add(previewTitle);
            previewStage=new Grid {Width=710,Height=710*900d/1290,ClipToBounds=true,Margin=new Thickness(0,8,0,12)};
            previewStage.Background=Brushes.Black;
            previewStage.Children.Add(backgroundPreview);

            panel.Children.Add(previewStage);

            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            actions.Children.Add(Action("Cancel",EndBackgroundPreview));
            applyPreview=Action("Use background",async()=>
            {
                if(previewedBackground!=null)await UseBackground(previewedBackground);
            });
            actions.Children.Add(applyPreview);
            panel.Children.Add(actions);
            previewPopup.Children.Add(new Border
            {
                Child=panel,Background=new SolidColorBrush(Color.FromRgb(20,20,20)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(65,65,65)),BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(14),HorizontalAlignment=HorizontalAlignment.Center,
                VerticalAlignment=VerticalAlignment.Center
            });
            PreviewKeyDown+=(sender,args)=>
            {
                if(!previewingBackground||args.Key!=Key.Escape)return;
                args.Handled=true;
                EndBackgroundPreview();
            };
            // Above the settings layer, so a preview started from Settings, Backgrounds shows over it.
            Panel.SetZIndex(previewPopup,30);root.Children.Add(previewPopup);
        }
        async Task PreviewBackground(string selection)
        {
            try
            {
                if(!await backgroundPreview.LoadFileAsync(selection)||closing||!settingsOpen||settingsCategory!="Backgrounds")return;
                previewedBackground=selection;
                previewStage.Height=previewStage.Width*Math.Max(1,ActualHeight)/Math.Max(1,ActualWidth);
                previewTitle.Text=Path.GetFileName(selection)+" preview";
                ApplyAppearance();
                applyPreview.Focus();
            }
            catch(Exception error){SetStatus("Background preview could not load. "+error.Message);}
        }

        void EndBackgroundPreview()
        {
            backgroundPreview.Clear();
            if(!previewingBackground)return;
            previewedBackground=null;
            ApplyAppearance();
            SetStatus("Background preview ended");
        }
        async Task LoadBackground(string selection, bool showArtwork = false, bool usePreviewVisibility = false)
        {
            try
            {
                if(!await animatedBackground.LoadFileAsync(selection)||closing)return;
                CommitBackground(selection,showArtwork,usePreviewVisibility);
            }
            catch(Exception error){SetStatus("Background could not load. "+error.Message);}
        }
        async Task UseBackground(string selection)
        {
            if(previewedBackground==selection)
            {
                backgroundPreview.CancelLoad();
                var previous=animatedBackground;
                root.Children.Remove(previous);
                previewStage.Children.Remove(backgroundPreview);
                animatedBackground=backgroundPreview;
                backgroundPreview=previous;
                root.Children.Insert(1,animatedBackground);
                previewStage.Children.Insert(0,backgroundPreview);
                previewedBackground=null;
                backgroundPreview.Clear();
                CommitBackground(selection,true,true);
                animatedBackground.InvalidateVisual();
                return;
            }

            EndBackgroundPreview();
            SetStatus("Loading "+Path.GetFileName(selection)+"…");
            await LoadBackground(selection,true,true);
        }

        void CommitBackground(string selection,bool showArtwork,bool usePreviewVisibility)
        {
            customBackgroundLoaded=true;
            preferences.BackgroundFile=selection;
            if(showArtwork)
            {
                preferences.Artwork=true;
                artworkToggle.IsChecked=true;
            }
            if(usePreviewVisibility)
            {
                preferences.BackgroundOpacity=PreviewVisibility;
                backgroundVisibility.Value=PreviewVisibility*100;
            }
            backgroundName.Text=Path.GetFileName(selection);
            ApplyAppearance();
            UpdateBackgroundButtons();
            SaveSoon();
            if(showArtwork)SetStatus(Path.GetFileName(selection)+" background applied.");
        }

        void UpdateBackgroundButtons()
        {
            foreach(var entry in backgroundButtons)
                entry.Value.Content=customBackgroundLoaded&&preferences.Artwork&&entry.Key==preferences.BackgroundFile
                    ?"✓ Applied":"Use background";
        }
        void UseDefaultBackground()
        {
            EndBackgroundPreview();
            animatedBackground.Clear();
            customBackgroundLoaded=false;
            preferences.BackgroundFile="";
            preferences.Artwork=true;
            artworkToggle.IsChecked=true;
            backgroundName.Text="Default appearance";
            ApplyAppearance();
            UpdateBackgroundButtons();
            SaveSoon();
            SetStatus(backgroundName.Text+" applied.");
        }
        FrameworkElement BackgroundControls()
        {
            var section=Section("Animated background","Your GIFs stay on this computer. Remove a background whenever you like.",true);
            backgroundName=Text(string.IsNullOrEmpty(preferences.BackgroundFile)?("Default appearance"):Path.GetFileName(preferences.BackgroundFile),14,true);
            section.Children.Add(backgroundName);
            var actions=new WrapPanel();
            actions.Children.Add(Action("Choose GIF…",async()=>
            {
                var picker=new Microsoft.Win32.OpenFileDialog {Filter="Animated GIF|*.gif",Title="Choose a background"};
                if(picker.ShowDialog(this)==true)await ImportCustomBackground(picker.FileName);
            }));
            actions.Children.Add(Action("Use default theme",UseDefaultBackground));

            section.Children.Add(actions);
            var amount=Text("Background visibility  "+(preferences.BackgroundOpacity*100).ToString("0")+"%",14);
            section.Children.Add(amount);
            backgroundVisibility=new Slider {Minimum=5,Maximum=100,Value=preferences.BackgroundOpacity*100,Height=42,IsMoveToPointEnabled=true};
            backgroundVisibility.ValueChanged+=(sender,args)=>
            {
                preferences.BackgroundOpacity=backgroundVisibility.Value/100;
                amount.Text="Background visibility  "+backgroundVisibility.Value.ToString("0")+"%";
                ApplyAppearance();
                SaveSoon();
            };
            section.Children.Add(backgroundVisibility);
            var note=Text("Motion pauses while Lucent is hidden or inactive. Background motion also pauses GIF playback.",12);
            note.Foreground=Muted;
            section.Children.Add(note);
            return section;
        }
        FrameworkElement BackgroundCard(string selection,string name,string description,bool removable)
        {
            var card=Section(name,description,true);
            try
            {
                BitmapSource thumbnail=LoadBackgroundThumbnail(selection);
                card.Children.Add(new Border {Height=150,ClipToBounds=true,Background=Brushes.Black,Margin=new Thickness(0,5,0,12),Child=new Image {Source=thumbnail,Stretch=Stretch.UniformToFill}});
            }
            catch{card.Children.Add(Text("File unavailable — remove or import it again.",12));}
            var actions=new DockPanel();card.Children.Add(actions);
            if(removable)
            {
                var trash=Action("",()=>{});trash.Style=(Style)Resources["TrashAction"];trash.ToolTip="Remove custom background";
                System.Windows.Automation.AutomationProperties.SetName(trash,"Remove "+name);
                trash.Content=new System.Windows.Shapes.Path {Data=Geometry.Parse("M 5,7 L 6,21 18,21 19,7 M 3,5 L 21,5 M 9,5 L 9,2 15,2 15,5 M 10,9 L 10,17 M 14,9 L 14,17"),Width=18,Height=20,Stretch=Stretch.Uniform,StrokeThickness=1.5};
                ((System.Windows.Shapes.Path)trash.Content).SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){Source=trash});
                DockPanel.SetDock(trash,Dock.Right);actions.Children.Add(trash);
                trash.Click+=async(s,e)=>await RemoveCustomBackground(selection,card);
            }
            var row=new WrapPanel();actions.Children.Add(row);
            row.Children.Add(Action("Preview",async()=>await PreviewBackground(selection)));
            var use=Action("Use background",async()=>await UseBackground(selection));
            backgroundButtons[selection]=use;
            row.Children.Add(use);
            return card;
        }
        void RefreshCustomGallery()
        {
            if(customGallery==null)return;
            customGallery.Children.Clear();
            backgroundButtons.Clear();
            if(!string.IsNullOrEmpty(preferences.BackgroundFile)&&!preferences.CustomBackgrounds.Contains(preferences.BackgroundFile))preferences.CustomBackgrounds.Add(preferences.BackgroundFile);
            if(preferences.CustomBackgrounds.Count==0)return;
            customGallery.Children.Add(Text("Your backgrounds",23,true));
            var columns=new[]{new StackPanel(),new StackPanel()};
            for(int i=0;i<preferences.CustomBackgrounds.Count;i++)
            {
                string file=preferences.CustomBackgrounds[i];
                columns[i%2].Children.Add(BackgroundCard(file,Path.GetFileNameWithoutExtension(file),"Custom GIF",true));
            }
            customGallery.Children.Add(ResponsiveColumns(columns[0],columns[1]));UpdateBackgroundButtons();
        }
        async Task ImportCustomBackground(string file)
        {
            string copy=null;
            try
            {
                if(new FileInfo(file).Length>50L*1024*1024)throw new InvalidDataException("Choose a GIF smaller than 50 MB.");
                string folder=Path.Combine(StateStore.Folder,"backgrounds",Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);copy=Path.Combine(folder,Path.GetFileName(file));
                await Task.Run(()=>File.Copy(file,copy));
                if(!await animatedBackground.LoadFileAsync(copy)||closing){DeleteOwnedBackground(copy);return;}
                EndBackgroundPreview();preferences.CustomBackgrounds.Add(copy);CommitBackground(copy,true,false);RefreshCustomGallery();
            }
            catch(Exception error){if(copy!=null)DeleteOwnedBackground(copy);SetStatus("Could not import GIF: "+error.Message);}
        }
        async Task RemoveCustomBackground(string selection,FrameworkElement card)
        {
            if(!preferences.CustomBackgrounds.Contains(selection))return;
            card.IsHitTestVisible=false;
            backgroundPreview.CancelLoad();
            if(previewedBackground==selection){EndBackgroundPreview();backgroundPreview.Clear();}
            if(preferences.BackgroundFile==selection)UseDefaultBackground();
            preferences.CustomBackgrounds.Remove(selection);SaveSoon();
            var slide=new TranslateTransform();card.RenderTransform=slide;
            slide.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,24,TimeSpan.FromMilliseconds(210)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}});
            card.BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(210)));
            await Task.Delay(220);
            DeleteOwnedBackground(selection);
            RefreshCustomGallery();SetStatus("Custom background removed.");
        }
        // Deletes only copies inside the app's own folder, together with their per-import folder. The original file is never touched.
        static void DeleteOwnedBackground(string file)
        {
            string ownedRoot=Path.GetFullPath(Path.Combine(StateStore.Folder,"backgrounds"))+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(file);
            if(!path.StartsWith(ownedRoot,StringComparison.OrdinalIgnoreCase))return;
            try
            {
                if(File.Exists(path))File.Delete(path);
                string folder=Path.GetDirectoryName(path);
                if(folder.Length>ownedRoot.Length&&!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
