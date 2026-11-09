using System;
using System.Reflection;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device=SharpDX.Direct3D11.Device;
class GpuZoomSmoke
{
 [STAThread] static void Main()
 {
  using(var host=new System.Windows.Forms.Form())
  using(var factory=new Factory1())
  using(var adapter=factory.GetAdapter1(0))
  using(var device=new Device(adapter,DeviceCreationFlags.BgraSupport))
  {
   var assembly=Assembly.LoadFrom("Lucent.exe");
   var type=assembly.GetType("Lucent.ZoomRenderer");
   var renderer=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{device,factory,host.Handle,256,144},null);
   try
   {
    var desc=new Texture2DDescription {Width=256,Height=144,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.ShaderResource};
    using(var texture=new Texture2D(device,desc))
    {
     byte[] pixels=new byte[256*144*4];
     var random=new Random(13);
     byte[] pattern=new byte[256*144];random.NextBytes(pattern);
     for(int frame=0;frame<3;frame++)
     {
      for(int y=0;y<144;y++)for(int x=0;x<256;x++)
      {
       int index=(y*256+x)*4;byte value=pattern[y*256+Math.Max(0,x-(frame==2?4:0))];
       pixels[index]=pixels[index+1]=pixels[index+2]=value;pixels[index+3]=255;
      }
      var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);
      try{device.ImmediateContext.UpdateSubresource(new DataBox(pinned.AddrOfPinnedObject(),256*4,0),texture,0);}
      finally{pinned.Free();}
            var colors=new float[25];for(int k=0;k<5;k++)colors[k*5+k]=1;
      colors[0]=colors[6]=colors[12]=frame==0?1:6;
      type.GetMethod("Render").Invoke(renderer,new object[]{texture,2f,colors});
     }
    }
    Console.WriteLine("PASS: GPU device, shader compilation, capture-format textures and zoom/color-compensation renders.");
   }
   finally{((IDisposable)renderer).Dispose();}
  }
 }
}
