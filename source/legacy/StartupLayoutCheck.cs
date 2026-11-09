using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using ResolutionSwitcher;
class StartupLayoutCheck {
[STAThread]static void Main(){
Application.EnableVisualStyles();
using(var f=new MainForm()){
f.Show();Application.DoEvents();
var intro=(StartupOverlay)typeof(MainForm).GetField("startupOverlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);
if(intro.Bounds!=f.ClientRectangle||f.Controls.GetChildIndex(intro)!=0)throw new Exception("Intro does not cover full window");
var header=f.Controls.OfType<Panel>().First(p=>p.Dock==DockStyle.Top);
var tabs=header.Controls.OfType<MiamiTab>().OrderBy(tab=>tab.Left).ToArray();
if(Math.Abs((tabs[0].Left+tabs[1].Right)-header.Width)>1)throw new Exception("Tabs not centered");
using(var b=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(b))intro.Render(g,.65f);b.Save("../../outputs/Lucent/Startup-full-window.png");}
var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<1800){Application.DoEvents();System.Threading.Thread.Sleep(5);}
if(typeof(MainForm).GetField("startupOverlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f)!=null)throw new Exception("Startup stuck");
using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,f.ClientRectangle);b.Save("../../outputs/Lucent/Centered-tabs.png");}
f.Close();
}Console.WriteLine("PASS full-window intro coverage, topmost control, centered tabs, and completed transition.");
}}

