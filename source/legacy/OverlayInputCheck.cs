using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ResolutionSwitcher;
class OverlayInputCheck {
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 static void Pump(){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<250){Application.DoEvents();System.Threading.Thread.Sleep(5);}}
 [STAThread] static void Main(string[] args) {
 Application.EnableVisualStyles();
 if(args.Length>0){using(var target=new Form{Text="Lucent input test target",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=Screen.PrimaryScreen.Bounds,TopMost=true,BackColor=Color.DarkSlateGray}){Application.Run(target);}return;}
 Process child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"target"){UseShellExecute=false});
 try {
 child.WaitForInputIdle(5000);Pump();child.Refresh(); IntPtr target=child.MainWindowHandle;
 if(target==IntPtr.Zero)throw new Exception("Target missing");
 using(var overlay=new CrosshairOverlay()){
 IntPtr foreground=GetForegroundWindow();
 for(int pass=0;pass<4;pass++){
 overlay.Configure("Classic",pass==1?Color.Magenta:Color.White,pass==2?20f:.6f,true);
 overlay.Show(); overlay.Bounds=new Rectangle(Screen.PrimaryScreen.Bounds.Left+40,Screen.PrimaryScreen.Bounds.Top+40,300,300); Pump();
 var r=overlay.Bounds;var center=new Point(r.Left+(r.Width-1)/2,r.Top+(r.Height-1)/2);
 IntPtr hit=WindowFromPoint(center); uint pid; GetWindowThreadProcessId(hit,out pid); Console.WriteLine("Target="+target+" overlay="+overlay.Handle+" hit="+hit+" pid="+pid+" expectedPid="+child.Id);
 Console.WriteLine("Pass "+pass+" enabled="+IsWindowEnabled(overlay.Handle)+" styles="+GetWindowLong(overlay.Handle,-20).ToString("X")+" center reaches target="+(hit==target));
 if(pid!=child.Id)throw new Exception("Overlay intercepts cross-process hit test");
 if(GetForegroundWindow()==overlay.Handle)throw new Exception("Overlay changed foreground");
 overlay.Hide();Pump();
 }
 overlay.Close();
 }
 Console.WriteLine("PASS: cross-process center hit testing and overlay non-activation across color, size, and show/hide.");
 } finally { if(!child.HasExited){child.CloseMainWindow();if(!child.WaitForExit(3000))child.Kill();}child.Dispose();}
 }
}
