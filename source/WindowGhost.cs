using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Lucent
{
    // Going to the tray and coming back from it, drawn by Windows itself: a live picture of the window (a DWM thumbnail, the same kind Alt+Tab shows) is placed
    // in a see-through window over it, and only that picture shrinks and fades, while the real window is already hidden (cloaked) from the first frame.
    // Nothing of the app is laid out or drawn again during the motion, so it starts on the click and does not depend on how busy the app is.
    // (Windows' own minimize motion is not used: it is off on many PCs, under Settings > Accessibility > Animation effects.)
    internal sealed class WindowGhost
    {
        [StructLayout(LayoutKind.Sequential)]struct Box{public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct ThumbnailProperties{public int Flags;public Box Destination;public Box Source;public byte Opacity;public int Visible;public int ClientAreaOnly;}
        [DllImport("dwmapi.dll")]static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Box box);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        const int Cloaked=13,DestinationFlag=1,OpacityFlag=4,VisibleFlag=8;
        const uint NoActivate=0x10,ShowFlag=0x40;

        Window ghost;IntPtr ghostHandle,thumbnail;int motion;EventHandler tick;
        internal bool Moving {get{return tick!=null;}}

        // Hides (or shows) a window from the screen while it stays shown to the app and to Windows, so its picture can still be taken.
        internal static void Cloak(Window window,bool on)
        {
            var handle=new WindowInteropHelper(window).EnsureHandle();
            int value=on?1:0;
            try{DwmSetWindowAttribute(handle,Cloaked,ref value,sizeof(int));}catch(Exception){}
        }
        // The see-through window is made once, ahead of time, so the first motion does not wait for it.
        internal void Prepare()
        {
            if(ghost!=null)return;
            ghost=new Window {WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,ResizeMode=ResizeMode.NoResize,
                Focusable=false,IsHitTestVisible=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000,Width=10,Height=10,Title=""};
            ghostHandle=new WindowInteropHelper(ghost).EnsureHandle();
        }
        // away: the picture shrinks a little and fades out. Otherwise it grows into place and fades in. ready runs once the picture is on the screen (the real
        // window can be hidden then without a gap: a see-through window shows nothing until it has drawn itself once). done runs when it is over.
        internal bool Play(Window window,bool away,Action ready,Action done)
        {
            Stop();
            Prepare();
            var source=new WindowInteropHelper(window).Handle;
            Box box;
            if(source==IntPtr.Zero||!GetWindowRect(source,out box)||box.Right-box.Left<40||box.Bottom-box.Top<40)return false;
            int width=box.Right-box.Left,height=box.Bottom-box.Top;
            ghost.Show();
            // Above everything (it only lives for a moment), so a window in between never covers the motion.
            SetWindowPos(ghostHandle,new IntPtr(-1),box.Left,box.Top,width,height,NoActivate|ShowFlag);
            if(DwmRegisterThumbnail(ghostHandle,source,out thumbnail)!=0){thumbnail=IntPtr.Zero;ghost.Hide();return false;}
            int mine=++motion;
            double length=away?170:210;
            Action<double> show=p=>
            {
                // p: 0 is the window whole and in place, 1 is gone.
                double scale=1-.07*p;
                int w=(int)Math.Round(width*scale),h=(int)Math.Round(height*scale);
                int x=(width-w)/2,y=(int)Math.Round((height-h)*.5+14*p);
                var properties=new ThumbnailProperties {Flags=DestinationFlag|OpacityFlag|VisibleFlag,Destination=new Box {Left=x,Top=y,Right=x+w,Bottom=y+h},Opacity=(byte)Math.Round(255*(1-p)),Visible=1};
                DwmUpdateThumbnailProperties(thumbnail,ref properties);
            };
            show(away?0:1);
            Stopwatch clock=null;int waited=0;
            tick=(s,e)=>
            {
                if(mine!=motion)return;
                if(clock==null)
                {
                    // Two frames for the see-through window to be on the screen with the picture in it; then the motion starts from its beginning.
                    if(++waited<2)return;
                    if(ready!=null)ready();
                    clock=Stopwatch.StartNew();
                    return;
                }
                double t=Math.Min(1,clock.Elapsed.TotalMilliseconds/length);
                // Away it speeds up as it goes; coming back it slows down as it arrives.
                double p=away?t*t*t:1-Math.Pow(1-t,3);
                show(away?p:1-p);
                if(t<1)return;
                Stop();
                if(done!=null)done();
            };
            CompositionTarget.Rendering+=tick;
            return true;
        }
        internal void Stop()
        {
            motion++;
            if(tick!=null){CompositionTarget.Rendering-=tick;tick=null;}
            if(thumbnail!=IntPtr.Zero){DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;}
            if(ghost!=null&&ghost.IsVisible)ghost.Hide();
        }
    }
}
