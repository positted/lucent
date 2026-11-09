using System;
using System.Drawing;
using ResolutionSwitcher;
class SymmetryCheck {
 static void Main() {
 foreach(float size in new[]{.1f,.6f,1f,5f,20f}) foreach(bool outline in new[]{false,true})
 using(var b=new Bitmap(301,301)) {
 using(var g=Graphics.FromImage(b)) CrosshairArt.Draw(g,new Rectangle(0,0,301,301),"Classic",Color.White,size,outline);
 for(int y=0;y<301;y++)for(int x=0;x<301;x++)
 if(b.GetPixel(x,y)!=b.GetPixel(300-x,y)||b.GetPixel(x,y)!=b.GetPixel(x,300-y))throw new Exception("Asymmetry "+size);
 if(b.GetPixel(150,150).ToArgb()!=Color.White.ToArgb())throw new Exception("Center gap");
 }
 Console.WriteLine("PASS: plus symmetry and filled center at five sizes, outlines on/off.");
 }
}
