using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Lucent
{
    // The big player popped out of the main window. It has no owner, so it stays when Lucent is minimized.
    internal sealed class BigPlayerWindow:Window
    {
        internal readonly BigPlayerView View;

        public BigPlayerWindow(Window owner,bool testMode,Action dockBack)
        {
            Title="Lucent · Player";
            Icon=owner.Icon;
            Width=900;Height=600;MinWidth=640;MinHeight=520;
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.CanResize;
            if(testMode){WindowStartupLocation=WindowStartupLocation.Manual;Left=-20000;Top=-20000;ShowActivated=false;}
            Foreground=Brushes.White;
            FontFamily=Design.Font;
            Resources=owner.Resources;
            UseLayoutRounding=true;
            Background=new SolidColorBrush(Color.FromRgb(13,13,16));
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
            TextOptions.SetTextHintingMode(this,TextHintingMode.Auto);
            WindowChrome.SetWindowChrome(this,new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0)});
            SourceInitialized+=(s,e)=>WindowEdges.Round(this);

            var style=(Style)Resources["ChromeAction"];
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,12,0)};
            var dock=Design.WindowButton(style,"Dock",dockBack);
            dock.ToolTip="Dock back into Lucent";
            System.Windows.Automation.AutomationProperties.SetName(dock,"Dock player");
            var close=Design.WindowButton(style,"Close",Close);
            buttons.Children.Add(dock);
            buttons.Children.Add(close);
            View=new BigPlayerView(this,testMode,null,buttons,false);
            Content=View;

            // Buttons and the seek bar handle their own presses, so everywhere else drags the window.
            MouseLeftButtonDown+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed)DragMove();};
            PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
            Loaded+=(s,e)=>
            {
                if(!testMode)Place(owner);
                View.SetOpen(true);
            };
            Closed+=(s,e)=>View.Core.Stop();
        }
        // Opens over the main window, kept inside the screen it is on.
        void Place(Window owner)
        {
            var area=SystemParameters.WorkArea;
            double left=owner.IsVisible&&owner.WindowState==WindowState.Normal?owner.Left+(owner.ActualWidth-Width)/2:area.Left+(area.Width-Width)/2;
            double top=owner.IsVisible&&owner.WindowState==WindowState.Normal?owner.Top+(owner.ActualHeight-Height)/2:area.Top+(area.Height-Height)/2;
            Left=Math.Max(area.Left,Math.Min(left,area.Right-Width));
            Top=Math.Max(area.Top,Math.Min(top,area.Bottom-Height));
        }
    }
}
