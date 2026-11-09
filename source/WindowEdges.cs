using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Lucent
{
    internal static class WindowEdges
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        // Paint through the window edges and let DWM clip the native window to rounded corners.
        // A second, differently rounded WPF border would leave black corner wedges.
        internal static void Round(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;
            int noBorder=unchecked((int)0xfffffffe),round=2;
            DwmSetWindowAttribute(handle,34,ref noBorder,sizeof(int));
            DwmSetWindowAttribute(handle,33,ref round,sizeof(int));
        }
    }
}
