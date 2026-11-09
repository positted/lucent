using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Lucent;

// Runs the real sound radar window on a display that is NOT the primary one, with fixed levels instead of the speakers, and reads the screen
// back. It checks the window is visible, lets the mouse through, and draws the arc on the side the sound is on. Nothing is played or heard.
class RadarLiveSmoke
{
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
 static int failed;
 static void Check(bool ok,string message){if(!ok){Console.WriteLine("FAIL: "+message);failed++;}}
 sealed class Backdrop:Form
 {
  public Backdrop(Rectangle b){FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=b;ShowInTaskbar=false;TopMost=true;BackColor=Color.FromArgb(20,40,70);}
  protected override bool ShowWithoutActivation {get{return true;}}
 }
 static Color Pixel(Rectangle area,int x,int y)
 {
  using(var shot=new Bitmap(1,1,PixelFormat.Format32bppArgb))
  {
   using(var g=Graphics.FromImage(shot))g.CopyFromScreen(area.Left+x,area.Top+y,0,0,new Size(1,1));
   return shot.GetPixel(0,0);
  }
 }
 [STAThread]static int Main()
 {
  var other=Screen.AllScreens.FirstOrDefault(s=>!s.Primary);
  if(other==null){Console.WriteLine("SKIP: there is no second display to test the live radar on.");return 0;}
  var area=other.Bounds;
  using(var back=new Backdrop(area))
  {
   back.Show();for(int i=0;i<15;i++){Application.DoEvents();Thread.Sleep(20);}
   var radar=new SoundRadar{Target=other,FixedLevels=new double[]{0,0,1,0,0,0,0,0}};
   string failure=null;radar.Failed+=message=>failure=message;
   try
   {
    radar.Apply(new RadarOptions{Sensitivity=100,Size="Medium",Place="Center"});
    for(int i=0;i<150&&radar.Frames<1&&failure==null;i++){Application.DoEvents();Thread.Sleep(20);}
    Check(failure==null,"The radar failed: "+failure);
    Check(radar.Frames>=1&&radar.OverlayHandle!=IntPtr.Zero,"The radar drew nothing");
    for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(20);}
    long style=(long)GetWindowLong(radar.OverlayHandle,-20);
    Check((style&0x80000)!=0&&(style&0x20)!=0&&(style&0x08000000)!=0,"The radar window must be layered, transparent and non-activating: 0x"+style.ToString("X"));
    Check((long)SendMessage(radar.OverlayHandle,0x0084,IntPtr.Zero,(IntPtr)0)==-1,"The radar must answer a hit test with HTTRANSPARENT");
    // The arc for a sound on the right sits at the right edge of the radar, halfway down; the left edge is untouched.
    int size=240,x=radar.Origin.X-area.Left,y=radar.Origin.Y-area.Top;
    // The arc swells over a moment (it follows the level smoothly), so give it a few seconds to reach full strength before judging it.
    var rightArc=Pixel(area,x+size-14,y+size/2);
    for(int i=0;i<150&&!(rightArc.R>150&&rightArc.G>100);i++){Application.DoEvents();Thread.Sleep(20);rightArc=Pixel(area,x+size-14,y+size/2);}
    var leftSpot=Pixel(area,x+14,y+size/2);var topSpot=Pixel(area,x+size/2,y+14);var outside=Pixel(area,x+size/2,y+size/2);
    var backdrop=Color.FromArgb(20,40,70);
    Check(rightArc.R>150&&rightArc.G>100,"A sound on the right should show a bright arc on the right: "+rightArc);
    Check(Math.Abs(leftSpot.R-backdrop.R)<6&&Math.Abs(topSpot.R-backdrop.R)<6&&Math.Abs(outside.R-backdrop.R)<6,"Only the right arc should be drawn: left "+leftSpot+" top "+topSpot+" middle "+outside);
    // Where it sits: centered on this display.
    Check(Math.Abs((x+size/2)-area.Width/2)<=1&&Math.Abs((y+size/2)-area.Height/2)<=1,"The radar is not centered on the display: "+radar.Origin);
   }
   finally{radar.Dispose();}
   for(int i=0;i<20;i++){Application.DoEvents();Thread.Sleep(20);}
   int ox=area.Width/2+110,oy=area.Height/2;var after=Pixel(area,ox,oy);
   Check(Math.Abs(after.R-20)<6,"The radar did not go away when stopped: "+after);
   back.Close();
  }
  if(failed==0)Console.WriteLine("PASS: the real sound radar window, shown on the second display with fixed levels, was visible, let the mouse through, drew only the arc on the side of the sound, sat in the middle of the display and went away when stopped. No audio was played or read, and the primary display was not touched.");
  return failed==0?0:1;
 }
}
