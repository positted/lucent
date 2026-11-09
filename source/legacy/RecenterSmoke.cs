using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
namespace ResolutionSwitcher {
class RecenterSmoke {
[STAThread] static void Main() {
Application.EnableVisualStyles();
using(var overlay=new CrosshairOverlay()) {
overlay.Bounds=new Rectangle(0,0,800,600);
overlay.Show(); Application.DoEvents();
if(overlay.Bounds!=Screen.PrimaryScreen.Bounds)throw new Exception("Show did not recenter");
overlay.Bounds=new Rectangle(30,20,1000,700);
typeof(CrosshairOverlay).GetMethod("OnDisplaySettingsChanged",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(overlay,new object[]{null,EventArgs.Empty});
Application.DoEvents();
if(overlay.Bounds!=Screen.PrimaryScreen.Bounds)throw new Exception("Display change did not recenter");
overlay.Hide(); overlay.Bounds=new Rectangle(0,0,640,480); overlay.Show(); Application.DoEvents();
if(overlay.Bounds!=Screen.PrimaryScreen.Bounds)throw new Exception("Toggle did not recenter");
overlay.Close();
}
Console.WriteLine("PASS: stale bounds corrected on show, display-change notification, and re-enable.");
}
}}
