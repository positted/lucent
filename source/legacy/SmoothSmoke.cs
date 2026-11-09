using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ResolutionSwitcher;
class Smoke
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(MainForm f=new MainForm())
        {
            f.Show();
            Stopwatch timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<2300){Application.DoEvents();System.Threading.Thread.Sleep(8);}
            int painted=0; f.Paint += (sender,args) => painted++; timer.Restart(); while(timer.ElapsedMilliseconds<3000) { Application.DoEvents(); System.Threading.Thread.Sleep(1); } Console.WriteLine("Observed form repaint rate: " + (painted / timer.Elapsed.TotalSeconds).ToString("F1") + " FPS"); object overlay=typeof(MainForm).GetField("startupOverlay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
            if(overlay!=null) throw new Exception("Startup did not complete");
            using(Bitmap b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,f.ClientRectangle);b.Save("../../outputs/Lucent/Preview.png");}
            Console.WriteLine("Startup completed; real display modes loaded; animated form rendered successfully.");
            typeof(MainForm).GetField("allowExit",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,true);
            f.Close();
        }
    }
}
