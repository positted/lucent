using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lucent
{
    internal static class ColorMath
    {
        static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        // The color of a glowing black body at a temperature, as the share of full white each channel keeps.
        // Tanner Helland's curve fit to Mitchell Charity's measurements, valid from 1000 K to 40000 K.
        static void BlackBody(double kelvin,out float red,out float green,out float blue)
        {
            double t=Math.Max(1000,Math.Min(40000,kelvin))/100,r,g,b;
            r=t<=66?255:329.698727446*Math.Pow(t-60,-.1332047592);
            g=t<=66?99.4708025861*Math.Log(t)-161.1195681661:288.1221695283*Math.Pow(t-60,-.0755148492);
            b=t>=66?255:t<=19?0:138.5177312231*Math.Log(t-10)-305.0447927307;
            red=(float)(Math.Max(0,Math.Min(255,r))/255);green=(float)(Math.Max(0,Math.Min(255,g))/255);blue=(float)(Math.Max(0,Math.Min(255,b))/255);
        }
        // Rows are the input red, green and blue; columns the output red, green and blue (strength 0.8 for protan and deutan, 1.0 for tritan).
        internal static float[] VisionFix(string vision)
        {
            switch(vision)
            {
                case "Protanopia":return new[]{1f,.5637f,.6821f, 0f,.3717f,-.7940f, 0f,.0647f,1.1119f};
                case "Deuteranopia":return new[]{1f,.2261f,.5180f, 0f,.6390f,-.7315f, 0f,.1350f,1.2135f};
                case "Tritanopia":return new[]{.7397f,.0737f,0f, -.6146f,.3778f,0f, .8749f,.5485f,1f};
                default:return null;
            }
        }
        public static float[] Identity(){float[] m=new float[25];for(int i=0;i<5;i++)m[i*5+i]=1;return m;}
        // Win32 MAGCOLOREFFECT uses row-vector RGB, alpha and homogeneous offset.
        public static float[] Multiply(float[] a,float[] b)
        {
            float[] m=new float[25];
            for(int i=0;i<5;i++)for(int j=0;j<5;j++)for(int k=0;k<5;k++)m[i*5+j]+=a[i*5+k]*b[k*5+j];
            return m;
        }
        public static float[] Build(Look look)
        {
            float s=(float)Math.Max(0,Math.Min(3,look.Saturation/100)),red=(float)Math.Max(0,Math.Min(1,look.Red/100)),dim=(float)(1-Math.Max(0,Math.Min(.85,look.Dim/100)));
            float[] m=Identity();float[] luma={.2126f,.7152f,.0722f};
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)m[i*5+j]=luma[i]*(1-s)+(i==j?s:0);
            // A red filter does what f.lux, Night Light and Redshift do: it moves the screen's white point along the
            // glow of a hot object, from daylight through cream and amber to orange and red. Blue fades first, green last.
            float[] filter=Identity();
            if(red>0)
            {
                // Evenly spaced in mireds (1,000,000 / kelvin), which is how the eye sees warmth, from 6600 K to 1200 K.
                float warm=Math.Min(1,red/.75f);
                double kelvin=1000000d/(151.5+(833.3-151.5)*warm);
                float gainRed,gainGreen,gainBlue;
                BlackBody(kelvin,out gainRed,out gainGreen,out gainBlue);
                // Past the last temperature the table covers, green keeps fading until only red is left.
                if(red>.75f)gainGreen*=1-Smooth((red-.75f)/.25f);
                float mix=Smooth((red-.55f)/.45f);
                filter[0]=gainRed*(1-mix+.2126f*mix);
                filter[5]=gainRed*.7152f*mix;
                filter[10]=gainRed*.0722f*mix;
                filter[6]=gainGreen;
                filter[12]=gainBlue;
            }            m=Multiply(m,filter);
            // Color blindness aid: daltonization (Fidaner, Lin and Ozguven, 2005). The picture a person with that deficiency would see is worked out
            // (Machado, Oliveira and Fernandes, 2009) and the part they lose is added back in colors they can tell apart, so colors that look the same to
            // them, like red and green, come apart. Checked against that same model on thousands of confusable color pairs (see the tests). A display aid,
            // not a medical correction; real eyes vary.
            float[] vision=Identity();
            float[] fix=VisionFix(StateStore.CanonicalVision(look.Vision));
            // The strength moves the matrix from "no change" (0) to the full aid (100), so half strength is half way between the two.
            float aid=(float)Math.Max(0,Math.Min(1,look.VisionStrength/100));
            if(fix!=null)for(int i=0;i<3;i++)for(int j=0;j<3;j++)vision[i*5+j]=(i==j?1f:0f)+(fix[i*3+j]-(i==j?1f:0f))*aid;
            m=Multiply(m,vision);
            // Smart shadows are done on the GPU instead, so the display matrix leaves them alone.
            if(look.DarkEnabled&&!look.DarkSmart)
            {
                float strength=(float)Math.Max(0,Math.Min(1,look.DarkStrength/100));
                float gain=1+strength*5,offset=-(float)Math.Max(0,Math.Min(.2,look.BlackLevel/500));
                float[] dark=Identity();for(int i=0;i<3;i++){dark[i*5+i]=gain;dark[20+i]=offset;}
                m=Multiply(m,dark);
                if(look.DarkColor!="White")
                {
                    float[] tint=Identity();float r=1,g=1,b=1;
                    if(look.DarkColor=="Green"){r=.12f;b=.12f;}if(look.DarkColor=="Amber"){g=.65f;b=.08f;}if(look.DarkColor=="Cyan"){r=.1f;}
                    tint[0]=r;tint[6]=g;tint[12]=b;m=Multiply(m,tint);
                }
            }
            float[] brightness=Identity();brightness[0]=brightness[6]=brightness[12]=dim;return Multiply(m,brightness);
        }
        public static bool Equal(float[] a,float[] b)
        {
            return a!=null&&b!=null&&a.Length==b.Length&&a.Zip(b,(x,y)=>Math.Abs(x-y)<.0001f).All(x=>x);
        }
    }
    [Serializable] public class EffectJournal
    {
        public float[] Before,Applied;
        public float ZoomBefore=1,ZoomApplied=1;
        public int XBefore,YBefore,XApplied,YApplied;
    }
    internal static class NativeEffects
    {
        [DllImport("Magnification.dll",SetLastError=true)]internal static extern bool MagInitialize();
        [DllImport("Magnification.dll")]internal static extern bool MagUninitialize();
        [DllImport("Magnification.dll",SetLastError=true)]internal static extern bool MagGetFullscreenColorEffect([Out]float[] matrix);
        [DllImport("Magnification.dll",SetLastError=true)]internal static extern bool MagSetFullscreenColorEffect(float[] matrix);
        [DllImport("Magnification.dll",SetLastError=true)]internal static extern bool MagGetFullscreenTransform(out float zoom,out int x,out int y);
        [DllImport("Magnification.dll",SetLastError=true)]internal static extern bool MagSetFullscreenTransform(float zoom,int x,int y);
        static void Check(bool ok)
        {
            if(!ok)throw new InvalidOperationException("Windows denied the display effect ("+Marshal.GetLastWin32Error()+"). No administrator or security setting was changed.");
        }
        public static int Worker(string token,int parentId)
        {
            bool initialized=false,owns=false;
            var journal=new EffectJournal();
            string path=Path.Combine(StateStore.Folder,"software-recovery.xml");
            using(var mutex=new Mutex(false,LegacyNames.SoftwareColorMutex))
            using(var pipe=new NamedPipeClientStream(".","lucent-effects-"+token,PipeDirection.InOut))
            {
                try
                {
                    try
                    {
                        owns=mutex.WaitOne(0);
                    }
                    catch(AbandonedMutexException)
                    {
                        owns=true;
                    }
                    if(!owns)throw new InvalidOperationException("Another Lucent color session is active.");
                    using(var parent=Process.GetProcessById(parentId))if(parent.MainModule.FileName!=System.Windows.Forms.Application.ExecutablePath)throw new InvalidOperationException("Invalid display owner.");
                    pipe.Connect(5000);var reader=new BinaryReader(pipe);var writer=new BinaryWriter(pipe);
                    if(File.Exists(path))throw new InvalidOperationException("A recovery record exists. Use Recover colors before enabling effects.");
                    Check(MagInitialize());initialized=true;journal.Before=new float[25];Check(MagGetFullscreenColorEffect(journal.Before));
                    Check(MagGetFullscreenTransform(out journal.ZoomBefore,out journal.XBefore,out journal.YBefore));
                    journal.Applied=(float[])journal.Before.Clone();
                    journal.ZoomApplied=journal.ZoomBefore;
                    journal.XApplied=journal.XBefore;
                    journal.YApplied=journal.YBefore;
                    writer.Write("ready");writer.Flush();
                    while(true)
                    {
                        int command=reader.ReadInt32();if(command==2)break;
                        float[] requested=new float[25];
                        for(int i=0;i<25;i++)requested[i]=reader.ReadSingle();
                        float zoom=reader.ReadSingle();
                        int x=reader.ReadInt32(),y=reader.ReadInt32();
                        float[] current=new float[25];Check(MagGetFullscreenColorEffect(current));
                        if(!ColorMath.Equal(current,journal.Applied))throw new InvalidOperationException("Another application changed screen colors. Its changes were preserved.");
                        float z;int zx,zy;Check(MagGetFullscreenTransform(out z,out zx,out zy));
                        if(Math.Abs(z-journal.ZoomApplied)>.001||zx!=journal.XApplied||zy!=journal.YApplied)throw new InvalidOperationException("Another magnifier changed the zoom. Its changes were preserved.");
                        journal.Applied=ColorMath.Multiply(journal.Before,requested);
                        journal.ZoomApplied=zoom<=1?journal.ZoomBefore:zoom;
                        journal.XApplied=zoom<=1?journal.XBefore:x;
                        journal.YApplied=zoom<=1?journal.YBefore:y;
                        StateStore.Write(path,journal);
                        Check(MagSetFullscreenColorEffect(journal.Applied));
                        Check(MagSetFullscreenTransform(journal.ZoomApplied,journal.XApplied,journal.YApplied));
                        writer.Write("applied");writer.Flush();
                    }
                }
                catch(EndOfStreamException){}catch(IOException){}
                catch(Exception e){try{new BinaryWriter(pipe).Write("error: "+e.Message);}catch{}}
                finally
                {
                    if(initialized)
                    {
                        bool restored=true;float[] current=new float[25];
                        if(!MagGetFullscreenColorEffect(current))restored=false;
                        else if(ColorMath.Equal(current,journal.Applied))restored=MagSetFullscreenColorEffect(journal.Before);
                        float z;int x,y;if(!MagGetFullscreenTransform(out z,out x,out y))restored=false;
                        else if(Math.Abs(z-journal.ZoomApplied)<.001&&x==journal.XApplied&&y==journal.YApplied)restored=MagSetFullscreenTransform(journal.ZoomBefore,journal.XBefore,journal.YBefore)&&restored;
                        if(restored&&File.Exists(path))File.Delete(path);MagUninitialize();
                    }
                    if(owns)mutex.ReleaseMutex();
                }
            }
            return 0;
        }
        public static void Recover()
        {
            string path=Path.Combine(StateStore.Folder,"software-recovery.xml");if(!File.Exists(path))return;
            using(var mutex=new Mutex(false,LegacyNames.SoftwareColorMutex))
            {
                bool owns=false;
                try
                {
                    try
                    {
                        owns=mutex.WaitOne(0);
                    }
                    catch(AbandonedMutexException)
                    {
                        owns=true;
                    }
                    if(!owns)throw new InvalidOperationException("The color worker is still active.");
                    EffectJournal record;
                    using(var stream=File.OpenRead(path))record=(EffectJournal)new System.Xml.Serialization.XmlSerializer(typeof(EffectJournal)).Deserialize(stream);
                    Check(MagInitialize());
                    try
                    {
                        float[] current=new float[25];
                        Check(MagGetFullscreenColorEffect(current));
                        if(ColorMath.Equal(current,record.Applied))Check(MagSetFullscreenColorEffect(record.Before));
                        float z;
                        int x,y;
                        Check(MagGetFullscreenTransform(out z,out x,out y));
                        if(Math.Abs(z-record.ZoomApplied)<.001&&x==record.XApplied&&y==record.YApplied)Check(MagSetFullscreenTransform(record.ZoomBefore,record.XBefore,record.YBefore));
                        File.Delete(path);
                    }finally{MagUninitialize();}
                }finally{if(owns)mutex.ReleaseMutex();}
            }
        }
    }
    internal sealed class EffectsClient:IDisposable
    {
        NamedPipeServerStream pipe;
        Process worker;
        readonly object gate=new object();
        bool disposed;
        Look next;
        bool nextZoom;
        public Action<string> Status,Error;
        public void Set(Look look,bool zoom)
        {
            lock(gate)
            {
                next=look.Copy();
                nextZoom=zoom;
                if(pipe!=null)return;
                pipe=new NamedPipeServerStream("lucent-effects-"+token,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
            }
            Task.Factory.StartNew(Run);
        }
        readonly string token=Guid.NewGuid().ToString("N");
        void Run()
        {
            try{
                worker=Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"--effects "+token+" "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                var pending=pipe.BeginWaitForConnection(null,null);
                if(!pending.AsyncWaitHandle.WaitOne(6000))throw new TimeoutException("The color worker did not connect.");
                pipe.EndWaitForConnection(pending);
                var reader=new BinaryReader(pipe);
                var writer=new BinaryWriter(pipe);
                string answer=reader.ReadString();
                if(answer!="ready")throw new InvalidOperationException(answer);
                Look previous=null;bool previousZoom=false;
                while(!disposed)
                {
                    Look current;
                    bool zoom;
                    lock(gate)
                    {
                        current=next;
                        zoom=nextZoom;
                    }
                    if(current!=previous||zoom!=previousZoom)
                    {
                        float[] matrix=ColorMath.Build(current);
                        writer.Write(1);
                        foreach(float f in matrix)writer.Write(f);
                        float factor=zoom?(float)Math.Max(1.5,Math.Min(4,current.Zoom)):1;
                        var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                        writer.Write(factor);
                        writer.Write((int)(screen.Width*(1-1/factor)/2));
                        writer.Write((int)(screen.Height*(1-1/factor)/2));
                        writer.Flush();
                        answer=reader.ReadString();
                        if(answer!="applied")throw new InvalidOperationException(answer);
                        if(previous==null&&Status!=null)Status("Display adjustments applied");
                        previous=current;
                        previousZoom=zoom;
                        }
                        Thread.Sleep(30);
                        }
                writer.Write(2);writer.Flush();
            }catch(Exception e){if(!disposed&&Error!=null)Error(e.Message);}finally{try{pipe.Dispose();}catch{}}
        }
        public void Dispose()
        {
            disposed=true;
            try
            {
                if(pipe!=null)pipe.Dispose();
            }
            catch{}if(worker!=null)
            {
                try
                {
                    worker.WaitForExit(2000);
                }
                catch{}worker.Dispose();
            }
        }
    }
}


