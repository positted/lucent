using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Lucent
{
    // A thin progress bar that can be clicked or dragged to seek. It grows and shows a handle while
    // the pointer is on it; when seeking is not possible it is just a progress line.
    internal sealed class SeekBar:Grid
    {
        const double Handle=9;
        readonly Border rail=new Border {Height=4,CornerRadius=new CornerRadius(2),Background=new SolidColorBrush(Color.FromArgb(56,255,255,255)),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(1,1)};
        readonly Border fill=new Border {Height=4,CornerRadius=new CornerRadius(2),Background=Brushes.White,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false,RenderTransformOrigin=new Point(0,.5),RenderTransform=new ScaleTransform(1,1)};
        readonly Ellipse handle=new Ellipse {Width=Handle,Height=Handle,Fill=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Opacity=0,RenderTransformOrigin=new Point(.5,.5)};
        readonly TranslateTransform handleMove=new TranslateTransform();
        readonly ScaleTransform handleScale=new ScaleTransform(.4,.4);
        bool seekable,dragging;
        // Called with the fraction under the pointer while dragging, and once when it is released.
        internal Action<double> Preview,Commit;
        internal double Fraction;
        internal bool Dragging {get{return dragging;}}
        internal bool Seekable
        {
            get{return seekable;}
            set
            {
                if(seekable==value)return;
                seekable=value;
                Cursor=value?Cursors.Hand:Cursors.Arrow;
                if(!value&&dragging){dragging=false;if(IsMouseCaptured)ReleaseMouseCapture();}
                Grow(value&&(IsMouseOver||dragging));
            }
        }
        public SeekBar()
        {
            Height=18;
            Background=Brushes.Transparent;
            Children.Add(rail);
            Children.Add(fill);
            var move=new TransformGroup();
            move.Children.Add(handleScale);
            move.Children.Add(handleMove);
            handle.RenderTransform=move;
            Children.Add(handle);
            SizeChanged+=(s,e)=>Show(Fraction);
            MouseEnter+=(s,e)=>Grow(seekable);
            MouseLeave+=(s,e)=>{if(!dragging)Grow(false);};
            MouseLeftButtonDown+=(s,e)=>
            {
                if(!seekable)return;
                CaptureMouse();
                Press(e.GetPosition(this).X);
                e.Handled=true;
            };
            MouseMove+=(s,e)=>{if(dragging)Drag(e.GetPosition(this).X);};
            MouseLeftButtonUp+=(s,e)=>
            {
                if(!dragging)return;
                Up(e.GetPosition(this).X);
                e.Handled=true;
            };
            LostMouseCapture+=(s,e)=>{if(dragging){dragging=false;Grow(IsMouseOver&&seekable);}};
        }
        // Letting go commits the position first. Releasing the mouse capture raises LostMouseCapture, which cancels a drag,
        // so doing that first would throw the seek away.
        internal void Up(double x)
        {
            Release(x);
            if(IsMouseCaptured)ReleaseMouseCapture();
        }
        // Playback moves the bar; the pointer owns it while it is held down.
        internal void SetFraction(double value){if(!dragging)Show(value);}
        void Show(double value)
        {
            Fraction=Math.Max(0,Math.Min(1,double.IsNaN(value)?0:value));
            // The filled part is given its width (not squeezed with a scale), so its start keeps its round end.
            fill.Width=Math.Max(0,Fraction*ActualWidth);
            handleMove.X=Fraction*ActualWidth-Handle/2;
        }
        double At(double x){return ActualWidth<=0?0:Math.Max(0,Math.Min(1,x/ActualWidth));}
        internal void Press(double x)
        {
            if(!seekable||ActualWidth<=0)return;
            dragging=true;
            Show(At(x));
            Grow(true);
            if(Preview!=null)Preview(Fraction);
        }
        internal void Drag(double x)
        {
            if(!dragging)return;
            Show(At(x));
            if(Preview!=null)Preview(Fraction);
        }
        internal void Release(double x)
        {
            if(!dragging)return;
            dragging=false;
            Show(At(x));
            if(Commit!=null)Commit(Fraction);
            Grow(IsMouseOver&&seekable);
        }
        // The bar thickens and its handle shows while the pointer is over it, at once (nothing eases).
        void Grow(bool on)
        {
            foreach(var transform in new[]{(ScaleTransform)rail.RenderTransform,(ScaleTransform)fill.RenderTransform})
            {transform.BeginAnimation(ScaleTransform.ScaleYProperty,null);transform.ScaleY=on?1.3:1;}
            handleScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);handleScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
            handleScale.ScaleX=handleScale.ScaleY=on?1:.4;
            handle.BeginAnimation(OpacityProperty,null);handle.Opacity=on?1:0;
        }    }
}
