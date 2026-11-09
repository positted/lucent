using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using ResolutionSwitcher;
class StartupPolishCheck {
[STAThread] static void Main(){
Application.EnableVisualStyles();
using(var form=new Form())using(var intro=new StartupOverlay{Size=new Size(720,466)}){
using(var sheet=new Bitmap(1440,932)){
using(var g=Graphics.FromImage(sheet)){
float[] times={.12f,.4f,.78f,1.08f};
for(int i=0;i<4;i++)using(var frame=new Bitmap(720,466)){
using(var fg=Graphics.FromImage(frame))intro.Render(fg,times[i]);
g.DrawImageUnscaled(frame,(i%2)*720,(i/2)*466);
}}
sheet.Save("../../outputs/Lucent/Startup-v22-frames.png");
}
int finished=0;intro.Finished+=(s,e)=>finished++; form.Controls.Add(intro); form.Show();intro.Start();
var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<1650){Application.DoEvents();System.Threading.Thread.Sleep(5);}
if(finished!=1)throw new Exception("Startup completion count: "+finished);
form.Close();
}
Console.WriteLine("PASS: four startup phases rendered; completion fires once.");
}}
