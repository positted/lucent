using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Lucent
{
    // The volume of one app, not of the whole PC. The media controls follow the app that is playing, so moving the slider
    // changes only that app's own level in the Windows volume mixer and leaves everything else alone.
    internal static class AppVolume
    {
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator
        {int EnumAudioEndpoints(int flow,uint mask,out object devices);int GetDefaultAudioEndpoint(int flow,int role,out Device device);}
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Device
        {int Activate(ref Guid id,uint context,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);}
        [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface SessionManager
        {
            int GetAudioSessionControl(IntPtr group,uint flags,out IntPtr control);
            int GetSimpleAudioVolume(IntPtr group,uint flags,out IntPtr volume);
            int GetSessionEnumerator(out SessionList list);
        }
        [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface SessionList
        {
            int GetCount(out int count);
            int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object session);
        }
        [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface SessionInfo
        {
            int GetState(out int state);
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
            int GetGroupingParam(out Guid group);
            int SetGroupingParam(ref Guid group,ref Guid context);
            int RegisterAudioSessionNotification(IntPtr notification);
            int UnregisterAudioSessionNotification(IntPtr notification);
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
            int GetProcessId(out uint process);
            [PreserveSig]int IsSystemSoundsSession();
            int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)]bool optOut);
        }
        [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface SessionVolume
        {
            int SetMasterVolume(float level,ref Guid context);
            int GetMasterVolume(out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);
            int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);
        }
        static readonly Guid Origin=new Guid("3C9C6F52-1F4E-4C8C-9B0A-5B7E2D4A6C11");

        // The name of the app inside the media source id. "Spotify.exe" gives spotify, "Microsoft.ZuneMusic_8wekyb3d8bbwe!App" gives microsoft.zunemusic.
        internal static string Key(string source)
        {
            if(string.IsNullOrWhiteSpace(source))return null;
            string key=source.Trim();
            int bang=key.IndexOf('!');
            if(bang>0)key=key.Substring(0,bang);
            int underscore=key.IndexOf('_');
            if(underscore>0)key=key.Substring(0,underscore);
            if(key.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))key=key.Substring(0,key.Length-4);
            return key.Length==0?null:key.ToLowerInvariant();
        }
        // Whether a process belongs to the app that a media source id names. Matches by program name, then by where the program lives.
        internal static bool Belongs(string key,string processName,string imagePath)
        {
            if(key==null)return false;
            string name=(processName??"").ToLowerInvariant();
            // A source such as "Brave.4f2a" or "chrome" names the program at its start.
            string head=key;
            int dot=key.IndexOf('.');
            if(dot>0&&key.Substring(0,dot)!="microsoft"&&key.Substring(0,dot)!="windows")head=key.Substring(0,dot);
            if(name.Length>0&&(name==key||name==head||(head.Length>=4&&name.StartsWith(head))||(name.Length>=4&&key.StartsWith(name))))return true;
            return imagePath!=null&&key.Length>=6&&imagePath.ToLowerInvariant().Contains(key);
        }
        // Calls use once for every session of the playing app. Returns how many sessions matched; nothing changes when none do.
        static int Each(string source,Func<SessionVolume,bool> use)
        {
            string key=Key(source);
            if(key==null)return 0;
            object enumerator=null,endpoint=null,managerObject=null;
            int matched=0;
            try
            {
                enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                Device device;
                Marshal.ThrowExceptionForHR(((Enumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device));
                endpoint=device;
                Guid managerId=typeof(SessionManager).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref managerId,23,IntPtr.Zero,out managerObject));
                SessionList list;
                Marshal.ThrowExceptionForHR(((SessionManager)managerObject).GetSessionEnumerator(out list));
                try
                {
                    int count;
                    Marshal.ThrowExceptionForHR(list.GetCount(out count));
                    for(int i=0;i<count;i++)
                    {
                        object session=null;
                        try
                        {
                            if(list.GetSession(i,out session)<0)continue;
                            var info=(SessionInfo)session;
                            if(info.IsSystemSoundsSession()==0)continue;
                            uint pid;
                            if(info.GetProcessId(out pid)<0||pid==0)continue;
                            string name=null,path=null;
                            try{using(var process=Process.GetProcessById((int)pid))name=process.ProcessName;}catch(Exception){}
                            try{path=ResolutionSwitcher.ProcessIdentity.ImagePath((int)pid);}catch(Exception){}
                            if(!Belongs(key,name,path))continue;
                            if(use((SessionVolume)session))matched++;
                        }
                        catch(Exception){}
                        finally{if(session!=null)try{Marshal.ReleaseComObject(session);}catch(Exception){}}
                    }
                }
                finally{try{Marshal.ReleaseComObject(list);}catch(Exception){}}
            }
            catch(Exception){return matched;}
            finally
            {
                foreach(object item in new[]{managerObject,endpoint,enumerator})if(item!=null)try{Marshal.ReleaseComObject(item);}catch(Exception){}
            }
            return matched;
        }
        // The loudest level among the app's sessions, and whether all of them are muted. False when the app has no audio session yet.
        public static bool TryGet(string source,out float level,out bool muted)
        {
            float best=0;bool allMuted=true;
            int found=Each(source,volume=>
            {
                float read;bool mute;
                if(volume.GetMasterVolume(out read)<0||volume.GetMute(out mute)<0)return false;
                if(read>best)best=read;
                if(!mute)allMuted=false;
                return true;
            });
            level=best;muted=found>0&&allMuted;
            return found>0;
        }
        public static bool TrySet(string source,float level)
        {
            float clamped=Math.Max(0,Math.Min(1,level));
            return Each(source,volume=>{Guid context=Origin;return volume.SetMasterVolume(clamped,ref context)>=0;})>0;
        }
        public static bool TryMute(string source,bool muted)
        {
            return Each(source,volume=>{Guid context=Origin;return volume.SetMute(muted,ref context)>=0;})>0;
        }
    }
}
