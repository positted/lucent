using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ResolutionSwitcher;
internal static class SignalProbe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DisplayId([MarshalAs(UnmanagedType.LPStr)] string device, out uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetTiming(uint id, ref NvTimingInput input, out NvTiming timing);
    [STAThread] private static void Main()
    {
        foreach (Screen screen in Screen.AllScreens)
        {
            using (NvidiaScalingConfig config = new NvidiaScalingConfig(screen.DeviceName))
            {
                NvTiming timing = config.CurrentSignalTiming;
                Console.WriteLine("CONFIG READ ONLY {0}: {1}x{2}, clock={3}, milliHz={4}, total={5}x{6}", screen.DeviceName, timing.HVisible, timing.VVisible, timing.PixelClock, timing.Extra.RefreshMilliHz, timing.HTotal, timing.VTotal);
            }
            using (CustomNvApi api = new CustomNvApi())
            {
                uint id; CustomNvApi.Check(api.Bind<DisplayId>(0xAE457190)(screen.DeviceName, out id));
                NvTimingInput input = new NvTimingInput { Version = (uint)Marshal.SizeOf(typeof(NvTimingInput)) | 0x10000u, Type = 0 };
                NvTiming timing; int status = api.Bind<GetTiming>(0x175167E9)(id, ref input, out timing);
                Console.WriteLine("CURRENT READ ONLY {0}: status={1}, {2}x{3}, clock={4}, milliHz={5}, total={6}x{7}", screen.DeviceName, status, timing.HVisible, timing.VVisible, timing.PixelClock, timing.Extra.RefreshMilliHz, timing.HTotal, timing.VTotal);
            }
        }
    }
}
