using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
namespace ResolutionSwitcher
{
 class CrosshairSmoke
 {
  static object Get(object o,string n){return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
  static void Call(object o,string n){o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);}
  static void Pump(int ms){var s=Stopwatch.StartNew();while(s.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(2);}}
  [STAThread] static void Main(){Application.EnableVisualStyles();using(var f=new MainForm()){f.Show();Pump(1300);var resolution=(Panel)Get(f,"resolutionPage");var crosshair=(Panel)Get(f,"crosshairPage");if(!resolution.Visible||crosshair.Visible)throw new Exception("Initial page wrong");var tabs=f.Controls[0].Controls;MiamiTab r=null,c=null;foreach(Control x in tabs){if(x is MiamiTab){if(x.Text=="Resolution")r=(MiamiTab)x;else c=(MiamiTab)x;}}c.PerformClickForTest();Pump(30);if(resolution.Visible||!crosshair.Visible)throw new Exception("Tab switch failed");var overlay=(CrosshairOverlay)Get(f,"crosshairOverlay");if(!overlay.Visible)Call(f,"ToggleCrosshair");Pump(50);if(!overlay.Visible)throw new Exception("Overlay missing");using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,f.ClientRectangle);b.Save("../../outputs/Lucent/Crosshair-preview.png");}Call(f,"ToggleCrosshair");if(overlay.Visible)throw new Exception("Toggle failed");f.Close();Console.WriteLine("Tabs, overlay, toggle, and close passed.");}}
 }
 internal static class MiamiTabTestExtension { public static void PerformClickForTest(this MiamiTab tab){typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tab,new object[]{EventArgs.Empty});} }
}
