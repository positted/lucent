using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace ResolutionSwitcher
{
 class CrosshairPrecisionSmoke
 {
  static void Key(Control c,char ch){typeof(Control).GetMethod("OnKeyPress",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{new KeyPressEventArgs(ch)});}
  [STAThread] static void Main()
  {
   Application.EnableVisualStyles();
   using(var entry=new DecimalEntry()){entry.Value=.6f;entry.Focus();typeof(DecimalEntry).GetMethod("SelectAllText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entry,null);foreach(char c in "0.1")Key(entry,c);Key(entry,'\r');if(Math.Abs(entry.Value-.1f)>.001)throw new Exception("Minimum decimal failed");typeof(DecimalEntry).GetMethod("SelectAllText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entry,null);foreach(char c in "20.0")Key(entry,c);Key(entry,'\r');if(Math.Abs(entry.Value-20f)>.001)throw new Exception("Maximum decimal failed");}
   foreach(float scale in new[]{.1f,.6f,1f,5f,20f})using(var preview=new Bitmap(220,190))using(var overlay=new Bitmap(220,190)){using(Graphics a=Graphics.FromImage(preview))CrosshairArt.Draw(a,new Rectangle(0,0,220,190),"Classic",Color.White,scale);using(Graphics b=Graphics.FromImage(overlay))CrosshairArt.Draw(b,new Rectangle(0,0,220,190),"Classic",Color.White,scale);if(!Equal(preview,overlay))throw new Exception("Preview mismatch at "+scale);}
   Console.WriteLine("Decimal limits and exact preview/overlay renderer match passed.");
  }
  static bool Equal(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
 }
}
