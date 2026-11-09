using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using ResolutionSwitcher;
class DragSmoke
{
    static void Pump(int ms) { Stopwatch sw=Stopwatch.StartNew(); while(sw.ElapsedMilliseconds<ms) { Application.DoEvents(); System.Threading.Thread.Sleep(1); } }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(MainForm form=new MainForm())
        {
            form.Show(); Pump(1500);
            MethodInfo moving=typeof(MainForm).GetMethod("SetMovingWindow",BindingFlags.Instance|BindingFlags.NonPublic);
            moving.Invoke(form,new object[]{true}); float phase=Synthwave.Phase;
            Pump(300); if(Synthwave.Phase!=phase) throw new Exception("Animation advanced during move");
            moving.Invoke(form,new object[]{false}); Pump(300);
            if(Synthwave.Phase<=phase) throw new Exception("Animation did not resume");
            typeof(MainForm).GetField("allowExit",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true); form.Close();
            Console.WriteLine("Move pause and release resume passed.");
        }
    }
}
