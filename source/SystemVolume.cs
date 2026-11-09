using System;
using System.Runtime.InteropServices;

namespace Lucent
{
    // The Windows master volume: read it, set it, mute it. Nothing here runs until a volume control is shown.
    internal static class SystemVolume
    {
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator
        {int EnumAudioEndpoints(int flow,uint mask,out object devices);int GetDefaultAudioEndpoint(int flow,int role,out Device device);}
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Device
        {int Activate(ref Guid id,uint context,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);}
        [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Volume
        {
            int RegisterControlChangeNotify(IntPtr callback);
            int UnregisterControlChangeNotify(IntPtr callback);
            int GetChannelCount(out uint count);
            int SetMasterVolumeLevel(float value,ref Guid context);
            int SetMasterVolumeLevelScalar(float value,ref Guid context);
            int GetMasterVolumeLevel(out float value);
            int GetMasterVolumeLevelScalar(out float value);
            int SetChannelVolumeLevel(uint channel,float value,ref Guid context);
            int SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context);
            int GetChannelVolumeLevel(uint channel,out float value);
            int GetChannelVolumeLevelScalar(uint channel,out float value);
            int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);
            int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);
        }
        static readonly Guid Source=new Guid("3C9C6F52-1F4E-4C8C-9B0A-5B7E2D4A6C11");
        static bool With(Func<Volume,bool> use)
        {
            object enumerator=null,endpoint=null,volumeObject=null;
            try
            {
                enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                Device device;
                Marshal.ThrowExceptionForHR(((Enumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device));
                endpoint=device;
                Guid id=typeof(Volume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref id,23,IntPtr.Zero,out volumeObject));
                return use((Volume)volumeObject);
            }
            catch(Exception){return false;}
            finally
            {
                foreach(object item in new[]{volumeObject,endpoint,enumerator})if(item!=null)try{Marshal.ReleaseComObject(item);}catch(Exception){}
            }
        }
        public static bool TryGet(out float level,out bool muted)
        {
            float read=0;bool mute=false;
            bool ok=With(volume=>volume.GetMasterVolumeLevelScalar(out read)>=0&&volume.GetMute(out mute)>=0);
            level=read;muted=mute;
            return ok;
        }
        public static bool TrySet(float level)
        {
            float clamped=Math.Max(0,Math.Min(1,level));
            return With(volume=>{Guid context=Source;return volume.SetMasterVolumeLevelScalar(clamped,ref context)>=0;});
        }
        public static bool TryMute(bool muted)
        {
            return With(volume=>{Guid context=Source;return volume.SetMute(muted,ref context)>=0;});
        }
    }
}
