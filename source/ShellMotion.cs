using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Lucent
{
    internal sealed partial class Shell
    {
        int pageMotion;
        // The page runs up to the top of the window and scrolls under the window buttons, instead of being cut off at a bar below them. Its first line starts this far
        // down (the height of the top strip: 8 above it, the strip itself and the gap under it), so nothing sits differently when it is not scrolled.
        const double PageTop=54;
        double pageInset;
        Grid pageLayer,pageArea,pageHost;
        readonly Dictionary<string,ScrollViewer> pageSlots=new Dictionary<string,ScrollViewer>();
        readonly Dictionary<string,ContentControl> pageHolders=new Dictionary<string,ContentControl>();

        // Every page lives in a scroller of its own, attached once and kept there. Changing tabs only fades one scroller
        // out and the next one in, so nothing is laid out, re-attached or drawn for the first time during the motion.
        ScrollViewer PageSlot(string key)
        {
            ScrollViewer slot;
            if(pageSlots.TryGetValue(key,out slot))return slot;
            var holder=new ContentControl {Content=pages[key],Focusable=false,FocusVisualStyle=null,Margin=new Thickness(0,PageTop,0,pageInset)};
            slot=new ScrollViewer {Content=holder,Focusable=false,FocusVisualStyle=null,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,10,0),Visibility=Visibility.Hidden,Opacity=0,IsHitTestVisible=false,RenderTransform=new TranslateTransform()};
            pageSlots[key]=slot;
            pageHolders[key]=holder;
            pageLayer.Children.Add(slot);
            return slot;
        }
        // While the window is being resized, the pages that are not showing are left out of layout. A hidden element is still measured and arranged at every
        // change of width, so dragging the sides of the window laid out every other page again at each step (about a quarter of a second per step with six
        // pages). They come back one by one once the window has settled, so changing tabs stays quick.
        DispatcherTimer pageRestore;
        void RestPages()
        {
            if(pageRestore!=null)pageRestore.Stop();
            foreach(var slot in pageSlots.Values)
                if(!ReferenceEquals(slot,contentScroll)&&slot.Visibility==Visibility.Hidden&&slot.Opacity<.01)slot.Visibility=Visibility.Collapsed;
            // The settings, kept laid out while hidden so they open at once, are a large part of what the window lays out at every step: they too wait out the resize.
            if(settingsLayer!=null&&!settingsOpen&&settingsLayer.Visibility==Visibility.Hidden){settingsLayer.Visibility=Visibility.Collapsed;settingsRested=true;}
        }
        bool settingsRested,warmed;
        // The first time the window is dragged larger or smaller everything it does about that runs for the first time, which showed as one long stall at the start
        // of the drag. A change of 2 pixels there and back, made once while the app is idle, runs all of it beforehand.
        // The first time the lyrics or any large text is laid out in the display font, the font and its shapes are loaded, which held up the first opening of the lyrics for
        // a quarter of a second. Laying out a long line once in advance spends that while nothing is happening.
        internal static void WarmText()
        {
            foreach(var weight in new[]{FontWeights.Bold,FontWeights.Medium})
                foreach(var family in new[]{Design.DisplayFont,Design.Font})
                {
                    var probe=new TextBlock {Text="The quick brown fox jumps over the lazy dog, and then she said \"hello\" (again) \u2014 caf\u00e9, na\u00efve, 0123456789 ?!",FontFamily=family,FontWeight=weight,FontSize=30,TextWrapping=TextWrapping.Wrap,Width=360};
                    probe.Measure(new Size(360,double.PositiveInfinity));
                }
            // And a whole list of lyrics once, off screen: the first real one built (about a tenth of a second) is what made the first opening of the lyrics catch.
            var view=new LyricsView();
            view.SetLyrics(LyricsService.Parse(string.Join("\n",System.Linq.Enumerable.Range(0,24).Select(k=>"[00:"+(k*2).ToString("00")+".00] This is a line of a song that is long enough to wrap onto a second line sometimes, number "+k)),null));
            view.Measure(new Size(360,520));view.Arrange(new Rect(0,0,360,520));view.UpdateLayout();
            view.SetLyrics(null);
        }
        // The first opening of the big player did everything for the first time at once (its code, its layout, decoding the cover at full size), which showed as a
        // stall right when its opening motion should play. All of that is done once beforehand, while the app is idle and the player is not showing.
        internal void WarmBigPlayer()
        {
            if(bigPlayerView==null||bigPlayerOpen)return;
            try
            {
                if(ambientTrack!=null&&bigPlayerView.Core.Track==null)bigPlayerView.Core.SetTrack(CopyTrack(ambientTrack));
                bigPlayerView.Warm();
            }
            catch(Exception){}
        }
        internal void WarmResize()
        {
            WarmText();
            if(!IsVisible||WindowState!=WindowState.Normal||leaving||double.IsNaN(Width))return;
            double width=Width;
            Width=width+2;UpdateLayout();
            Width=width;UpdateLayout();
        }
        void WakePages()
        {
            if(pageRestore==null)
            {
                pageRestore=new DispatcherTimer(DispatcherPriority.ApplicationIdle){Interval=TimeSpan.FromMilliseconds(70)};
                pageRestore.Tick+=(s,e)=>
                {
                    foreach(var slot in pageSlots.Values)
                        if(!ReferenceEquals(slot,contentScroll)&&slot.Visibility==Visibility.Collapsed){slot.Visibility=Visibility.Hidden;pageRestore.Interval=TimeSpan.FromMilliseconds(70);return;}
                    if(settingsRested){settingsRested=false;if(settingsLayer!=null&&!settingsOpen&&settingsLayer.Visibility==Visibility.Collapsed)settingsLayer.Visibility=Visibility.Hidden;}
                    pageRestore.Stop();
                };
            }
            pageRestore.Interval=TimeSpan.FromMilliseconds(700);pageRestore.Start();
        }
        static void HideSlot(ScrollViewer slot)
        {
            slot.BeginAnimation(OpacityProperty,null);
            ((TranslateTransform)slot.RenderTransform).BeginAnimation(TranslateTransform.YProperty,null);
            slot.Opacity=0;
            if(slot.Visibility!=Visibility.Collapsed)slot.Visibility=Visibility.Hidden;
            slot.IsHitTestVisible=false;
        }
        // ---- Minimizing, going to the tray and coming back. A see-through window like this one gets no animation from Windows, so it is made here, in the window
        // itself (no picture, no second window, nothing to wait for): it shrinks toward its top left corner and fades, and then really minimizes or hides; when it
        // comes back it grows out of the corner and fades in.
        WindowState lastWindowState=WindowState.Normal;
        bool leaving;
        // Only coming back from the tray (BringToFront) is animated. From the taskbar the window simply returns: that is Windows'' own restore, and animating it lagged.
        bool animateRestore;
        // A safety net: if the window was made invisible for its return and the return never ran, it is made visible again.
        void EnsureVisibleSoon()
        {
            var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(1500)};
            timer.Tick+=(s,e)=>{timer.Stop();animateRestore=false;if(IsVisible&&Opacity<.01&&WindowState!=WindowState.Minimized){BeginAnimation(OpacityProperty,null);Opacity=1;}};
            timer.Start();
        }
        void InstallWindowMotion()
        {
            lastWindowState=WindowState;
            StateChanged+=(s,e)=>
            {
                var was=lastWindowState;lastWindowState=WindowState;
                if(TestMode||closing)return;
                if(was==WindowState.Minimized&&WindowState!=WindowState.Minimized&&animateRestore){animateRestore=false;GrowBack();}
            };
            IsVisibleChanged+=(s,e)=>
            {
                if(TestMode||closing)return;
                // Once, a moment after the window first shows (the app is idle by then): the stall at the start of the first drag of an edge is spent beforehand.
                if(IsVisible&&!warmed)
                {
                    warmed=true;
                    var warm=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(2500)};
                    warm.Tick+=(a,b)=>{warm.Stop();if(closing||!IsVisible||leaving)return;WarmText();WarmResize();WarmBigPlayer();};
                    warm.Start();
                }
                if(IsVisible&&animateRestore&&WindowState!=WindowState.Minimized){animateRestore=false;GrowBack();}
            };
        }        // Runs the leaving motion, then what follows (really minimizing, or hiding to the tray). Minimizing shrinks the window toward its top left corner; going to
        // the tray (toRight) is the exact reverse of coming back from it: the window slides to the right, shrinks a little and fades.
        // Going to the tray and coming back from it use the same distance, size, time and easing, one the mirror of the other.
        const double MotionTime=200;
        const bool NativeMotion=true;
        // While the window shrinks or grows it is drawn once into a picture on the graphics card and only that picture moves, and the drifting colors stand still,
        // so no text or card is drawn again at every step.
        void BeginMotionCache()
        {
            flowBackdrop.Hold(true);
            windowSurface.CacheMode=new BitmapCache(VisualTreeHelper.GetDpi(this).DpiScaleX){SnapsToDevicePixels=false};
        }
        void EndMotionCache()
        {
            windowSurface.CacheMode=null;
            flowBackdrop.Hold(WindowState==WindowState.Minimized);
        }
        void AnimateAway(Action then)
        {
            // Windows animates minimizing and hiding an ordinary window itself, smoothly and outside the app, so there is nothing to do here but go.
            if(NativeMotion||TestMode||preferences.PerformanceMode||closing||leaving||!IsVisible||WindowState==WindowState.Minimized){then();return;}
            leaving=true;
            BeginMotionCache();
            var scale=new ScaleTransform(1,1);
            windowSurface.RenderTransformOrigin=new Point(0,0);
            windowSurface.RenderTransform=scale;
            var span=TimeSpan.FromMilliseconds(MotionTime);
            IEasingFunction ease=new CubicEase {EasingMode=EasingMode.EaseIn};
            var shrink=new DoubleAnimation(1,.92,span){EasingFunction=ease};
            shrink.Completed+=(s,e)=>
            {
                then();
                // Put everything back while it is gone, so that coming back starts from the normal window.
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                windowSurface.RenderTransform=Transform.Identity;windowSurface.RenderTransformOrigin=new Point(0,0);BeginAnimation(OpacityProperty,null);Opacity=1;leaving=false;
                EndMotionCache();
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,shrink);scale.BeginAnimation(ScaleTransform.ScaleYProperty,shrink.Clone());
            BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,span){EasingFunction=ease});
        }        void MinimizeWithMotion(){AnimateAway(()=>WindowState=WindowState.Minimized);}
        // Coming back from the tray: the window grows from its top right corner to its place while it fades in. (Going away, minimizing or to the tray, is the same motion
        // with the same size, time and easing, but toward the top left corner.)
        void GrowBack()
        {
            var scale=new ScaleTransform(.92,.92);
            windowSurface.RenderTransformOrigin=new Point(1,0);
            windowSurface.RenderTransform=scale;
            Opacity=0;
            BeginMotionCache();
            // The window has just been shown again and its first frame takes longer than the others. The motion starts once that frame is drawn, so it plays
            // from its beginning instead of skipping ahead to catch up.
            int frames=0;EventHandler begin=null;
            begin=(s,e)=>
            {
                if(++frames<2)return;
                CompositionTarget.Rendering-=begin;
                var span=TimeSpan.FromMilliseconds(MotionTime);
                var grow=new DoubleAnimation(.92,1,span){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
                grow.Completed+=(c,d)=>{windowSurface.RenderTransform=Transform.Identity;windowSurface.RenderTransformOrigin=new Point(0,0);BeginAnimation(OpacityProperty,null);Opacity=1;EndMotionCache();};
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,grow);scale.BeginAnimation(ScaleTransform.ScaleYProperty,grow.Clone());
                BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,span){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            };
            CompositionTarget.Rendering+=begin;
        }        void ChangePage(string key,bool animate)
        {
            int motion=++pageMotion;
            // A page that has not been laid out yet (never opened, or left out while the window was resized) is laid out and drawn once before it moves.
            bool fresh=!pageSlots.ContainsKey(key)||pageSlots[key].Visibility==Visibility.Collapsed;
            var next=PageSlot(key);
            var old=ReferenceEquals(contentScroll,next)?null:contentScroll;
            contentScroll=next;
            content=pageHolders[key];
            foreach(var slot in pageSlots.Values)if(!ReferenceEquals(slot,next)&&!ReferenceEquals(slot,old))HideSlot(slot);
            var rise=(TranslateTransform)next.RenderTransform;
            // A tab left a moment ago may still be fading out; fading it back in continues from where it is.
            double start=fresh?0:Math.Max(0,Math.Min(1,next.Opacity)),lift=fresh||start<.02?10:Math.Min(10,rise.Y);
            next.BeginAnimation(OpacityProperty,null);
            rise.BeginAnimation(TranslateTransform.YProperty,null);
            next.Visibility=Visibility.Visible;
            next.IsHitTestVisible=true;
            if(old!=null)old.IsHitTestVisible=false;
            if(!animate||old==null||preferences.PerformanceMode)
            {
                next.Opacity=1;
                rise.Y=0;
                if(old!=null)HideSlot(old);
                return;
            }
            next.Opacity=start;
            rise.Y=lift;
            Action go=()=>
            {
                if(motion!=pageMotion)return;
                var fadeIn=new DoubleAnimationUsingKeyFrames();
                fadeIn.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)),new KeySpline(.2,.2,.2,1)));
                fadeIn.Completed+=(s,e)=>{if(motion==pageMotion)SettlePage();};
                next.BeginAnimation(OpacityProperty,fadeIn);
                rise.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(lift,0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
                var fadeOut=new DoubleAnimation(old.Opacity,0,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
                var leaving=old;
                fadeOut.Completed+=(s,e)=>{if(!ReferenceEquals(contentScroll,leaving))HideSlot(leaving);};
                old.BeginAnimation(OpacityProperty,fadeOut);
            };
            if(!fresh){go();return;}
            // A page opened for the first time is laid out and drawn once before it moves.
            int frames=0;
            EventHandler begin=null;
            begin=(s,e)=>{if(++frames<2)return;CompositionTarget.Rendering-=begin;go();};
            CompositionTarget.Rendering+=begin;
            var guard=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(400)};
            guard.Tick+=(s,e)=>{guard.Stop();CompositionTarget.Rendering-=begin;go();};
            guard.Start();
        }
        // Ends any page motion at once, with the current page fully shown and the others hidden.
        internal void SettlePage()
        {
            pageMotion++;
            foreach(var slot in pageSlots.Values)
            {
                if(ReferenceEquals(slot,contentScroll))
                {
                    slot.BeginAnimation(OpacityProperty,null);
                    ((TranslateTransform)slot.RenderTransform).BeginAnimation(TranslateTransform.YProperty,null);
                    slot.Opacity=1;
                    ((TranslateTransform)slot.RenderTransform).Y=0;
                    slot.Visibility=Visibility.Visible;
                    slot.IsHitTestVisible=true;
                }
                else HideSlot(slot);
            }
        }
        // Each page not on screen is attached, laid out and drawn once while the app is idle and invisible,
        // so the first visit to it is as smooth as every later one.
        void AttachPages()
        {
            var waiting=new Queue<string>(pages.Keys.Where(name=>name!="Settings"&&!pageSlots.ContainsKey(name)));
            Action step=null;
            step=()=>
            {
                if(waiting.Count==0)return;
                var slot=PageSlot(waiting.Dequeue());
                if(slot.Visibility==Visibility.Hidden&&!ReferenceEquals(slot,contentScroll))
                {
                    slot.Visibility=Visibility.Visible;
                    slot.UpdateLayout();
                    int frames=0;
                    EventHandler hide=null;
                    hide=(s,e)=>
                    {
                        if(++frames<2)return;
                        CompositionTarget.Rendering-=hide;
                        // Only put it away if nothing has started showing it in the meantime.
                        if(!ReferenceEquals(slot,contentScroll)&&slot.Opacity==0&&!slot.HasAnimatedProperties)slot.Visibility=Visibility.Hidden;
                    };
                    CompositionTarget.Rendering+=hide;
                }
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,step);
            };
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,step);
        }
    }
}
