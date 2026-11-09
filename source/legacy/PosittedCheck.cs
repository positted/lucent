using System;
using System.Drawing;
using ResolutionSwitcher;
class PosittedCheck {
static void Main(){
foreach(bool outline in new[]{false,true})using(var bitmap=new Bitmap(64,64)){
using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.FromArgb(35,35,35));CrosshairArt.Draw(g,new Rectangle(0,0,64,64),"positted",Color.White,.6f,outline);}
int white=0,black=0;
for(int y=0;y<64;y++)for(int x=0;x<64;x++){
var p=bitmap.GetPixel(x,y);
if(p!=bitmap.GetPixel(63-x,y)||p!=bitmap.GetPixel(x,63-y))throw new Exception("Asymmetry");
if(p.ToArgb()==Color.White.ToArgb())white++;
if(p.ToArgb()==Color.Black.ToArgb())black++;
}
if(white!=24 || (outline ? black==0 : black!=0))throw new Exception("Arm geometry or outline wrong");
if(bitmap.GetPixel(31,31).ToArgb()!=Color.FromArgb(35,35,35).ToArgb())throw new Exception("Center filled");
System.IO.Directory.CreateDirectory("../previews");
bitmap.Save("../previews/positted-"+(outline?"outline":"plain")+".png");
}
Console.WriteLine("PASS: reference arm dimensions, symmetry, empty center, outline on/off.");
}}
