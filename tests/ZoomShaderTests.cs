using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Lucent;
using Device=SharpDX.Direct3D11.Device;

// Draws the zoom shader on the CPU-side WARP device into a texture and reads the pixels back, so zoom and the shadow lift are checked
// numerically without touching any screen, display setting or game. Nothing here captures the desktop.
class ZoomShaderTests
{
 const int W=64,H=36;
 static int failed;
 static void Check(bool ok,string message){if(!ok){Console.WriteLine("FAIL: "+message);failed++;}}
 static float[] Identity(){var m=new float[25];for(int i=0;i<5;i++)m[i*5+i]=1;return m;}
 // pixel(x,y) -> (r,g,b)
 static Texture2D Source(Device device,Func<int,int,byte[]> pixel)
 {
  var bytes=new byte[W*H*4];
  for(int y=0;y<H;y++)for(int x=0;x<W;x++){var c=pixel(x,y);int i=(y*W+x)*4;bytes[i]=c[2];bytes[i+1]=c[1];bytes[i+2]=c[0];bytes[i+3]=255;}
  var texture=new Texture2D(device,new Texture2DDescription{Width=W,Height=H,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.ShaderResource});
  var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
  try{device.ImmediateContext.UpdateSubresource(new DataBox(pinned.AddrOfPinnedObject(),W*4,0),texture,0);}finally{pinned.Free();}
  return texture;
 }
 static byte[,][] Read(Device device,Texture2D drawn)
 {
  var staging=new Texture2D(device,new Texture2DDescription{Width=W,Height=H,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Staging,CpuAccessFlags=CpuAccessFlags.Read,BindFlags=BindFlags.None});
  device.ImmediateContext.CopyResource(drawn,staging);
  DataStream stream;var box=device.ImmediateContext.MapSubresource(staging,0,MapMode.Read,SharpDX.Direct3D11.MapFlags.None,out stream);
  var result=new byte[W,H][];
  for(int y=0;y<H;y++)
  {
   var row=new byte[W*4];stream.Position=(long)y*box.RowPitch;stream.Read(row,0,row.Length);
   for(int x=0;x<W;x++)result[x,y]=new[]{row[x*4+2],row[x*4+1],row[x*4]};
  }
  device.ImmediateContext.UnmapSubresource(staging,0);staging.Dispose();
  return result;
 }
 static byte[,][] Draw(Device device,ZoomRenderer renderer,Texture2D source,ZoomLook look)
 {
  renderer.Capture(source);
  return Read(device,renderer.RenderOffscreen(look,Identity()));
 }
 static int Near(int a,int b){return Math.Abs(a-b);}
 [STAThread]static int Main()
 {
  using(var factory=new Factory1())
  using(var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport))
  using(var renderer=new ZoomRenderer(device,factory,IntPtr.Zero,W,H))
  {
   // A picture whose red is the column and green the row, so every output pixel says where it came from.
   using(var map=Source(device,(x,y)=>new byte[]{(byte)(x*4),(byte)(y*7),0}))
   {
    var plain=Draw(device,renderer,map,new ZoomLook{Zoom=1});
    bool same=true;for(int y=0;y<H&&same;y++)for(int x=0;x<W;x++)if(Near(plain[x,y][0],x*4)>3||Near(plain[x,y][1],y*7)>3)same=false;
    Check(same,"At 1x the picture should come out unchanged");
    var zoom2=Draw(device,renderer,map,new ZoomLook{Zoom=2});
    // The middle stays, and a point d pixels to the right of the middle shows what was d/2 pixels to the right.
    Check(Near(zoom2[32,18][0],128)<=6&&Near(zoom2[32,18][1],18*7)<=8,"2x should keep the middle of the picture in place");
    Check(Near(zoom2[48,18][0],(32+8)*4)<=8,"2x should show what was 8 pixels right of the middle at 16 pixels right: "+zoom2[48,18][0]);
    Check(Near(zoom2[16,18][0],(32-8)*4)<=8&&Near(zoom2[32,6][1],(18-6)*7)<=10,"2x should magnify left and up as well");
    var zoom4=Draw(device,renderer,map,new ZoomLook{Zoom=4});
    Check(Near(zoom4[48,18][0],(32+4)*4)<=8,"4x magnifies four times: "+zoom4[48,18][0]);
    // Around the pointer: the point under it stays exactly where it was, and nothing outside the picture is read.
    var around=Draw(device,renderer,map,new ZoomLook{Zoom=3,FocusX=.25f,FocusY=.5f});
    Check(Near(around[16,18][0],16*4)<=6&&Near(around[16,18][1],18*7)<=8,"Zooming around the pointer must keep the point under it fixed: "+around[16,18][0]+","+around[16,18][1]);
    Check(Near(around[40,18][0],(16+(40-16)/3)*4)<=8,"Around the pointer, a point 24 pixels away should show one 8 pixels away: "+around[40,18][0]);
    var corner=Draw(device,renderer,map,new ZoomLook{Zoom=4,FocusX=0,FocusY=0});
    Check(corner[0,0][0]<=4&&corner[0,0][1]<=4&&corner[W-1,H-1][0]<=(W/4)*4+8,"At the screen corner the zoom must stay inside the picture: "+corner[W-1,H-1][0]);
   }
   // A soft edge between two grays: sharpening pushes the sides apart, and plain zoom does not.
   using(var edge=Source(device,(x,y)=>{byte v=(byte)(x<W/2?90:170);return new byte[]{v,v,v};}))
   {
    var soft=Draw(device,renderer,edge,new ZoomLook{Zoom=3});
    var sharp=Draw(device,renderer,edge,new ZoomLook{Zoom=3,Sharpen=1f});
    int softDark=255,sharpDark=255,softBright=0,sharpBright=0;
    for(int x=24;x<40;x++){softDark=Math.Min(softDark,soft[x,18][0]);sharpDark=Math.Min(sharpDark,sharp[x,18][0]);softBright=Math.Max(softBright,soft[x,18][0]);sharpBright=Math.Max(sharpBright,sharp[x,18][0]);}
    Check(softDark>=88&&softBright<=172,"Plain zoom should not add halos: "+softDark+".."+softBright);
    Check(sharpDark<softDark-6&&sharpBright>softBright+6,"Sharpening should add edge contrast: "+sharpDark+".."+sharpBright+" vs "+softDark+".."+softBright);
    Check(sharp[4,18][0]>=88&&sharp[4,18][0]<=92&&sharp[60,18][0]>=168&&sharp[60,18][0]<=172,"Sharpening must leave flat areas alone");
   }
   // Adaptive sharpening leaves noise-sized differences and already-strong edges alone, and still sharpens a real soft edge.
   foreach(var kind in new[]{new byte[]{120,124},new byte[]{40,200},new byte[]{90,170}})
   {
    byte low=kind[0],high=kind[1];
    using(var edge=Source(device,(x,y)=>{byte v=x<W/2?low:high;return new byte[]{v,v,v};}))
    {
     var plain=Draw(device,renderer,edge,new ZoomLook{Zoom=3,Sharpen=1f});var adaptive=Draw(device,renderer,edge,new ZoomLook{Zoom=3,Sharpen=1f,Adaptive=true});var soft=Draw(device,renderer,edge,new ZoomLook{Zoom=3});
     int plainSpread=0,adaptiveSpread=0,softSpread=0,pl=255,ph=0,al=255,ah=0,sl=255,sh=0;
     for(int x=20;x<44;x++){pl=Math.Min(pl,plain[x,18][0]);ph=Math.Max(ph,plain[x,18][0]);al=Math.Min(al,adaptive[x,18][0]);ah=Math.Max(ah,adaptive[x,18][0]);sl=Math.Min(sl,soft[x,18][0]);sh=Math.Max(sh,soft[x,18][0]);}
     plainSpread=ph-pl;adaptiveSpread=ah-al;softSpread=sh-sl;
     if(low==120)Check(adaptiveSpread<=softSpread+1,"Adaptive sharpening should leave noise-sized differences alone: adaptive "+adaptiveSpread+", plain "+plainSpread+", soft "+softSpread);
     else if(low==40)Check(adaptiveSpread<=softSpread+2&&plainSpread>=softSpread+20,"Adaptive sharpening should not push strong edges further: adaptive "+adaptiveSpread+", plain "+plainSpread+", soft "+softSpread);
     else Check(adaptiveSpread>softSpread+4&&adaptiveSpread<plainSpread+1,"Adaptive sharpening should still sharpen a real soft edge, a little less than plain: adaptive "+adaptiveSpread+", plain "+plainSpread+", soft "+softSpread);
    }
   }
   // The shadow lift: dark grays rise a lot, bright grays hardly move, and the picture never goes past white.
   using(var ramp=Source(device,(x,y)=>{byte v=(byte)(x*4);return new byte[]{v,v,v};}))
   {
    var off=Draw(device,renderer,ramp,new ZoomLook{Zoom=1});
    var lifted=Draw(device,renderer,ramp,new ZoomLook{Zoom=1,Lift=1f});
    Check(lifted[10,18][0]>off[10,18][0]+50,"The lift should raise dark grays: "+off[10,18][0]+" -> "+lifted[10,18][0]);
    Check(lifted[60,18][0]>=off[60,18][0]-2&&lifted[60,18][0]>=238,"The lift should leave bright grays alone: "+off[60,18][0]+" -> "+lifted[60,18][0]);
    bool rising=true;for(int x=1;x<W;x++)if(lifted[x,18][0]+2<lifted[x-1,18][0])rising=false;
    Check(rising,"The lift must keep dark below bright");
    var half=Draw(device,renderer,ramp,new ZoomLook{Zoom=1,Lift=.5f});
    Check(half[10,18][0]>off[10,18][0]+15&&half[10,18][0]<lifted[10,18][0]-10,"A weaker lift should lift less");
    var crushed=Draw(device,renderer,ramp,new ZoomLook{Zoom=1,Lift=.001f,Black=1f});
    Check(crushed[3,18][0]<=4&&crushed[40,18][0]<off[40,18][0],"The black level should darken the darkest grays");
    var green=Draw(device,renderer,ramp,new ZoomLook{Zoom=1,Lift=1f,Tint=new[]{.12f,1f,.12f}});
    Check(green[40,18][1]>green[40,18][0]*4&&green[40,18][1]>green[40,18][2]*4,"The green tint should keep green and cut red and blue");
   }
   // Color pop: the chosen color gets richer, everything else is calmed toward gray, and with nothing chosen or no strength the picture is untouched.
   using(var colors=Source(device,(x,y)=>x<16?new byte[]{200,40,40}:x<32?new byte[]{40,200,40}:x<48?new byte[]{40,40,200}:new byte[]{128,128,128}))
   {
    var none=Draw(device,renderer,colors,new ZoomLook{Zoom=1,PopHue=-1f,PopStrength=1f});
    var zero=Draw(device,renderer,colors,new ZoomLook{Zoom=1,PopHue=0f,PopStrength=0f});
    Check(Near(none[4,18][0],200)<=3&&Near(zero[4,18][0],200)<=3&&Near(none[40,18][2],200)<=3,"Color pop with nothing chosen or no strength must leave the picture alone");
    var red=Draw(device,renderer,colors,new ZoomLook{Zoom=1,PopHue=0f,PopStrength=1f});
    Check(red[4,18][0]>=235&&red[4,18][1]<=30&&red[4,18][2]<=30,"The chosen red should get richer: "+string.Join(",",red[4,18]));
    int greenSpread=Math.Max(red[20,18][0],Math.Max(red[20,18][1],red[20,18][2]))-Math.Min(red[20,18][0],Math.Min(red[20,18][1],red[20,18][2]));
    Check(greenSpread<160-30,"A color that was not chosen should be calmed: spread "+greenSpread);
    Check(red[56,18][0]>=128*.85&&red[56,18][0]<=128*1.04&&Near(red[56,18][0],red[56,18][2])<=3,"Gray should hardly change: "+string.Join(",",red[56,18]));
    var green=Draw(device,renderer,colors,new ZoomLook{Zoom=1,PopHue=120/360f,PopStrength=1f});
    Check(green[20,18][1]>=235&&green[20,18][0]<=30&&(Math.Max(green[4,18][0],green[4,18][1])-Math.Min(green[4,18][0],green[4,18][2]))<160-30,"Choosing green should lift the green and calm the red");
    var weak=Draw(device,renderer,colors,new ZoomLook{Zoom=1,PopHue=0f,PopStrength=.3f});
    Check(weak[4,18][0]>red[4,18][0]-100&&weak[4,18][0]>=200&&weak[40,18][2]>red[40,18][2]&&weak[40,18][2]<200,"A weaker pop should do less");
   }
   // The reddish end of the wheel is still red: a pink-red close to hue 1 is picked up by red at hue 0.
   using(var near=Source(device,(x,y)=>new byte[]{200,40,70}))
   {
    var wrapped=Draw(device,renderer,near,new ZoomLook{Zoom=1,PopHue=0f,PopStrength=1f});
    Check(wrapped[10,18][0]>=235,"A color just across the start of the wheel should still count as red: "+string.Join(",",wrapped[10,18]));
   }   // A display turned on its side is captured sideways. Drawn back the right way up, the desktop must look exactly as it does on screen.
   foreach(int turns in new[]{1,2,3})
   {
    int sw=turns%2==1?H:W,sh=turns%2==1?W:H;
    var desktop=new Func<int,int,byte[]>((x,y)=>new byte[]{(byte)(x*4),(byte)(y*7),30});
    var turned=new byte[sw*sh*4];
    for(int ys=0;ys<sh;ys++)for(int xs=0;xs<sw;xs++)
    {
     int xd,yd;
     if(turns==1){xd=sh-1-ys;yd=xs;}else if(turns==2){xd=W-1-xs;yd=H-1-ys;}else{xd=ys;yd=sw-1-xs;}
     var c=desktop(xd,yd);int i=(ys*sw+xs)*4;turned[i]=c[2];turned[i+1]=c[1];turned[i+2]=c[0];turned[i+3]=255;
    }
    // A renderer owns the device context it draws with, so each one gets its own device.
    using(var turnedDevice=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport))
    using(var sideways=new ZoomRenderer(turnedDevice,factory,IntPtr.Zero,W,H,turns))
    using(var texture=new Texture2D(turnedDevice,new Texture2DDescription{Width=sw,Height=sh,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.ShaderResource}))
    {
     var pinned=GCHandle.Alloc(turned,GCHandleType.Pinned);
     try{turnedDevice.ImmediateContext.UpdateSubresource(new DataBox(pinned.AddrOfPinnedObject(),sw*4,0),texture,0);}finally{pinned.Free();}
     sideways.Capture(texture);
     var upright=Read(turnedDevice,sideways.RenderOffscreen(new ZoomLook{Zoom=1},Identity()));
     bool right=true;for(int y=0;y<H&&right;y++)for(int x=0;x<W;x++)if(Near(upright[x,y][0],x*4)>3||Near(upright[x,y][1],y*7)>3)right=false;
     Check(right,"A display turned "+turns*90+" degrees must be drawn upright");
     var turnedZoom=Read(turnedDevice,sideways.RenderOffscreen(new ZoomLook{Zoom=2},Identity()));
     Check(Near(turnedZoom[48,18][0],(32+8)*4)<=8&&Near(turnedZoom[32,6][1],(18-6)*7)<=10,"Zoom on a display turned "+turns*90+" degrees must magnify the desktop as it appears");
     bool refused=false;using(var wrong=Source(turnedDevice,(x,y)=>new byte[]{0,0,0})){try{if(turns%2==1)sideways.Capture(wrong);else refused=true;}catch(InvalidOperationException){refused=true;}}
     Check(refused,"A capture of the wrong size must be refused, not silently ignored");
    }
   }   // Easing toward a zoom target: smooth, never overshooting, and arriving.
   double shown=1,previous=1;bool smooth=true;int steps=0;
   while(shown<3&&steps<200){shown=DesktopGpuZoom.Approach(shown,3,.016);if(shown<previous||shown>3)smooth=false;previous=shown;steps++;}
   Check(smooth&&shown==3&&steps>6&&steps<30,"Zoom should ease to its target in a fraction of a second: "+steps+" steps");
   Check(DesktopGpuZoom.Approach(3,1,5)==1&&DesktopGpuZoom.Approach(1,3,0)==1,"A long pause must not overshoot and no time must not move");
  }
  if(failed==0)Console.WriteLine("PASS: zoom keeps the middle or the pointer in place at 1x to 4x, stays inside the picture at the corners, sharpens edges without touching flat areas, and the shadow lift raises darks, keeps whites, honors black level and tint. Software-rendered on WARP; no screen, display or game touched.");
  return failed==0?0:1;
 }
}
