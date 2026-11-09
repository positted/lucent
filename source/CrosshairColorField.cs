using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Lucent
{
    // Inline saturation/value field and hue strip. RGB inputs provide keyboard access.
    internal sealed class CrosshairColorField : FrameworkElement
    {
        double hue, saturation, brightness=1;
        bool draggingHue;
        public event Action<Color> Changed;
        public CrosshairColorField(){Height=184;MinWidth=150;Cursor=Cursors.Cross;}
        public static Color FromHsv(double h,double s,double v)
        {
            double c=v*s, x=c*(1-Math.Abs((h/60)%2-1)), m=v-c;
            double r=0,g=0,b=0;
            if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}
            else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}
            return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
        }
        public void SetColor(Color color)
        {
            double r=color.R/255.0,g=color.G/255.0,b=color.B/255.0;
            double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
            brightness=max;saturation=max==0?0:d/max;
            if(d>0){hue=max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);if(hue<0)hue+=360;}
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            double w=Math.Max(1,ActualWidth-12),h=142;
            var rect=new Rect(6,6,w,h);
            dc.PushClip(new RectangleGeometry(rect,9,9));
            dc.DrawRectangle(new SolidColorBrush(FromHsv(hue,1,1)),null,rect);
            dc.DrawRectangle(new LinearGradientBrush(Colors.White,Colors.Transparent,0),null,rect);
            dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent,Colors.Black,90),null,rect);
            dc.Pop();
            var spectrum=new LinearGradientBrush();spectrum.StartPoint=new Point(0,0);spectrum.EndPoint=new Point(1,0);
            for(int i=0;i<=6;i++)spectrum.GradientStops.Add(new GradientStop(FromHsv(i==6?0:i*60,1,1),i/6.0));
            dc.DrawRoundedRectangle(spectrum,null,new Rect(6,164,w,12),6,6);
            var p=new Point(6+saturation*w,6+(1-brightness)*h);
            dc.DrawEllipse(null,new Pen(Brushes.Black,4),p,6,6);
            dc.DrawEllipse(null,new Pen(Brushes.White,2),p,6,6);
            dc.DrawEllipse(new SolidColorBrush(FromHsv(hue,1,1)),new Pen(Brushes.White,2),new Point(6+hue/360*w,170),8,8);
        }
        void Pick(Point p)
        {
            if(draggingHue)hue=Math.Max(0,Math.Min(359.999,(p.X-6)/Math.Max(1,ActualWidth-12)*360));
            else{saturation=Math.Max(0,Math.Min(1,(p.X-6)/Math.Max(1,ActualWidth-12)));brightness=1-Math.Max(0,Math.Min(1,(p.Y-6)/142));}
            InvalidateVisual();if(Changed!=null)Changed(FromHsv(hue,saturation,brightness));
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            draggingHue=e.GetPosition(this).Y>154;
            CaptureMouse();
            Pick(e.GetPosition(this));
            e.Handled=true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)Pick(e.GetPosition(this));
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(IsMouseCaptured)
            {
                Pick(e.GetPosition(this));
                ReleaseMouseCapture();
                e.Handled=true;
            }
        }
    }
}
