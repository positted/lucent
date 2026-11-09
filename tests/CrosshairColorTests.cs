using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Windows.Media;
using Lucent;
class CrosshairColorTests
{
 [STAThread] static int Main()
 {
  var serializer=new XmlSerializer(typeof(Preferences));
  var old=(Preferences)serializer.Deserialize(new StringReader("<Preferences><CrosshairSize>0.6</CrosshairSize></Preferences>"));
  if(old.CrosshairColor!="#FFFFFF"||!old.CrosshairOutline)throw new Exception("Old settings defaults");
  var value=new Preferences {CrosshairColor="#19E5A7",CrosshairOutline=false};
  var text=new StringWriter();serializer.Serialize(text,value);
  var loaded=(Preferences)serializer.Deserialize(new StringReader(text.ToString()));
  if(loaded.CrosshairColor!=value.CrosshairColor||loaded.CrosshairOutline)throw new Exception("Round trip");
  var assembly=typeof(Preferences).Assembly;
  var hsv=assembly.GetType("Lucent.CrosshairColorField").GetMethod("FromHsv");
  var colors=new[]{Colors.Red,Colors.Yellow,Colors.Lime,Colors.Cyan,Colors.Blue,Colors.Magenta};
  for(int i=0;i<6;i++)if((Color)hsv.Invoke(null,new object[]{i*60.0,1.0,1.0})!=colors[i])throw new Exception("Hue conversion");
  var render=assembly.GetType("Lucent.Shell").GetMethod("RenderCrosshairPreview",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(string style in CrosshairPresets.Names)
   foreach(bool outline in new[]{false,true})
    foreach(string color in new[]{"#000000","#FFFFFF","#FF00FF","#00FFFF"})
    {
     var look=CrosshairPresets.Create(style);look.FillColor=color;look.Outline=outline;look.OutlineColor="#00FFFF";
     if(render.Invoke(null,new object[]{look,CrosshairRenderer.Extent(look)*2+1})==null)throw new Exception("Render");
    }  Console.WriteLine("PASS: legacy defaults, RGB/outline persistence, hue conversion and all preset render combinations.");return 0;
 }
}
