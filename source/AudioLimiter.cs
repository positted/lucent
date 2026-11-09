using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Lucent
{
    // Optional master-volume limiter. It never starts until explicitly enabled.
    internal sealed class AudioLimiter:IDisposable
    {
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator
        {int EnumAudioEndpoints(int flow,uint mask,out object devices);int GetDefaultAudioEndpoint(int flow,int role,out Device device);}
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Device
        {int Activate(ref Guid id,uint context,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);}
        [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Meter
        {int GetPeakValue(out float value);}
        [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Volume
        {
            int RegisterControlChangeNotify(IntPtr callback);
            int UnregisterControlChangeNotify(IntPtr callback);
            int GetChannelCount(out uint count);
            int SetMasterVolumeLevel(float value,ref Guid context);
            int SetMasterVolumeLevelScalar(float value,ref Guid context);
            int GetMasterVolumeLevel(out float value);
            int GetMasterVolumeLevelScalar(out float value);
        }
        Thread thread;volatile bool stopped;public float Limit=.5f;public Action<string> Status;public Action Stopped;
        public void Start()
        {
            if(thread!=null)return;
            thread=new Thread(Run){IsBackground=true,Name="lucent audio limiter"};
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }
        static void Check(int result){Marshal.ThrowExceptionForHR(result);}
        void Run()
        {
            object endpoint=null,meterObject=null,volumeObject=null,enumerator=null;
            Volume volume=null;
            float baseline=0,last=0;
            bool changed=false;
            Guid context=new Guid("D08AC57A-02B4-4CB7-88FD-4CD5A89D92EB");
            try{
                enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                Device device;
                Check(((Enumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device));
                endpoint=device;
                Guid meterId=typeof(Meter).GUID,volumeId=typeof(Volume).GUID;
                Check(device.Activate(ref meterId,23,IntPtr.Zero,out meterObject));
                Check(device.Activate(ref volumeId,23,IntPtr.Zero,out volumeObject));
                var meter=(Meter)meterObject;
                volume=(Volume)volumeObject;
                Check(volume.GetMasterVolumeLevelScalar(out baseline));
                last=baseline;
                while(!stopped)
                {
                    float current,peak;
                    Check(volume.GetMasterVolumeLevelScalar(out current));
                    if(Math.Abs(current-last)>.007f)
                    {
                        if(Status!=null)Status("Audio limiter stopped: your volume changed outside Lucent.");
                        return;
                    }
                    Check(meter.GetPeakValue(out peak));
                    float target=baseline*Math.Min(1,Math.Max(.05f,Limit)/Math.Max(.001f,peak));
                    float next=target<last?target:last+(target-last)*.04f;
                    if(Math.Abs(next-last)>.001)
                    {
                        Check(volume.SetMasterVolumeLevelScalar(next,ref context));
                        last=next;
                        changed=true;
                    }
                    Thread.Sleep(15);
                }
            }catch(Exception e){if(Status!=null)Status("Audio limiter unavailable: "+e.Message);}
            finally
            {
                if(changed&&volume!=null)try
                {
                    float now;
                    if(volume.GetMasterVolumeLevelScalar(out now)>=0&&Math.Abs(now-last)<.007f)volume.SetMasterVolumeLevelScalar(baseline,ref context);
                }
                catch{}foreach(object obj in new[]{meterObject,volumeObject,endpoint,enumerator})if(obj!=null)try
                {
                    Marshal.ReleaseComObject(obj);
                }
                catch{}if(!stopped&&Stopped!=null)Stopped();
            }
        }
        public void Dispose(){stopped=true;if(thread!=null)thread.Join(1500);}
    }
}
