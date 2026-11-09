using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ResolutionSwitcher;
class Preview
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(MainForm f=new MainForm())
        {
            Control overlay=null; foreach(Control c in f.Controls) if(c is StartupOverlay) overlay=c; if(overlay!=null) { f.Controls.Remove(overlay); overlay.Dispose(); }
            Label resolution=(Label)typeof(MainForm).GetField("currentResolution",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
            resolution.Text="1920 × 1080";
            Label details=(Label)typeof(MainForm).GetField("currentDetails",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
            details.Text="240 Hz";
            foreach(string name in new[]{"modeAPicker","modeBPicker","modeARefreshPicker","modeBRefreshPicker"})
            {
                DarkDropDown d=(DarkDropDown)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
                typeof(DarkDropDown).GetField("selectedItem",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(d,name.Contains("Refresh")?"240 Hz":name.Contains("modeA")?"1920 × 1080":"1440 × 1080");
            }
            Button button=(Button)typeof(MainForm).GetField("switchButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);button.Text="Switch to 1440 × 1080";button.Enabled=true;
            f.Show(); Application.DoEvents(); using(Bitmap b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,f.ClientRectangle);b.Save("../../outputs/Lucent/Preview.png");}
            Synthwave.Phase=1.2f;
            f.Show(); Application.DoEvents(); using(Bitmap b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,f.ClientRectangle);b.Save("../frame-two.png");}
        }
        using(StartupOverlay boot=new StartupOverlay())
        {
            boot.Size=new Size(720,466);typeof(StartupOverlay).GetField("frame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(boot,30);
            using(Bitmap b=new Bitmap(720,466)){boot.DrawToBitmap(b,boot.ClientRectangle);b.Save("../../outputs/Lucent/Startup.png");}
        }
    }
}
