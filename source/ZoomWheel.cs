using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lucent
{
    // Reads the mouse wheel while zoom is on, so it can change the zoom level, and keeps the wheel from also scrolling or switching
    // weapons in whatever is underneath. It exists only while zoom is on, only sees the wheel (no clicks, no keys, no movement), and
    // sends nothing anywhere.
    internal sealed class ZoomWheelHook:IDisposable
    {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
        [StructLayout(LayoutKind.Sequential)]struct MouseData{public Point Position;public uint Data,Flags,Time;public IntPtr Extra;}
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int kind,HookProc proc,IntPtr module,uint thread);
        [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
        const int LowLevelMouse=14;
        const int Wheel=0x020A;
        readonly Action<int> turned;
        HookProc proc;
        IntPtr hook;

        // turned gets the wheel's movement: 120 for each notch away from the user (zoom in), -120 toward the user.
        public ZoomWheelHook(Action<int> turned){this.turned=turned;}
        public bool Start()
        {
            if(hook!=IntPtr.Zero)return true;
            proc=Callback;
            using(var process=Process.GetCurrentProcess())hook=SetWindowsHookEx(LowLevelMouse,proc,GetModuleHandle(process.MainModule.ModuleName),0);
            return hook!=IntPtr.Zero;
        }
        IntPtr Callback(int code,IntPtr message,IntPtr data)
        {
            if(code>=0&&message.ToInt32()==Wheel)
            {
                var info=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                int delta=(short)(info.Data>>16);
                if(delta!=0)
                {
                    try{turned(delta);}catch(Exception){}
                    // The wheel belongs to zoom while zoom is on.
                    return new IntPtr(1);
                }
            }
            return CallNextHookEx(hook,code,message,data);
        }
        public void Dispose()
        {
            if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
            proc=null;
        }
    }
}
