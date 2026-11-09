using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using ResolutionSwitcher;
class WindowSmoke
{
    static void Pump(int ms){Stopwatch s=Stopwatch.StartNew();while(s.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(1);}}
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(MainForm form=new MainForm())
        {
            form.Show();Pump(1400);
            typeof(MainForm).GetMethod("MinimizeToTray",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            if(form.Visible||form.ShowInTaskbar)throw new Exception("Minimize failed");
            NotifyIcon tray=(NotifyIcon)typeof(MainForm).GetField("trayIcon",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
            if(!tray.Visible)throw new Exception("Tray icon missing");
            typeof(MainForm).GetMethod("ShowFromTray",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Pump(50);
            if(!form.Visible||!form.ShowInTaskbar)throw new Exception("Restore failed");
            form.Close();Pump(50);
            if(!form.IsDisposed||tray.Visible)throw new Exception("Close did not exit");
            Console.WriteLine("Minimize to tray, restore, and full close passed.");
        }
    }
}
