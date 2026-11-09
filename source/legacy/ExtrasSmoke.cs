using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ResolutionSwitcher;
class ExtrasSmoke
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(HotkeyWindow first=new HotkeyWindow(()=>{}))
        using(HotkeyWindow other=new HotkeyWindow(()=>{}))
        {
            Keys a=Keys.Control|Keys.Alt|Keys.F11,b=Keys.Control|Keys.Alt|Keys.F12;
            if(!first.Register(a)||!other.Register(b))throw new Exception("Test shortcuts unavailable");
            if(first.Register(b))throw new Exception("Conflict accepted");
            if(other.Register(a))throw new Exception("Original shortcut lost");
            if(!first.Register(Keys.Control|Keys.Shift|Keys.F11))throw new Exception("Rebind failed");
            if(!other.Register(a))throw new Exception("Old bind not released");
        }
        using(HotkeyDialog d=new HotkeyDialog(Keys.F10)) {d.Show();Application.DoEvents();using(Bitmap b=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save("../hotkey-preview.png");}d.Close();}
        using(Bitmap b=new Bitmap(256,256))using(Graphics g=Graphics.FromImage(b)){Synthwave.Mark(g,new RectangleF(0,0,256,256));b.Save("../../outputs/Lucent/Logo.png");}
        SwitchSound.Play();System.Threading.Thread.Sleep(300);
        Console.WriteLine("Hotkey registration, conflict preservation, and rebind passed; sound invoked.");
    }
}
