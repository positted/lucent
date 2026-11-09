using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ResolutionSwitcher
{
    // Full-screen GPU scaling using NVIDIA's documented display-config API.
    // Read the whole configuration and change only the selected target's scale.
    [StructLayout(LayoutKind.Sequential, Pack = 8)] internal struct NvConfigPath
    {
        public uint Version, SourceId, TargetCount;
        public IntPtr Targets, Source;
        public uint Flags;
        public IntPtr Adapter;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 8)] internal struct NvConfigTarget
    {
        public uint DisplayId; public IntPtr Details; public uint TargetId;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 8)] internal struct NvConfigDetails
    {
        public uint Version, Rotation, Scaling, RefreshMilliHz, Flags, Connector, TvFormat, TimingOverride;
        public NvTiming Timing;
    }
    internal sealed class NvidiaScalingConfig : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetConfig(ref uint count, IntPtr paths);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetConfig(uint count, IntPtr paths, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DisplayId([MarshalAs(UnmanagedType.LPStr)] string device, out uint id);
        private readonly CustomNvApi api;
        private readonly List<IntPtr> allocations = new List<IntPtr>();
        private IntPtr paths, selectedDetails;
        private uint count;
        private bool sharedSource;
        private readonly string deviceName;
        private readonly SetConfig set;
        private readonly int pathSize = Marshal.SizeOf(typeof(NvConfigPath));
        private readonly int targetSize = Marshal.SizeOf(typeof(NvConfigTarget));
        public NvidiaScalingConfig(string device)
        {
            deviceName=device;
            api = new CustomNvApi();
            try
            {
                GetConfig get = api.Bind<GetConfig>(0x11ABCCF8); set = api.Bind<SetConfig>(0x5D8CF8DE);
                uint selected; CustomNvApi.Check(api.Bind<DisplayId>(0xAE457190)(device, out selected));
                CustomNvApi.Check(get(ref count, IntPtr.Zero));
                if (count == 0 || count > 64) throw new InvalidOperationException("Unsupported NVIDIA display configuration.");
                uint allocatedCount = count;
                paths = Allocate(checked((int)count * pathSize));
                for (int i = 0; i < count; i++)
                    Marshal.StructureToPtr(new NvConfigPath { Version = (uint)pathSize | 0x20000u }, IntPtr.Add(paths, i * pathSize), false);
                CustomNvApi.Check(get(ref count, paths));
                if (count != allocatedCount) throw new InvalidOperationException("Display layout changed while reading scaling. Retry after it settles.");
                uint[] capacities = new uint[count];
                for (int i = 0; i < count; i++)
                {
                    IntPtr pointer = IntPtr.Add(paths, i * pathSize);
                    NvConfigPath path = (NvConfigPath)Marshal.PtrToStructure(pointer, typeof(NvConfigPath));
                    if (path.TargetCount == 0 || path.TargetCount > 64) throw new InvalidOperationException("Unsupported display target count.");
                    capacities[i] = path.TargetCount;
                    path.Targets = Allocate(checked((int)path.TargetCount * targetSize));
                    path.Source = Allocate(32); // NV_DISPLAYCONFIG_SOURCE_MODE_INFO_V1.
                    if ((path.Flags & 1) != 0) path.Adapter = Allocate(8); // Non-NVIDIA adapter LUID.
                    for (int j = 0; j < path.TargetCount; j++)
                    {
                        IntPtr details = IntPtr.Zero;
                        if ((path.Flags & 1) == 0)
                        {
                            details = Allocate(Marshal.SizeOf(typeof(NvConfigDetails)));
                            Marshal.WriteInt32(details, Marshal.SizeOf(typeof(NvConfigDetails)) | 0x10000);
                        }
                        Marshal.StructureToPtr(new NvConfigTarget { Details = details }, IntPtr.Add(path.Targets, j * targetSize), false);
                    }
                    Marshal.StructureToPtr(path, pointer, false);
                }
                CustomNvApi.Check(get(ref count, paths));
                if (count != allocatedCount) throw new InvalidOperationException("Display configuration changed during the read.");
                int matches = 0;
                for (int i = 0; i < count; i++)
                {
                    NvConfigPath path = (NvConfigPath)Marshal.PtrToStructure(IntPtr.Add(paths, i * pathSize), typeof(NvConfigPath));
                    if (path.TargetCount != capacities[i]) throw new InvalidOperationException("Display targets changed during the read.");
                    for (int j = 0; j < path.TargetCount; j++)
                    {
                        NvConfigTarget target = (NvConfigTarget)Marshal.PtrToStructure(IntPtr.Add(path.Targets, j * targetSize), typeof(NvConfigTarget));
                        if (target.DisplayId == selected && target.Details != IntPtr.Zero) { selectedDetails = target.Details; sharedSource = path.TargetCount > 1; matches++; }
                    }
                }
                if (matches != 1) throw new InvalidOperationException("Could not uniquely identify the selected NVIDIA monitor for scaling.");
            }
            catch { Dispose(); throw; }
        }
        private IntPtr Allocate(int bytes)
        {
            IntPtr pointer = Marshal.AllocHGlobal(bytes); allocations.Add(pointer);
            Marshal.Copy(new byte[bytes], 0, pointer, bytes); return pointer;
        }
        public int CurrentScaling { get { return Marshal.ReadInt32(selectedDetails, 8); } }
        public NvTiming CurrentSignalTiming { get { return ((NvConfigDetails)Marshal.PtrToStructure(selectedDetails, typeof(NvConfigDetails))).Timing; } }
        public bool SharesSource { get { return sharedSource; } }
        internal static bool SetFullscreenField(byte[] details)
        {
            if (details == null || details.Length != 128) throw new ArgumentException("Expected NV display target details.");
            int original = BitConverter.ToInt32(details, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(2), 0, details, 8, 4); // Force GPU — Full Screen.
            return original != 2;
        }
        public void ApplyFullscreen()
        {
            ApplyScaling(2);
        }
        internal static bool SetScalingField(byte[] details,int scaling)
        {
            if(details==null||details.Length!=128)throw new ArgumentException("Expected NV display target details.");
            if(scaling!=2&&scaling!=3&&scaling!=5&&scaling!=8)throw new ArgumentOutOfRangeException("scaling");
            int original=BitConverter.ToInt32(details,8);
            Buffer.BlockCopy(BitConverter.GetBytes(scaling),0,details,8,4);return original!=scaling;
        }
        public void ApplyScaling(int scaling)
        {
            if(sharedSource)throw new InvalidOperationException("Choose an extended display instead of a cloned display to change scaling independently.");
            if (CurrentScaling == scaling) return;
            byte[] previous = new byte[Marshal.SizeOf(typeof(NvConfigDetails))];
            Marshal.Copy(selectedDetails, previous, 0, previous.Length);
            byte[] changed = (byte[])previous.Clone(); SetScalingField(changed,scaling);
            Marshal.Copy(changed, 0, selectedDetails, changed.Length);
            // This driver's VALIDATE_ONLY call can change scaling despite returning
            // NVAPI_ERROR. Never probe modes on page load. Only write on explicit click.
            try
            {
                int result=set(count,paths,2);
                using(var readback=new NvidiaScalingConfig(deviceName))
                    if(readback.CurrentScaling==scaling)return;
                throw new InvalidOperationException("The driver did not confirm the selected scaling mode ("+result+").");
            }
            catch
            {
                Marshal.Copy(previous, 0, selectedDetails, previous.Length);
                // A failing driver call may have changed something. Attempt to
                // restore this same configuration; keep the original error.
                try { set(count, paths, 2); } catch { }
                throw;
            }
        }
        public void Dispose()
        {
            foreach (IntPtr pointer in allocations) Marshal.FreeHGlobal(pointer);
            allocations.Clear(); if (api != null) api.Dispose();
        }
    }
}
