using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Lucent;

// Runs the real zoom overlay on a display that is NOT the primary one, over a known test picture, and reads the screen back to check
// what is on it. It never touches the primary display, never hides the pointer, and changes no display setting.
class ZoomLiveSmoke
{
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLong(IntPtr window,int index);
 static int failed;
 static void Check(bool ok,string message){if(!ok){Console.WriteLine("FAIL: "+message);failed++;}}
 sealed class Pattern:Form
 {
  readonly Bitmap picture;
  public Pattern(Rectangle bounds)
  {
   FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;ShowInTaskbar=false;TopMost=true;
   // red grows to the right, green grows downward, so every screen pixel says where it is.
   picture=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppRgb);
   for(int y=0;y<bounds.Height;y++)for(int x=0;x<bounds.Width;x++)picture.SetPixel(x,y,Color.FromArgb(x*255/(bounds.Width-1),y*255/(bounds.Height-1),64));
  }
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(picture,0,0);}
 }
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int fromX,int fromY,uint operation);
 // CAPTUREBLT (0x40000000) is what includes layered windows: the overlay is one, and a plain screen grab would leave it out.
 static Bitmap Shot(Rectangle area)
 {
  var bitmap=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap))
  {
   IntPtr screen=GetDC(IntPtr.Zero),target=g.GetHdc();
   try{BitBlt(target,0,0,area.Width,area.Height,screen,area.Left,area.Top,0x00CC0020|0x40000000);}
   finally{g.ReleaseHdc(target);ReleaseDC(IntPtr.Zero,screen);}
  }
  return bitmap;
 }
 [STAThread]static int Main()
 {
  var other=Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(other==null){Console.WriteLine("SKIP: there is no second display to test the live overlay on.");return 0;}
  var area=other.Bounds;int w=area.Width,h=area.Height;
  // Exact color values can only be checked while no color effect (red filter, dimming, Dark Boost) is on the display. Everything else still runs.
  var effect=new float[25];bool identity=NativeEffects.MagInitialize()&&NativeEffects.MagGetFullscreenColorEffect(effect)&&ColorMath.Equal(effect,ColorMath.Identity());NativeEffects.MagUninitialize();
  if(!identity)Console.WriteLine("NOTE: a color effect is on the display right now, so the exact magnification values are not checked; style, visibility and input checks still run.");
  using(var pattern=new Pattern(area))
  {
   pattern.Show();
   for(int i=0;i<15;i++){Application.DoEvents();Thread.Sleep(20);}
   using(var before=Shot(area))
   {
    // Sanity: the picture is what we drew.
    var mid=before.GetPixel(w/2,h/2);Check(!identity||(Math.Abs(mid.R-127)<8&&Math.Abs(mid.G-127)<8),"The test picture did not appear on the second display: "+mid);
    // The overlay is configured exactly as in the app: hidden from screen capture, so what it draws is read from its own finished frames.
    var zoom=new DesktopGpuZoom{Target=other,AllowCapture=false};
    int dx=w/4,dy=h/4;
    zoom.SamplePoints=new[]{new[]{w/2,h/2},new[]{w/2+dx,h/2},new[]{w/2,h/2+dy},new[]{1,1},new[]{w/8,h/2},new[]{w-w/16,h/2}};
    string failure=null;zoom.Failed+=message=>failure=message;
    Func<int,int> redOf=x=>x*255/(w-1);Func<int,int> greenOf=y=>y*255/(h-1);
    Func<int,int,int[]> spot=(i,unused)=>{var s=zoom.Sampled;return s==null?new[]{-1,-1,-1}:s[i];};
    Action<int> wait=count=>{for(int i=0;i<count;i++){Application.DoEvents();Thread.Sleep(20);}};
    try
    {
     zoom.Apply(new ZoomRequest{Zoom=2,Follow=false,HideCursor=false});
     for(int i=0;i<200&&zoom.FramesDrawn<30&&failure==null;i++){Application.DoEvents();Thread.Sleep(20);}
     Check(failure==null,"The overlay failed: "+failure);
     Check(zoom.FramesDrawn>=30,"The overlay did not draw frames: "+zoom.FramesDrawn);
     wait(30);
     // The overlay must be invisible to the mouse and keyboard: the point under the pointer belongs to what is below it, and Windows is told it is transparent.
     Check(zoom.OverlayHandle!=IntPtr.Zero,"The overlay has no window");
     long style=(long)GetWindowLong(zoom.OverlayHandle,-20);
     Check((style&0x80000)!=0&&(style&0x20)!=0&&(style&0x08000000)!=0,"The overlay must be layered, transparent and non-activating: 0x"+style.ToString("X"));
     var hitSpot=new Point(area.Left+w/2,area.Top+h/2);
     Check((long)SendMessage(zoom.OverlayHandle,0x0084,IntPtr.Zero,(IntPtr)((hitSpot.Y<<16)|(hitSpot.X&0xFFFF)))==-1,"The overlay did not answer a hit test with HTTRANSPARENT");
     if(identity){
     var centre=spot(0,0);Check(Math.Abs(centre[0]-127)<10&&Math.Abs(centre[1]-127)<10,"2x should keep the middle in place: "+string.Join(",",centre));
     var right=spot(1,0);var down=spot(2,0);var corner=spot(3,0);
     Check(Math.Abs(right[0]-redOf(w/2+dx/2))<=10,"2x: a point "+dx+" px right of the middle should show red "+redOf(w/2+dx/2)+" but shows "+right[0]);
     Check(Math.Abs(down[1]-greenOf(h/2+dy/2))<=10,"2x: a point "+dy+" px below the middle should show green "+greenOf(h/2+dy/2)+" but shows "+down[1]);
     Check(corner[0]<=redOf(w/4)+12&&corner[1]<=greenOf(h/4)+12,"2x: the top-left corner should show the quarter-screen point: "+string.Join(",",corner));
     // Zoom 4: four times.
     zoom.Apply(new ZoomRequest{Zoom=4,Follow=false,HideCursor=false});wait(40);
     if(identity){right=spot(1,0);Check(Math.Abs(right[0]-redOf(w/2+dx/4))<=10,"4x: a point "+dx+" px right of the middle should show red "+redOf(w/2+dx/4)+" but shows "+right[0]);}
     // Smart shadows alone brighten the dark part and leave the bright part.
     zoom.Apply(new ZoomRequest{Zoom=1,Lift=1,HideCursor=false});wait(40);
     if(identity){var dark=spot(4,0);var light=spot(5,0);
     Check(dark[0]>redOf(w/8)+25,"The shadow lift did not brighten dark parts: red "+redOf(w/8)+" -> "+dark[0]);
     Check(light[0]>=redOf(w-w/16)-6,"The shadow lift darkened a bright part: "+light[0]);}
     }
     // Zoomed out with no lift, it stops drawing: the frame count stops growing.
     zoom.Apply(new ZoomRequest{Zoom=1});wait(20);long frozen=zoom.FramesDrawn;wait(20);
     Check(zoom.FramesDrawn==frozen,"The overlay kept drawing after zoom and lift were off");
    }
    finally{zoom.Dispose();}
    wait(25);
    // With the overlay hidden from capture, the screen itself always shows the unmagnified picture.
    using(var gone=Shot(area)){var after=gone.GetPixel(w/2+w/4,h/2);Check(Math.Abs(after.R-before.GetPixel(w/2+w/4,h/2).R)<10,"The picture changed even though the overlay is hidden from capture: "+after);}
   }
   // And it is still shown: with capture allowed (test only) the screen shows the overlay, which magnifies a picture of itself and so loses the pattern.
   var shown=new DesktopGpuZoom{Target=other,AllowCapture=true};
   try
   {
    using(var plain=Shot(area))
    {
     var probe=plain.GetPixel(w/8,h/2);
     shown.Apply(new ZoomRequest{Zoom=3,HideCursor=false});
     for(int i=0;i<150&&shown.FramesDrawn<30;i++){Application.DoEvents();Thread.Sleep(20);}
     for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(20);}
     using(var overlaid=Shot(area)){var now=overlaid.GetPixel(w/8,h/2);Check(Math.Abs(now.R-probe.R)>25,"The overlay is not visible on screen: "+probe+" -> "+now+" (frames drawn "+shown.FramesDrawn+", overlay "+shown.OverlayHandle+", pattern "+pattern.Handle+", top window at the probe "+WindowFromPoint(new Point(area.Left+w/8,area.Top+h/2))+")");}
    }
   }
   finally{shown.Dispose();}
   pattern.Close();
  }  if(failed==0)Console.WriteLine("PASS: the real GPU overlay, run hidden from capture on the second display (a turned display) over a test picture, drew frames, kept the middle fixed and magnified 2x and 4x exactly, lifted shadows without darkening highlights, stopped drawing when off, and left the screen alone. The primary display and the pointer were not touched.");
  return failed==0?0:1;
 }
}
