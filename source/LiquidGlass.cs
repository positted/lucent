using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Lucent
{
    // Dark liquid glass for small floating surfaces (the top bar): what is behind it is seen blurred through it, darkened; light catches its rim (brightest
    // along the top and a little along the bottom, like the edge of a thick pane), a soft sheen falls from the top left, and a soft shadow lifts it off the
    // window. The blur is a copy of only the strip behind the surface, so it stays cheap: blurring the whole window is what made settings lag.
    internal sealed class LiquidGlass:Grid
    {
        const double Bleed=28;
        readonly double radius;
        readonly Grid blur=new Grid {Margin=new Thickness(-Bleed),IsHitTestVisible=false,Effect=new BlurEffect {Radius=22,RenderingBias=RenderingBias.Performance,KernelType=KernelType.Gaussian}};
        readonly Grid glass=new Grid {IsHitTestVisible=false};
        readonly Border holder=new Border();
        Visual[] sources;VisualBrush[] brushes;
        internal LiquidGlass(double cornerRadius)
        {
            radius=cornerRadius;
            // The shadow under the glass: drawn once and kept as a picture, it never changes.
            var shadow=new Border {CornerRadius=new CornerRadius(radius),Background=new SolidColorBrush(Color.FromArgb(150,0,0,0)),IsHitTestVisible=false,Margin=new Thickness(1,3,1,-1),
                Effect=new DropShadowEffect {BlurRadius=18,ShadowDepth=4,Direction=270,Opacity=.55,Color=Colors.Black,RenderingBias=RenderingBias.Performance},CacheMode=new BitmapCache()};
            Children.Add(shadow);
            glass.Children.Add(blur);
            // Dark glass: more of the dark laid on toward the bottom.
            var tint=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(118,22,22,28),0),new GradientStop(Color.FromArgb(150,10,10,14),1)},new Point(0,0),new Point(0,1));tint.Freeze();
            glass.Children.Add(new Border {Background=tint,IsHitTestVisible=false});
            // The sheen: light falling on the glass from the top left, fading out by the middle.
            var sheen=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(34,255,255,255),0),new GradientStop(Color.FromArgb(8,255,255,255),.45),new GradientStop(Color.FromArgb(0,255,255,255),.7)},new Point(0,0),new Point(.35,1));sheen.Freeze();
            glass.Children.Add(new Border {Background=sheen,IsHitTestVisible=false});
            // A thin bright line just inside the top edge (the light the curved rim catches), and a fainter one along the bottom, as thick glass bends light at both edges.
            var top=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(0,255,255,255),0),new GradientStop(Color.FromArgb(70,255,255,255),.25),new GradientStop(Color.FromArgb(70,255,255,255),.75),new GradientStop(Color.FromArgb(0,255,255,255),1)},new Point(0,0),new Point(1,0));top.Freeze();
            glass.Children.Add(new Border {Height=1,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(radius*.6,1.5,radius*.6,0),Background=top,IsHitTestVisible=false});
            var bottom=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(0,255,255,255),0),new GradientStop(Color.FromArgb(22,255,255,255),.5),new GradientStop(Color.FromArgb(0,255,255,255),1)},new Point(0,0),new Point(1,0));bottom.Freeze();
            glass.Children.Add(new Border {Height=1,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(radius,0,radius,1.5),Background=bottom,IsHitTestVisible=false});
            Children.Add(glass);
            Children.Add(Design.GlassEdge(radius));
            Children.Add(holder);
            SizeChanged+=(s,e)=>{if(ActualWidth>0&&ActualHeight>0)glass.OpacityMask=PlayerBar.RoundedMask(ActualWidth,ActualHeight,radius);Fit();};
            LayoutUpdated+=(s,e)=>Fit();
        }
        internal UIElement Content {get{return holder.Child;}set{holder.Child=value;}}
        // What is seen through the glass: each source is copied for the strip behind the glass and blurred (the window's colors, then the moving colors on top).
        internal void SetSources(params Visual[] behind)
        {
            sources=behind;brushes=new VisualBrush[behind.Length];
            blur.Children.Clear();
            for(int i=0;i<behind.Length;i++)
            {
                brushes[i]=new VisualBrush(behind[i]) {ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};
                blur.Children.Add(new System.Windows.Shapes.Rectangle {Fill=brushes[i],IsHitTestVisible=false});
            }
        }
        internal bool BlurOn {set{blur.Visibility=value?Visibility.Visible:Visibility.Collapsed;}}
        void Fit()
        {
            if(sources==null||ActualWidth<=0||ActualHeight<=0)return;
            for(int i=0;i<sources.Length;i++)
            {
                try
                {
                    var area=TransformToVisual(sources[i]).TransformBounds(new Rect(-Bleed,-Bleed,ActualWidth+2*Bleed,ActualHeight+2*Bleed));
                    if(area.Width>0&&area.Height>0&&brushes[i].Viewbox!=area)brushes[i].Viewbox=area;
                }
                catch(InvalidOperationException){}
            }
        }
    }
}
