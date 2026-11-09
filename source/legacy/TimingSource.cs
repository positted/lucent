using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Any(arg => string.Equals(arg, "--toggle", StringComparison.OrdinalIgnoreCase)))
            {
                RunQuickToggle();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void RunQuickToggle()
        {
            try
            {
                SwitcherSettings settings = SwitcherSettings.Load();
                DisplayInfo display = DisplayService.GetDisplays().FirstOrDefault(candidate => candidate.IsPrimary)
                    ?? DisplayService.GetDisplays().FirstOrDefault();

                if (display == null)
                {
                    Environment.ExitCode = 10;
                    return;
                }

                DisplayMode current = DisplayService.GetCurrentMode(display.DeviceName);
                List<DisplayMode> modes = DisplayService.GetModes(display.DeviceName);
                DisplayMode modeA = modes.FirstOrDefault(mode => mode.Width == settings.ModeAWidth && mode.Height == settings.ModeAHeight && mode.Frequency == settings.ModeARefresh)
                    ?? DisplayService.FindBestMode(modes, settings.ModeAWidth, settings.ModeAHeight, current);
                DisplayMode modeB = modes.FirstOrDefault(mode => mode.Width == settings.ModeBWidth && mode.Height == settings.ModeBHeight && mode.Frequency == settings.ModeBRefresh)
                    ?? DisplayService.FindBestMode(modes, settings.ModeBWidth, settings.ModeBHeight, current);

                if (current == null || modeA == null || modeB == null)
                {
                    Environment.ExitCode = 11;
                    return;
                }

                DisplayMode target;
                if (modeA.HasResolution(modeB))
                {
                    target = current.HasResolution(modeA) && current.Frequency == modeA.Frequency ? modeB : modeA;
                }
                else
                {
                    target = current.HasResolution(modeA) ? modeB : current.HasResolution(modeB) ? modeA : null;
                }
                if (target == null)
                {
                    Environment.ExitCode = 12;
                    return;
                }

                DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
                Environment.ExitCode = result.IsSuccess ? 0 : 20 + Math.Abs(result.Code);
            }
            catch
            {
                Environment.ExitCode = 99;
            }
        }
    }

    internal sealed class SwitcherSettings
    {
        private const string SettingsFolderName = "ResolutionSwitcher";
        private const string SettingsFileName = "settings.ini";

        public string DeviceName = string.Empty;
        public int ModeAWidth = 1920;
        public int ModeAHeight = 1080;
        public int ModeARefresh = 0;
        public int ModeBWidth = 1440;
        public int ModeBHeight = 1080;
        public int ModeBRefresh = 0;
        public string RecentModes = string.Empty;
        public int FavoriteWidth = 0;
        public int FavoriteHeight = 0;
        public string FavoriteResolutions = string.Empty;
        public bool PreferGpuScaling = true;
        public bool RememberAfterRestart = false;

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SettingsFolderName,
                    SettingsFileName);
            }
        }

        public static SwitcherSettings Load()
        {
            SwitcherSettings settings = new SwitcherSettings();

            try
            {
                if (!File.Exists(FilePath))
                {
                    return settings;
                }

                Dictionary<string, string> values = File.ReadAllLines(FilePath)
                    .Where(line => line.Contains("="))
                    .Select(line => line.Split(new[] { '=' }, 2))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

                settings.DeviceName = ReadString(values, "DeviceName", settings.DeviceName);
                settings.ModeAWidth = ReadInt(values, "ModeAWidth", settings.ModeAWidth);
                settings.ModeAHeight = ReadInt(values, "ModeAHeight", settings.ModeAHeight);
                settings.ModeARefresh = ReadInt(values, "ModeARefresh", settings.ModeARefresh, true);
                settings.ModeBWidth = ReadInt(values, "ModeBWidth", settings.ModeBWidth);
                settings.ModeBHeight = ReadInt(values, "ModeBHeight", settings.ModeBHeight);
                settings.ModeBRefresh = ReadInt(values, "ModeBRefresh", settings.ModeBRefresh, true);
                settings.RecentModes = ReadString(values, "RecentModes", settings.RecentModes);
                settings.FavoriteWidth = ReadInt(values, "FavoriteWidth", settings.FavoriteWidth);
                settings.FavoriteHeight = ReadInt(values, "FavoriteHeight", settings.FavoriteHeight);
                settings.FavoriteResolutions = ReadString(values, "FavoriteResolutions", settings.FavoriteResolutions);
                settings.PreferGpuScaling = ReadBool(values, "PreferGpuScaling", settings.PreferGpuScaling);
                settings.RememberAfterRestart = ReadBool(values, "RememberAfterRestart", settings.RememberAfterRestart);
            }
            catch
            {
                // A damaged preference file should never stop the switcher from opening.
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(FilePath);
                Directory.CreateDirectory(folder);
                File.WriteAllLines(FilePath, new[]
                {
                    "DeviceName=" + DeviceName,
                    "ModeAWidth=" + ModeAWidth,
                    "ModeAHeight=" + ModeAHeight,
                    "ModeARefresh=" + ModeARefresh,
                    "ModeBWidth=" + ModeBWidth,
                    "ModeBHeight=" + ModeBHeight,
                    "ModeBRefresh=" + ModeBRefresh,
                    "RecentModes=" + RecentModes,
                    "FavoriteWidth=" + FavoriteWidth,
                    "FavoriteHeight=" + FavoriteHeight,
                    "FavoriteResolutions=" + FavoriteResolutions,
                    "PreferGpuScaling=" + PreferGpuScaling,
                    "RememberAfterRestart=" + RememberAfterRestart
                });
            }
            catch
            {
                // Preferences are optional. The current click should still work if they cannot be written.
            }
        }

        private static string ReadString(IDictionary<string, string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : fallback;
        }

        private static int ReadInt(IDictionary<string, string> values, string key, int fallback, bool allowZero = false)
        {
            string value;
            int parsed;
            return values.TryGetValue(key, out value) && int.TryParse(value, out parsed) && (allowZero ? parsed >= 0 : parsed > 0) ? parsed : fallback;
        }

        private static bool ReadBool(IDictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool parsed;
            return values.TryGetValue(key, out value) && bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        public bool IsFavorite(int width, int height)
        {
            string key = width + "x" + height;
            if (!string.IsNullOrWhiteSpace(FavoriteResolutions))
            {
                return FavoriteResolutions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
            }
            return FavoriteWidth == width && FavoriteHeight == height;
        }

        public void ToggleFavorite(int width, int height)
        {
            string key = width + "x" + height;
            HashSet<string> favorites = new HashSet<string>(
                FavoriteResolutions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            if (FavoriteWidth > 0 && FavoriteHeight > 0)
            {
                favorites.Add(FavoriteWidth + "x" + FavoriteHeight);
                FavoriteWidth = 0;
                FavoriteHeight = 0;
            }

            if (!favorites.Add(key))
            {
                favorites.Remove(key);
            }
            FavoriteResolutions = string.Join(";", favorites.OrderBy(item => item));
        }
    }

    internal sealed class DisplayInfo
    {
        public string DeviceName { get; private set; }
        public string FriendlyName { get; private set; }
        public bool IsPrimary { get; private set; }

        public DisplayInfo(string deviceName, string friendlyName, bool isPrimary)
        {
            DeviceName = deviceName;
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? deviceName : friendlyName;
            IsPrimary = isPrimary;
        }

        public override string ToString()
        {
            return FriendlyName + (IsPrimary ? "  •  Primary" : string.Empty);
        }
    }

    internal sealed class DisplayMode
    {
        internal DEVMODE NativeMode;

        public string DeviceName { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int BitsPerPixel { get; private set; }
        public int Frequency { get; private set; }

        public DisplayMode(string deviceName, DEVMODE nativeMode)
        {
            DeviceName = deviceName;
            NativeMode = nativeMode;
            Width = nativeMode.dmPelsWidth;
            Height = nativeMode.dmPelsHeight;
            BitsPerPixel = nativeMode.dmBitsPerPel;
            Frequency = nativeMode.dmDisplayFrequency;
        }

        public bool HasResolution(DisplayMode other)
        {
            return other != null && Width == other.Width && Height == other.Height;
        }

        public string ResolutionText
        {
            get { return Width + " × " + Height; }
        }

        public string DetailText
        {
            get
            {
                string frequency = Frequency > 1 ? Frequency + " Hz" : "Default refresh";
                return frequency;
            }
        }

        public override string ToString()
        {
            return ResolutionText + "  •  " + DetailText;
        }
    }

    internal sealed class DisplayChangeResult
    {
        public int Code { get; private set; }
        public bool UsedScalingRequest { get; private set; }
        public bool FellBackWithoutScaling { get; private set; }
        public bool UsedNativeOutputFastPath { get; private set; }

        public bool IsSuccess
        {
            get { return Code == DisplayService.DISP_CHANGE_SUCCESSFUL; }
        }

        public DisplayChangeResult(int code, bool usedScalingRequest, bool fellBackWithoutScaling, bool usedNativeOutputFastPath = false)
        {
            Code = code;
            UsedScalingRequest = usedScalingRequest;
            FellBackWithoutScaling = fellBackWithoutScaling;
            UsedNativeOutputFastPath = usedNativeOutputFastPath;
        }

        public string ErrorMessage
        {
            get
            {
                switch (Code)
                {
                    case DisplayService.DISP_CHANGE_RESTART:
                        return "Windows accepted the mode, but it needs a restart to finish applying it.";
                    case DisplayService.DISP_CHANGE_BADMODE:
                        return "This resolution is not accepted by the selected display or driver.";
                    case DisplayService.DISP_CHANGE_BADFLAGS:
                        return "The display driver rejected the requested settings.";
                    case DisplayService.DISP_CHANGE_BADPARAM:
                        return "Windows could not read the requested display settings.";
                    case DisplayService.DISP_CHANGE_NOTUPDATED:
                        return "Windows could not save the requested display settings.";
                    case DisplayService.DISP_CHANGE_FAILED:
                        return "Windows could not switch the display mode.";
                    default:
                        return "Windows could not switch the display mode (error " + Code + ").";
                }
            }
        }
    }

    internal sealed class ResolutionChoice
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsFavorite { get; set; }

        public ResolutionChoice(int width, int height, bool isFavorite)
        {
            Width = width;
            Height = height;
            IsFavorite = isFavorite;
        }

        public override string ToString()
        {
            return (IsFavorite ? "★  " : string.Empty) + Width + " × " + Height;
        }
    }

    internal sealed class RefreshChoice
    {
        public int Hertz { get; private set; }

        public RefreshChoice(int hertz)
        {
            Hertz = hertz;
        }

        public override string ToString()
        {
            return Hertz > 1 ? Hertz + " Hz" : "Default Hz";
        }
    }

    internal static class DisplayService
    {
        internal const int DISP_CHANGE_SUCCESSFUL = 0;
        internal const int DISP_CHANGE_RESTART = 1;
        internal const int DISP_CHANGE_FAILED = -1;
        internal const int DISP_CHANGE_BADMODE = -2;
        internal const int DISP_CHANGE_NOTUPDATED = -3;
        internal const int DISP_CHANGE_BADFLAGS = -4;
        internal const int DISP_CHANGE_BADPARAM = -5;

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_TEST = 0x00000002;
        private const int CDS_UPDATEREGISTRY = 0x00000001;

        private const int DM_BITSPERPEL = 0x00040000;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;
        private const int DM_DISPLAYFIXEDOUTPUT = unchecked((int)0x20000000);
        private const int DMDFO_DEFAULT = 0;
        private const int DMDFO_STRETCH = 1;

        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
        private const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, int dwFlags);

        [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwFlags, IntPtr lParam);

        public static List<DisplayInfo> GetDisplays()
        {
            List<DisplayInfo> displays = new List<DisplayInfo>();

            for (uint index = 0; index < 32; index++)
            {
                DISPLAY_DEVICE device = new DISPLAY_DEVICE();
                device.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                if (!EnumDisplayDevices(null, index, ref device, 0))
                {
                    break;
                }

                bool attached = (device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
                bool mirroring = (device.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0;
                if (!attached || mirroring)
                {
                    continue;
                }

                bool primary = (device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                displays.Add(new DisplayInfo(device.DeviceName, device.DeviceString, primary));
            }

            return displays.OrderByDescending(display => display.IsPrimary).ThenBy(display => display.FriendlyName).ToList();
        }

        public static DisplayInfo FindDisplay(string deviceName)
        {
            List<DisplayInfo> displays = GetDisplays();
            DisplayInfo chosen = displays.FirstOrDefault(display => string.Equals(display.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            return chosen ?? displays.FirstOrDefault(display => display.IsPrimary) ?? displays.FirstOrDefault();
        }

        public static DisplayMode GetCurrentMode(string deviceName)
        {
            DEVMODE mode = CreateDevMode();
            return EnumDisplaySettingsEx(deviceName, ENUM_CURRENT_SETTINGS, ref mode, 0) ? new DisplayMode(deviceName, mode) : null;
        }

        public static List<DisplayMode> GetModes(string deviceName)
        {
            List<DisplayMode> modes = new List<DisplayMode>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < 8192; index++)
            {
                DEVMODE nativeMode = CreateDevMode();
                if (!EnumDisplaySettingsEx(deviceName, index, ref nativeMode, 0))
                {
                    break;
                }

                if (nativeMode.dmPelsWidth < 1 || nativeMode.dmPelsHeight < 1)
                {
                    continue;
                }

                DisplayMode mode = new DisplayMode(deviceName, nativeMode);
                string key = mode.Width + ":" + mode.Height + ":" + mode.Frequency + ":" + mode.BitsPerPixel;
                if (seen.Add(key))
                {
                    modes.Add(mode);
                }
            }

            return modes
                .OrderByDescending(mode => (long)mode.Width * mode.Height)
                .ThenByDescending(mode => mode.Frequency)
                .ThenByDescending(mode => mode.BitsPerPixel)
                .ToList();
        }

        public static DisplayMode FindBestMode(IEnumerable<DisplayMode> modes, int width, int height, DisplayMode current)
        {
            List<DisplayMode> candidates = modes
                .Where(mode => mode.Width == width && mode.Height == height)
                .ToList();

            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderBy(mode => current != null && mode.Frequency == current.Frequency ? 0 : 1)
                .ThenBy(mode => current != null ? Math.Abs(mode.Frequency - current.Frequency) : 0)
                .ThenBy(mode => current != null && mode.BitsPerPixel == current.BitsPerPixel ? 0 : 1)
                .ThenByDescending(mode => mode.Frequency)
                .ThenByDescending(mode => mode.BitsPerPixel)
                .First();
        }

        public static DisplayChangeResult Apply(DisplayMode target, DisplayMode current, bool preferGpuScaling, bool rememberAfterRestart)
        {
            DEVMODE requested = target.NativeMode;
            requested.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            requested.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;

            if (requested.dmBitsPerPel > 0)
            {
                requested.dmFields |= DM_BITSPERPEL;
            }

            if (requested.dmDisplayFrequency > 1)
            {
                requested.dmFields |= DM_DISPLAYFREQUENCY;
            }

            bool shouldRequestScaling = preferGpuScaling && current != null &&
                (long)target.Width * target.Height < (long)current.Width * current.Height;

            if (shouldRequestScaling)
            {
                requested.dmDisplayFixedOutput = DMDFO_STRETCH;
                requested.dmFields |= DM_DISPLAYFIXEDOUTPUT;
            }

            int testResult = ChangeDisplaySettingsEx(target.DeviceName, ref requested, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            bool fellBack = false;

            if (testResult != DISP_CHANGE_SUCCESSFUL && shouldRequestScaling)
            {
                requested.dmDisplayFixedOutput = DMDFO_DEFAULT;
                requested.dmFields &= ~DM_DISPLAYFIXEDOUTPUT;
                fellBack = true;
                testResult = ChangeDisplaySettingsEx(target.DeviceName, ref requested, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            }

            if (testResult != DISP_CHANGE_SUCCESSFUL)
            {
                return new DisplayChangeResult(testResult, shouldRequestScaling && !fellBack, fellBack);
            }

            if (shouldRequestScaling)
            {
                int validation;
                int nativeOutputResult = CcdFastPath.TryApply((uint)target.Width, (uint)target.Height, rememberAfterRestart, out validation);
                if (nativeOutputResult == 0)
                {
                    return new DisplayChangeResult(DISP_CHANGE_SUCCESSFUL, true, false, true);
                }
            }

            int applyFlags = rememberAfterRestart ? CDS_UPDATEREGISTRY : 0;
            int result = ChangeDisplaySettingsEx(target.DeviceName, ref requested, IntPtr.Zero, applyFlags, IntPtr.Zero);
            return new DisplayChangeResult(result, shouldRequestScaling && !fellBack, fellBack);
        }

        private static DEVMODE CreateDevMode()
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return mode;
        }
    }

    // Windows' modern display-configuration API keeps the physical target timing
    // separate from the desktop's source size. On a compatible GPU this lets a
    // smaller desktop be scaled to the current monitor signal instead of forcing
    // the monitor to retrain. It deliberately declines extended multi-source
    // layouts; the legacy path below is safer in that case.
    internal static class CcdFastPath
    {
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_NOT_FOUND = 1168;
        private const int ERROR_BAD_CONFIGURATION = 1610;
        private const int ERROR_UNSAFE_MULTIPLE_DESKTOP_SOURCES = -10001;

        private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
        private const uint DISPLAYCONFIG_PATH_SUPPORT_VIRTUAL_MODE = 0x00000008;
        private const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;
        private const uint DISPLAYCONFIG_SCALING_STRETCHED = 3;
        private const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xffffffff;
        private const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID_16 = 0xffff;

        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint QDC_VIRTUAL_MODE_AWARE = 0x00000010;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint SDC_VALIDATE = 0x00000040;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        private const uint SDC_VIRTUAL_MODE_AWARE = 0x00008000;

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeInfoCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [Out] CcdPath[] pathInfoArray,
            ref uint modeInfoCount,
            [Out] CcdMode[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(
            uint pathCount,
            [In, Out] CcdPath[] pathArray,
            uint modeInfoCount,
            [In, Out] CcdMode[] modeInfoArray,
            uint flags);

        public static int TryApply(uint width, uint height, bool persist, out int validation)
        {
            validation = ERROR_SUCCESS;

            CcdPath[] paths;
            CcdMode[] modes;
            int readResult = Read(out paths, out modes);
            if (readResult != ERROR_SUCCESS)
            {
                return readResult;
            }

            int selectedPath = -1;
            for (int index = 0; index < paths.Length; index++)
            {
                if ((paths[index].Flags & DISPLAYCONFIG_PATH_ACTIVE) == 0)
                {
                    continue;
                }

                if (selectedPath < 0)
                {
                    selectedPath = index;
                }
                else if (!SameSource(paths[selectedPath], paths[index]))
                {
                    return ERROR_UNSAFE_MULTIPLE_DESKTOP_SOURCES;
                }
            }

            if (selectedPath < 0)
            {
                return ERROR_NOT_FOUND;
            }

            uint sourceModeIndex = SourceModeIndex(paths[selectedPath]);
            if (sourceModeIndex == DISPLAYCONFIG_PATH_MODE_IDX_INVALID ||
                sourceModeIndex == DISPLAYCONFIG_PATH_MODE_IDX_INVALID_16 ||
                sourceModeIndex >= modes.Length ||
                modes[sourceModeIndex].InfoType != DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
            {
                return ERROR_BAD_CONFIGURATION;
            }

            // Keep every target mode returned by Windows intact. Only the source
            // dimensions and scaling intent are changed.
            for (int index = 0; index < paths.Length; index++)
            {
                if ((paths[index].Flags & DISPLAYCONFIG_PATH_ACTIVE) == 0)
                {
                    continue;
                }

                paths[index].Scaling = DISPLAYCONFIG_SCALING_STRETCHED;
                if ((paths[index].Flags & DISPLAYCONFIG_PATH_SUPPORT_VIRTUAL_MODE) != 0)
                {
                    paths[index].TargetModeRaw =
                        (paths[index].TargetModeRaw & 0xffff0000) | DISPLAYCONFIG_PATH_MODE_IDX_INVALID_16;
                }
            }

            modes[sourceModeIndex].SourceWidth = width;
            modes[sourceModeIndex].SourceHeight = height;

            uint commonFlags = SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_VIRTUAL_MODE_AWARE;
            validation = SetDisplayConfig(
                (uint)paths.Length,
                paths,
                (uint)modes.Length,
                modes,
                commonFlags | SDC_VALIDATE);

            if (validation != ERROR_SUCCESS)
            {
                return validation;
            }

            uint applyFlags = commonFlags | SDC_APPLY | (persist ? SDC_SAVE_TO_DATABASE : 0);
            return SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, applyFlags);
        }

        private static int Read(out CcdPath[] paths, out CcdMode[] modes)
        {
            paths = null;
            modes = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint pathCount;
                uint modeCount;
                int result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE, out pathCount, out modeCount);
                if (result != ERROR_SUCCESS)
                {
                    return result;
                }

                CcdPath[] candidatePaths = new CcdPath[pathCount];
                CcdMode[] candidateModes = new CcdMode[modeCount];
                result = QueryDisplayConfig(
                    QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE,
                    ref pathCount,
                    candidatePaths,
                    ref modeCount,
                    candidateModes,
                    IntPtr.Zero);

                if (result == ERROR_INSUFFICIENT_BUFFER)
                {
                    continue;
                }
                if (result != ERROR_SUCCESS)
                {
                    return result;
                }

                Array.Resize(ref candidatePaths, (int)pathCount);
                Array.Resize(ref candidateModes, (int)modeCount);
                paths = candidatePaths;
                modes = candidateModes;
                return ERROR_SUCCESS;
            }

            return ERROR_INSUFFICIENT_BUFFER;
        }

        private static uint SourceModeIndex(CcdPath path)
        {
            return (path.Flags & DISPLAYCONFIG_PATH_SUPPORT_VIRTUAL_MODE) != 0
                ? path.SourceModeRaw >> 16
                : path.SourceModeRaw;
        }

        private static bool SameSource(CcdPath first, CcdPath second)
        {
            return first.SourceAdapterId.LowPart == second.SourceAdapterId.LowPart &&
                first.SourceAdapterId.HighPart == second.SourceAdapterId.HighPart &&
                first.SourceId == second.SourceId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct CcdLuid
        {
            [FieldOffset(0)] public uint LowPart;
            [FieldOffset(4)] public int HighPart;
        }

        // Native sizes are 72 bytes for DISPLAYCONFIG_PATH_INFO and 64 bytes for
        // DISPLAYCONFIG_MODE_INFO. The unexposed target signal information stays
        // byte-for-byte as Windows reported it.
        [StructLayout(LayoutKind.Explicit, Size = 72)]
        private struct CcdPath
        {
            [FieldOffset(0)] public CcdLuid SourceAdapterId;
            [FieldOffset(8)] public uint SourceId;
            [FieldOffset(12)] public uint SourceModeRaw;
            [FieldOffset(32)] public uint TargetModeRaw;
            [FieldOffset(44)] public uint Scaling;
            [FieldOffset(68)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CcdMode
        {
            [FieldOffset(0)] public uint InfoType;
            [FieldOffset(16)] public uint SourceWidth;
            [FieldOffset(20)] public uint SourceHeight;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    internal sealed class MainForm : Form
    {
        private readonly Color background = Color.FromArgb(20, 3, 35);
        private readonly Color card = Color.FromArgb(41, 8, 57);
        private readonly Color subtleText = Color.FromArgb(219, 166, 236);
        private readonly Color primaryText = Color.FromArgb(255, 240, 253);
        private readonly Color accent = Color.FromArgb(255, 54, 190);
        private readonly Color success = Color.FromArgb(53, 240, 255);
        private readonly Timer stateTimer; private readonly Timer sceneTimer = new Timer { Interval = 1 };
        private readonly System.Diagnostics.Stopwatch animationClock = System.Diagnostics.Stopwatch.StartNew();
        private double nextFrame;
        private bool precisionTimer;
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
        private void UpdateAnimationTiming()
        {
            bool active = Visible && WindowState != FormWindowState.Minimized;
            if(active && !precisionTimer) precisionTimer = timeBeginPeriod(1) == 0;
            else if(!active && precisionTimer) { timeEndPeriod(1); precisionTimer = false; }
        }

        private SwitcherSettings settings;
        private List<DisplayMode> modes = new List<DisplayMode>();
        private bool loading;

        private DarkDropDown modeAPicker;
        private DarkDropDown modeBPicker;
        private DarkDropDown modeARefreshPicker;
        private DarkDropDown modeBRefreshPicker;
        private DisplayInfo primaryDisplay;
        private Label currentResolution;
        private RefreshGlowLabel currentDetails;
        private MonitorPreview monitorPreview;
        // Kept as a nonvisual state sink so mode validation remains unchanged without a second line of UI.
        private readonly Label targetHint = new NeonLabel();
        private AccentButton switchButton;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private HotkeyWindow hotkeyWindow;
        private StartupOverlay startupOverlay;
        private bool allowExit;
        private bool hotkeyRegistered;
        private bool switchPreparation;

        public MainForm()
        {
            Text = "Lucent";
            BackColor = background;
            ForeColor = primaryText;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(720, 466);
            MinimumSize = Size;
            MaximumSize = Size;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            ShowInTaskbar = true;
            KeyPreview = true;
            try
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                Icon = SystemIcons.Application;
            }

            BuildInterface();
            sceneTimer.Tick += (sender, args) => {
                if (!Visible || WindowState == FormWindowState.Minimized || startupOverlay != null) return;
                double now = animationClock.Elapsed.TotalSeconds;
                if(now < nextFrame) return;
                nextFrame = Math.Max(nextFrame + 1.0 / 60.0, now);
                Synthwave.Phase = (float)now; Smoke.Frames++;
                Invalidate(true);
            };
            VisibleChanged += (sender,args) => UpdateAnimationTiming();
            Resize += (sender,args) => UpdateAnimationTiming();
            sceneTimer.Start();
            FormClosed += (sender, args) => { sceneTimer.Dispose(); if(precisionTimer) { timeEndPeriod(1); precisionTimer=false; } };
            Load += OnLoad;
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
            KeyDown += OnFormKeyDown;

            stateTimer = new Timer();
            stateTimer.Interval = 1800;
            stateTimer.Tick += (sender, args) => RefreshLiveDisplay(false);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        private void BuildInterface()
        {
            Panel header = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 66
            };
            header.MouseDown += BeginWindowDrag;
            Controls.Add(header);

            SymbolMark brandMark = new SymbolMark
            {
                AccentColor = accent,
                Size = new Size(32, 32),
                Location = new Point(23, 12),
                BackColor = Color.Transparent
            };
            brandMark.MouseDown += BeginWindowDrag;
            header.Controls.Add(brandMark);

            Label title = new NeonLabel
            {
                Text = "LUCENT",
                ForeColor = primaryText,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(56, 15),
                BackColor = Color.Transparent
            };
            title.MouseDown += BeginWindowDrag;
            header.Controls.Add(title);

            HeaderButton minimize = new HeaderButton("—") { Location = new Point(638, 16), HoverColor = Color.FromArgb(94, 170, 255) };
            minimize.Click += (sender, args) => WindowState = FormWindowState.Minimized;
            header.Controls.Add(minimize);

            HeaderButton close = new HeaderButton("×") { Location = new Point(676, 16), HoverColor = Color.FromArgb(255, 112, 125) };
            close.Click += (sender, args) => Close();
            header.Controls.Add(close);

            RoundedPanel liveCard = NewCard(new Rectangle(24, 84, 672, 104));
            Controls.Add(liveCard);

            Label liveKicker = NewKicker("ACTIVE DISPLAY", new Point(22, 18));
            liveCard.Controls.Add(liveKicker);

            currentResolution = new NeonLabel
            {
                Text = "Loading display…",
                ForeColor = primaryText,
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 38),
                BackColor = Color.Transparent
            };
            liveCard.Controls.Add(currentResolution);

            currentDetails = new RefreshGlowLabel
            {
                Text = string.Empty,
                ForeColor = subtleText,
                Font = new Font("Segoe UI", 10f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Size = new Size(150, 28),
                Location = new Point(286, 49),
                BackColor = Color.Transparent
            };
            liveCard.Controls.Add(currentDetails);

            monitorPreview = new MonitorPreview
            {
                Size = new Size(112, 82),
                Location = new Point(536, 10),
                BackColor = Color.Transparent
            };
            monitorPreview.Visible = false; liveCard.Controls.Add(monitorPreview);

            RoundedPanel modesCard = NewCard(new Rectangle(24, 202, 672, 144));
            Controls.Add(modesCard);

            Label modesKicker = NewKicker("TOGGLE PAIR", new Point(22, 17));
            modesCard.Controls.Add(modesKicker);

            Label modeAHeading = new NeonLabel
            {
                Text = "MODE A",
                ForeColor = subtleText,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(22, 49),
                BackColor = Color.Transparent
            };
            modesCard.Controls.Add(modeAHeading);

            Label modeAHertz = NewKicker("REFRESH RATE", new Point(193, 49));
            modesCard.Controls.Add(modeAHertz);

            Label modeBHeading = new NeonLabel
            {
                Text = "MODE B",
                ForeColor = subtleText,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(368, 49),
                BackColor = Color.Transparent
            };
            modesCard.Controls.Add(modeBHeading);

            Label modeBHertz = NewKicker("REFRESH RATE", new Point(539, 49));
            modesCard.Controls.Add(modeBHertz);

            modeAPicker = NewDropDown(new Size(165, 36));
            modeAPicker.IsFavorite = item => item is ResolutionChoice && ((ResolutionChoice)item).IsFavorite;
            modeAPicker.ToggleFavorite = item => ToggleFavorite(item as ResolutionChoice);
            modeAPicker.Location = new Point(22, 69);
            modeAPicker.SelectedIndexChanged += (sender, args) => OnResolutionChanged(modeAPicker, modeARefreshPicker);
            modesCard.Controls.Add(modeAPicker);

            modeARefreshPicker = NewDropDown(new Size(107, 36));
            modeARefreshPicker.Location = new Point(193, 69);
            modeARefreshPicker.SelectedIndexChanged += (sender, args) => OnModeChanged();
            modesCard.Controls.Add(modeARefreshPicker);

            SwapControl swapGlyph = new SwapControl
            {
                AccentColor = accent,
                Size = new Size(46, 46),
                Location = new Point(313, 64),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            swapGlyph.MouseDown += (sender, args) =>
            {
                if (args.Button != MouseButtons.Left) return;
                swapGlyph.Spin();
                SwapModes();
            };
            modesCard.Controls.Add(swapGlyph);

            modeBPicker = NewDropDown(new Size(165, 36));
            modeBPicker.IsFavorite = item => item is ResolutionChoice && ((ResolutionChoice)item).IsFavorite;
            modeBPicker.ToggleFavorite = item => ToggleFavorite(item as ResolutionChoice);
            modeBPicker.Location = new Point(368, 69);
            modeBPicker.SelectedIndexChanged += (sender, args) => OnResolutionChanged(modeBPicker, modeBRefreshPicker);
            modesCard.Controls.Add(modeBPicker);

            modeBRefreshPicker = NewDropDown(new Size(107, 36));
            modeBRefreshPicker.Location = new Point(539, 69);
            modeBRefreshPicker.SelectedIndexChanged += (sender, args) => OnModeChanged();
            modesCard.Controls.Add(modeBRefreshPicker);

            switchButton = new AccentButton
            {
                Text = "Choose two supported modes",
                Location = new Point(24, 373),
                Size = new Size(672, 56),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AccentColor = accent,
                Enabled = false
            };
            switchButton.Click += OnSwitchClicked;
            Controls.Add(switchButton);

            Controls.Add(new ShimmerLabel
            {
                Text = "Lucent  •  Made by positted",
                Size = new Size(672, 17),
                Location = new Point(24, 438),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            });

            startupOverlay = new StartupOverlay { Dock = DockStyle.Fill, BackColor = background };
            startupOverlay.Finished += (sender, args) => { Controls.Remove(startupOverlay); startupOverlay.Dispose(); startupOverlay = null; };
            Controls.Add(startupOverlay);
            startupOverlay.BringToFront();

        }

        private void OnLoad(object sender, EventArgs e)
        {
            int roundedCorners = 2;
            DwmSetWindowAttribute(Handle, 33, ref roundedCorners, Marshal.SizeOf(typeof(int)));
            int borderColor = (background.B << 16) | (background.G << 8) | background.R;
            DwmSetWindowAttribute(Handle, 34, ref borderColor, Marshal.SizeOf(typeof(int)));

            settings = SwitcherSettings.Load();
            PopulateDisplays();
            SetUpTrayIcon();
            hotkeyWindow = new HotkeyWindow(SwitchFromHotkey);
            hotkeyRegistered = hotkeyWindow.Register(Keys.F10);
            SetStatus(hotkeyRegistered ? "Ready — F10 switches from anywhere." : "Ready — F10 is already used by another app.", success);
            stateTimer.Start();
            startupOverlay.Start();
        }

        private void SetUpTrayIcon()
        {
            Icon icon = null;
            try
            {
                icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                icon = SystemIcons.Application;
            }

            trayIcon = new NotifyIcon
            {
                Icon = icon ?? SystemIcons.Application,
                Text = "Lucent — F10 to switch",
                Visible = true
            };
            trayIcon.DoubleClick += (sender, args) => ShowFromTray();
            RebuildTrayMenu();
        }

        private void RebuildTrayMenu()
        {
            if (trayIcon == null) return;
            if (trayMenu != null) trayMenu.Dispose();
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open Lucent", null, (sender, args) => ShowFromTray());
            trayMenu.Items.Add("Switch now  (F10)", null, (sender, args) => SwitchFromHotkey());
            List<string> recent = (settings == null ? string.Empty : settings.RecentModes).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Take(4).ToList();
            if (recent.Count > 0)
            {
                trayMenu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem heading = new ToolStripMenuItem("Recent switches") { Enabled = false };
                trayMenu.Items.Add(heading);
                foreach (string key in recent)
                {
                    string captured = key;
                    trayMenu.Items.Add("↩  " + FormatRecentMode(key), null, (sender, args) => SwitchToRecent(captured));
                }
            }
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (sender, args) => ExitApplication());
            trayIcon.ContextMenuStrip = trayMenu;
        }

        private void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            allowExit = true;
            trayIcon.Visible = false;
            Close();
        }

        private void RecordRecent(DisplayMode mode)
        {
            if (mode == null || settings == null) return;
            string key = mode.Width + "," + mode.Height + "," + mode.Frequency;
            List<string> entries = settings.RecentModes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Where(entry => entry != key).ToList();
            entries.Insert(0, key);
            settings.RecentModes = string.Join(";", entries.Take(4));
            settings.Save();
            RebuildTrayMenu();
        }

        private static string FormatRecentMode(string key)
        {
            string[] parts = key.Split(',');
            return parts.Length == 3 ? parts[0] + " × " + parts[1] + "  •  " + parts[2] + " Hz" : key;
        }

        private void SwitchToRecent(string key)
        {
            string[] parts = key.Split(',');
            int width, height, hertz;
            if (parts.Length != 3 || !int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height) || !int.TryParse(parts[2], out hertz) || primaryDisplay == null) return;
            DisplayMode current = DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode target = modes.FirstOrDefault(mode => mode.Width == width && mode.Height == height && mode.Frequency == hertz);
            if (current == null || target == null) return;
            DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
            if (result.IsSuccess) RecordRecent(target);
            BeginInvoke(new Action(() => RefreshLiveDisplay(false)));
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (allowExit || e.CloseReason == CloseReason.WindowsShutDown)
            {
                return;
            }

            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(1800, "Lucent is still running", "Use F10 to switch, or right-click the tray icon to open it.", ToolTipIcon.Info);
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            stateTimer.Stop();
            if (hotkeyWindow != null) hotkeyWindow.Dispose();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }

        private void PopulateDisplays()
        {
            List<DisplayInfo> displays = DisplayService.GetDisplays();
            primaryDisplay = displays.FirstOrDefault(display => display.IsPrimary)
                ?? displays.FirstOrDefault();

            if (primaryDisplay == null)
            {
                SetStatus("No active Windows display was found.", Color.FromArgb(255, 126, 136));
                return;
            }

            LoadModes(primaryDisplay, true);
        }

        private void ReloadDisplayModes()
        {
            if (primaryDisplay == null)
            {
                PopulateDisplays();
                return;
            }

            LoadModes(primaryDisplay, false);
            SetStatus("Display modes refreshed.", success);
        }

        private void SwapModes()
        {
            ResolutionChoice modeA = modeAPicker.SelectedItem as ResolutionChoice;
            ResolutionChoice modeB = modeBPicker.SelectedItem as ResolutionChoice;
            RefreshChoice refreshA = modeARefreshPicker.SelectedItem as RefreshChoice;
            RefreshChoice refreshB = modeBRefreshPicker.SelectedItem as RefreshChoice;
            if (modeA == null || modeB == null)
            {
                return;
            }

            loading = true;
            modeAPicker.SelectedItem = modeB;
            modeBPicker.SelectedItem = modeA;
            PopulateRefreshPicker(modeAPicker, modeARefreshPicker, refreshB == null ? 0 : refreshB.Hertz);
            PopulateRefreshPicker(modeBPicker, modeBRefreshPicker, refreshA == null ? 0 : refreshA.Hertz);
            loading = false;
            SaveSettings();
            RefreshLiveDisplay(true);
            SetStatus("Toggle pair swapped.", success);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }

            if (e.KeyCode == Keys.F5)
            {
                ReloadDisplayModes();
                e.SuppressKeyPress = true;
                return;
            }

            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && switchButton.Enabled && !(ActiveControl is DarkDropDown))
            {
                OnSwitchClicked(switchButton, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void SwitchFromHotkey()
        {
            DisplayMode current = primaryDisplay == null ? null : DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode modeA = SelectedMode(modeAPicker, modeARefreshPicker, current);
            DisplayMode modeB = SelectedMode(modeBPicker, modeBRefreshPicker, current);
            DisplayMode target = ChooseTarget(current, modeA, modeB);

            if (current == null || target == null)
            {
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(1600, "Lucent", "Choose a valid toggle pair before using F10.", ToolTipIcon.Warning);
                }
                return;
            }

            DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
            if (result.IsSuccess)
            {
                RecordRecent(target);
                SetStatus("F10 switched to " + target.ResolutionText + ".", success);
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(1200, "Lucent", "Switched to " + target.ResolutionText + ".", ToolTipIcon.Info);
                }
            }
            else
            {
                SetStatus(result.ErrorMessage, Color.FromArgb(255, 126, 136));
            }
            BeginInvoke(new Action(() => RefreshLiveDisplay(false)));
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)7721)
            {
                BeginInvoke(new Action(SwitchFromHotkey));
            }
            base.WndProc(ref m);
        }

        private void LoadModes(DisplayInfo display, bool firstLoad)
        {
            loading = true;
            modes = DisplayService.GetModes(display.DeviceName);
            DisplayMode current = DisplayService.GetCurrentMode(display.DeviceName);

            List<ResolutionChoice> resolutions = BuildResolutionChoices();
            modeAPicker.SetItems(resolutions);
            modeBPicker.SetItems(resolutions);

            ResolutionChoice modeA = FindResolutionChoice(resolutions, settings.ModeAWidth, settings.ModeAHeight)
                ?? FindResolutionChoice(resolutions, current == null ? 0 : current.Width, current == null ? 0 : current.Height)
                ?? resolutions.FirstOrDefault();
            ResolutionChoice modeB = FindResolutionChoice(resolutions, settings.ModeBWidth, settings.ModeBHeight)
                ?? resolutions.FirstOrDefault(choice => modeA == null || choice.Width != modeA.Width || choice.Height != modeA.Height)
                ?? modeA;

            modeAPicker.SelectedItem = modeA;
            modeBPicker.SelectedItem = modeB;
            PopulateRefreshPicker(modeAPicker, modeARefreshPicker, settings.ModeARefresh);
            PopulateRefreshPicker(modeBPicker, modeBRefreshPicker, settings.ModeBRefresh);
            loading = false;

            RefreshLiveDisplay(true);
            if (!firstLoad)
            {
                SaveSettings();
            }
        }

        private void OnModeChanged()
        {
            if (!loading)
            {
                SaveSettings();
                RefreshLiveDisplay(true);
            }
        }

        private void OnResolutionChanged(DarkDropDown resolutionPicker, DarkDropDown refreshPicker)
        {
            if (loading)
            {
                return;
            }

            PopulateRefreshPicker(resolutionPicker, refreshPicker, 0);
            SaveSettings();
            RefreshLiveDisplay(true);
        }

        private List<ResolutionChoice> BuildResolutionChoices()
        {
            return modes
                .GroupBy(mode => mode.Width + ":" + mode.Height)
                .Select(group => new ResolutionChoice(group.First().Width, group.First().Height, settings.IsFavorite(group.First().Width, group.First().Height)))
                .OrderByDescending(choice => choice.IsFavorite)
                .ThenByDescending(choice => (long)choice.Width * choice.Height)
                .ToList();
        }

        private static ResolutionChoice FindResolutionChoice(IEnumerable<ResolutionChoice> choices, int width, int height)
        {
            return choices.FirstOrDefault(choice => choice.Width == width && choice.Height == height);
        }

        private void PopulateRefreshPicker(DarkDropDown resolutionPicker, DarkDropDown refreshPicker, int preferredRefresh)
        {
            ResolutionChoice resolution = resolutionPicker.SelectedItem as ResolutionChoice;
            List<RefreshChoice> refreshes = resolution == null
                ? new List<RefreshChoice>()
                : modes.Where(mode => mode.Width == resolution.Width && mode.Height == resolution.Height)
                    .Select(mode => mode.Frequency)
                    .Distinct()
                    .OrderByDescending(frequency => frequency)
                    .Select(frequency => new RefreshChoice(frequency))
                    .ToList();

            refreshPicker.SetItems(refreshes);
            RefreshChoice selected = refreshes.FirstOrDefault(choice => choice.Hertz == preferredRefresh)
                ?? refreshes.FirstOrDefault();
            refreshPicker.SelectedItem = selected;
        }

        private DisplayMode SelectedMode(DarkDropDown resolutionPicker, DarkDropDown refreshPicker, DisplayMode current)
        {
            ResolutionChoice resolution = resolutionPicker.SelectedItem as ResolutionChoice;
            RefreshChoice refresh = refreshPicker.SelectedItem as RefreshChoice;
            if (resolution == null)
            {
                return null;
            }

            DisplayMode exact = refresh == null ? null : modes.FirstOrDefault(mode =>
                mode.Width == resolution.Width &&
                mode.Height == resolution.Height &&
                mode.Frequency == refresh.Hertz);
            return exact ?? DisplayService.FindBestMode(modes, resolution.Width, resolution.Height, current);
        }

        private void ToggleFavorite(ResolutionChoice resolution)
        {
            if (resolution == null)
            {
                return;
            }

            settings.ToggleFavorite(resolution.Width, resolution.Height);
            SaveSettings();
            bool isFavorite = settings.IsFavorite(resolution.Width, resolution.Height);
            foreach (object item in modeAPicker.Items.Concat(modeBPicker.Items))
            {
                ResolutionChoice choice = item as ResolutionChoice;
                if (choice != null && choice.Width == resolution.Width && choice.Height == resolution.Height)
                {
                    choice.IsFavorite = isFavorite;
                }
            }
            modeAPicker.Invalidate();
            modeBPicker.Invalidate();
        }

        private void RefreshLiveDisplay(bool forceStatus)
        {
            if (primaryDisplay == null)
            {
                return;
            }

            DisplayMode current = DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode modeA = SelectedMode(modeAPicker, modeARefreshPicker, current);
            DisplayMode modeB = SelectedMode(modeBPicker, modeBRefreshPicker, current);

            if (current == null)
            {
                currentResolution.Text = "Unavailable";
                currentDetails.Text = "Primary display";
                switchButton.Enabled = false;
                targetHint.Text = "Windows did not return an active mode for this display.";
                if (forceStatus)
                {
                    SetStatus("Could not read the active display mode.", Color.FromArgb(255, 126, 136));
                }
                return;
            }

            currentResolution.Text = current.ResolutionText;
            currentDetails.Text = current.DetailText;
            currentDetails.SetHertz(current.Frequency);
            monitorPreview.SetMode(current);
            ApplyRefreshColor(modeARefreshPicker, modeA == null ? 0 : modeA.Frequency);
            ApplyRefreshColor(modeBRefreshPicker, modeB == null ? 0 : modeB.Frequency);

            DisplayMode target = ChooseTarget(current, modeA, modeB);
            if (target == null)
            {
                switchButton.Enabled = false;
                switchButton.Text = "Select two different modes";
                targetHint.Text = "The active resolution must match Mode A or Mode B before it can toggle.";
                if (forceStatus)
                {
                    SetStatus("Choose a valid toggle pair.", Color.FromArgb(255, 193, 92));
                }
                return;
            }

            switchButton.Enabled = true;
            switchButton.Text = "Switch to " + target.ResolutionText + "  •  " + target.DetailText;
            switchButton.AccentColor = AccentForRefresh(target.Frequency);
            bool preserveSignal = (long)target.Width * target.Height < (long)current.Width * current.Height;
            targetHint.Text = preserveSignal
                ? "Native-signal scaling will be attempted first to keep the display signal stable."
                : target.HasResolution(current)
                    ? "This switches only the refresh rate."
                    : "The selected refresh rate is applied with your chosen resolution.";

            if (forceStatus)
            {
                SetStatus("Ready — " + current.DetailText + ".", success);
            }
        }

        private DisplayMode ChooseTarget(DisplayMode current, DisplayMode modeA, DisplayMode modeB)
        {
            if (current == null || modeA == null || modeB == null ||
                (modeA.HasResolution(modeB) && modeA.Frequency == modeB.Frequency))
            {
                return null;
            }

            if (modeA.HasResolution(modeB))
            {
                if (!current.HasResolution(modeA) || current.Frequency == modeA.Frequency)
                {
                    return modeB;
                }
                return modeA;
            }

            if (current.HasResolution(modeA))
            {
                return modeB;
            }

            if (current.HasResolution(modeB))
            {
                return modeA;
            }

            return null;
        }

        private void OnSwitchClicked(object sender, EventArgs e)
        {
            if (switchPreparation) return;
            DisplayMode current = primaryDisplay == null ? null : DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode target = ChooseTarget(current, SelectedMode(modeAPicker, modeARefreshPicker, current), SelectedMode(modeBPicker, modeBRefreshPicker, current));

            if (primaryDisplay == null || current == null || target == null)
            {
                RefreshLiveDisplay(true);
                return;
            }

            switchPreparation = true;
            switchButton.Enabled = false;
            switchButton.Text = "Preparing display switch…";
            switchButton.TriggerEffect();
            Timer launchTimer = new Timer { Interval = 430 };
            launchTimer.Tick += (timerSender, timerArgs) =>
            {
                launchTimer.Stop();
                launchTimer.Dispose();
                switchButton.Text = "Switching…";
                ExecuteSwitch(target, current);
            };
            launchTimer.Start();
        }

        private void ExecuteSwitch(DisplayMode target, DisplayMode current)
        {
            DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
            if (result.IsSuccess)
            {
                RecordRecent(target);
                string message = "Switched to " + target.ResolutionText + ".";
                if (result.UsedNativeOutputFastPath)
                {
                    message += " Kept the monitor's current output timing.";
                }
                else if (result.UsedScalingRequest)
                {
                    message += " GPU scaling was requested.";
                }
                else if (result.FellBackWithoutScaling)
                {
                    message += " Your driver declined the scaling request, so Windows used its normal mode.";
                }
                SetStatus(message, success);
            }
            else
            {
                SetStatus(result.ErrorMessage, Color.FromArgb(255, 126, 136));
            }

            switchPreparation = false;
            BeginInvoke(new Action(() => RefreshLiveDisplay(false)));
        }

        private void SaveSettings()
        {
            if (loading || settings == null)
            {
                return;
            }

            ResolutionChoice modeA = modeAPicker.SelectedItem as ResolutionChoice;
            ResolutionChoice modeB = modeBPicker.SelectedItem as ResolutionChoice;
            RefreshChoice refreshA = modeARefreshPicker.SelectedItem as RefreshChoice;
            RefreshChoice refreshB = modeBRefreshPicker.SelectedItem as RefreshChoice;

            if (primaryDisplay != null)
            {
                settings.DeviceName = primaryDisplay.DeviceName;
            }
            if (modeA != null)
            {
                settings.ModeAWidth = modeA.Width;
                settings.ModeAHeight = modeA.Height;
                settings.ModeARefresh = refreshA == null ? 0 : refreshA.Hertz;
            }
            if (modeB != null)
            {
                settings.ModeBWidth = modeB.Width;
                settings.ModeBHeight = modeB.Height;
                settings.ModeBRefresh = refreshB == null ? 0 : refreshB.Hertz;
            }

            settings.Save();
        }

        private void SetStatus(string message, Color color)
        {
            // Status is intentionally kept out of the compact window. Relevant
            // messages are surfaced through the tray when the app is hidden.
        }

        private RoundedPanel NewCard(Rectangle bounds)
        {
            return new RoundedPanel
            {
                BackColor = Color.Transparent,
                BorderColor = Color.FromArgb(156, 35, 130),
                CornerRadius = 16,
                Location = bounds.Location,
                Size = bounds.Size
            };
        }

        private Label NewKicker(string text, Point location)
        {
            return new NeonLabel
            {
                Text = text,
                ForeColor = Color.FromArgb(225, 175, 235),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Location = location,
                BackColor = Color.Transparent
            };
        }

        private DarkDropDown NewDropDown(Size size)
        {
            DarkDropDown dropdown = new DarkDropDown
            {
                Size = size,
                BackColor = Color.FromArgb(49, 10, 68),
                ForeColor = primaryText,
                BorderColor = Color.FromArgb(179, 42, 152),
                SelectedColor = Color.FromArgb(112, 28, 126),
                Font = new Font("Segoe UI", 10f)
            };
            return dropdown;
        }

        private static Color AccentForRefresh(int hertz)
        {
            return Color.FromArgb(255, 240, 253);
        }

        private static void ApplyRefreshColor(DarkDropDown dropdown, int hertz)
        {
            if (dropdown == null) return;
            Color color = AccentForRefresh(hertz);
            dropdown.ForeColor = color;
            dropdown.SelectedColor = Color.FromArgb(Math.Min(150, (int)color.R), Math.Min(150, (int)color.G), Math.Min(185, (int)color.B));
            dropdown.ItemColor = item => item is RefreshChoice ? AccentForRefresh(((RefreshChoice)item).Hertz) : dropdown.ForeColor;
            dropdown.Invalidate();
        }

        private void BeginWindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }

    internal sealed class MonitorPreview : Control
    {
        private DisplayMode mode;
        private readonly Timer pulseTimer;
        private float pulse;

        public MonitorPreview()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            pulseTimer = new Timer { Interval = 16 };
            pulseTimer.Tick += (sender, args) => { pulse -= 0.045f; if (pulse <= 0) { pulse = 0; pulseTimer.Stop(); } Invalidate(); };
        }

        public void SetMode(DisplayMode value)
        {
            if (mode == null || value == null || mode.Width != value.Width || mode.Height != value.Height || mode.Frequency != value.Frequency)
            {
                pulse = 1f;
                pulseTimer.Start();
            }
            mode = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float ratio = mode == null || mode.Height == 0 ? 16f / 9f : (float)mode.Width / mode.Height;
            int screenHeight = (int)(46 + pulse * 10);
            int screenWidth = Math.Min(90, Math.Max(44, (int)(screenHeight * ratio)));
            int x = (Width - screenWidth) / 2;
            int y = 5 + (int)((1f - pulse) * 5f);
            Color glow = mode != null && mode.Frequency >= 240 ? Color.FromArgb(255, 54, 190) : mode != null && mode.Frequency >= 144 ? Color.FromArgb(53, 240, 255) : Color.FromArgb(222, 106, 255);
            using (GraphicsPath frame = RoundedRectangle(new Rectangle(x, y, screenWidth, screenHeight), 6))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(49, 10, 68)))
            using (Pen border = new Pen(glow, 1.6f))
            using (Pen stand = new Pen(Color.FromArgb(151, 165, 194), 2f))
            {
                if (pulse > 0)
                {
                    using (Pen ring = new Pen(Color.FromArgb((int)(pulse * 100), glow), 2f))
                        e.Graphics.DrawEllipse(ring, x - 8, y - 8, screenWidth + 16, screenHeight + 16);
                }
                e.Graphics.FillPath(fill, frame);
                e.Graphics.DrawPath(border, frame);
                e.Graphics.DrawLine(stand, Width / 2, y + screenHeight + 1, Width / 2, y + screenHeight + 10);
                e.Graphics.DrawLine(stand, Width / 2 - 11, y + screenHeight + 10, Width / 2 + 11, y + screenHeight + 10);
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) pulseTimer.Dispose(); base.Dispose(disposing); }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class RefreshGlowLabel : Label
    {
        public RefreshGlowLabel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor,true); }
        public void SetHertz(int value) { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) { Synthwave.Text(e.Graphics,Text,Font,ClientRectangle,Color.FromArgb(255,240,253),false); }
    }
    internal sealed class StartupOverlay : Control
    {
        private readonly Timer timer;
        private int frame; private readonly System.Diagnostics.Stopwatch bootClock = new System.Diagnostics.Stopwatch();
        public event EventHandler Finished;

        public StartupOverlay()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            timer = new Timer { Interval = 16 };
            timer.Tick += (sender, args) =>
            {
                frame = (int)(bootClock.ElapsedMilliseconds / 16);
                Invalidate();
                if (frame >= 64)
                {
                    timer.Stop();
                    EventHandler handler = Finished;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            };
        }

        public void Start() { bootClock.Restart(); timer.Start(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Synthwave.Draw(e.Graphics, ClientRectangle, frame * .065f);
            float enter = Math.Min(1f, frame / 20f);
            int cy = Height / 2 - 55;
            Synthwave.Mark(e.Graphics, new RectangleF(Width / 2 - 42, cy - 65 + (1-enter)*18, 84, 84));
            using(Font font = new Font("Segoe UI", 32f, FontStyle.Bold))
                Synthwave.Text(e.Graphics, "R E S L Y", font, new RectangleF(0,cy+23,Width,64), Color.FromArgb(255,238,255),true);
            using(Font font = new Font("Segoe UI", 9f))
                Synthwave.Text(e.Graphics, "F I N D   Y O U R   F R E Q U E N C Y", font, new RectangleF(0,cy+88,Width,30), Color.FromArgb(230,182,242),true);
            if(frame > 48) using(SolidBrush fade = new SolidBrush(Color.FromArgb(Math.Min(255,(frame-48)*16),20,3,35))) e.Graphics.FillRectangle(fade,ClientRectangle);
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class ShimmerLabel : Control
    {
        private readonly Timer shimmerTimer;
        private int offset;

        public ShimmerLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            shimmerTimer = new Timer { Interval = 16 };
            shimmerTimer.Tick += (sender, args) => { offset = (offset + 7) % (Width + 120); Invalidate(); };
            shimmerTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            SizeF size = e.Graphics.MeasureString(Text, Font);
            float x = (Width - size.Width) / 2f;
            RectangleF textBounds = new RectangleF(x, 0, size.Width, Height);
            using (LinearGradientBrush fill = new LinearGradientBrush(textBounds, Color.FromArgb(117, 131, 164), Color.FromArgb(117, 131, 164), 0f))
            {
                float shine = x - 60 + offset;
                ColorBlend blend = new ColorBlend(5);
                blend.Colors = new[] { Color.FromArgb(117, 131, 164), Color.FromArgb(117, 131, 164), Color.FromArgb(235, 242, 255), Color.FromArgb(117, 131, 164), Color.FromArgb(117, 131, 164) };
                blend.Positions = new[] { 0f, Math.Max(0f, (shine - x - 18) / Math.Max(1f, size.Width)), Math.Max(0f, Math.Min(1f, (shine - x) / Math.Max(1f, size.Width))), Math.Max(0f, Math.Min(1f, (shine - x + 18) / Math.Max(1f, size.Width))), 1f };
                fill.InterpolationColors = blend;
                e.Graphics.DrawString(Text, Font, fill, x, 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) shimmerTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    // Owns a message-only handle. Unlike the form handle, it remains valid while the app is hidden in the tray.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int HotkeyId = 7721;
        private const int WmHotkey = 0x0312;
        private readonly Action pressed;
        private bool registered;

        public HotkeyWindow(Action pressed)
        {
            this.pressed = pressed;
            CreateHandle(new CreateParams());
        }

        public bool Register(Keys key)
        {
            registered = RegisterHotKey(Handle, HotkeyId, 0, (uint)key);
            return registered;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey && message.WParam == (IntPtr)HotkeyId && pressed != null)
            {
                pressed();
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (registered) UnregisterHotKey(Handle, HotkeyId);
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }

    internal sealed class DarkDropDown : Control
    {
        private readonly List<object> items = new List<object>();
        private object selectedItem;

        public Color BorderColor { get; set; }
        public Color SelectedColor { get; set; }
        public Func<object, Color> ItemColor { get; set; }
        public Func<object, bool> IsFavorite { get; set; }
        public Action<object> ToggleFavorite { get; set; }
        public IEnumerable<object> Items { get { return items; } }

        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                if (ReferenceEquals(selectedItem, value) || (selectedItem != null && selectedItem.Equals(value)))
                {
                    return;
                }

                selectedItem = value;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler SelectedIndexChanged;

        public DarkDropDown()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
            TabStop = true;
            BorderColor = Color.FromArgb(179, 42, 152);
            SelectedColor = Color.FromArgb(112, 28, 126);
        }

        public void SetItems(IEnumerable values)
        {
            items.Clear();
            if (values != null)
            {
                foreach (object value in values)
                {
                    items.Add(value);
                }
            }
            if (selectedItem != null && !items.Contains(selectedItem))
            {
                selectedItem = null;
            }
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            ShowOptions();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down)
            {
                ShowOptions();
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using(GraphicsPath path=RoundedPanel.RoundedPath(new Rectangle(0,0,Width-1,Height-1),8))
            using(LinearGradientBrush fill=new LinearGradientBrush(ClientRectangle,Color.FromArgb(185,109,30,124),Color.FromArgb(155,60,18,83),70f))
            using(Pen edge=new Pen(Focused ? Color.FromArgb(70,235,255) : Color.FromArgb(196,80,181),1))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(edge,path);}
            Synthwave.Text(e.Graphics, selectedItem==null ? "Choose a mode" : Convert.ToString(selectedItem), Font, new RectangleF(10,0,Width-35,Height),ForeColor,false);
            using(Pen chevron=new Pen(Color.FromArgb(229,187,242),1.5f)) e.Graphics.DrawLines(chevron,new[]{new Point(Width-21,Height/2-2),new Point(Width-16,Height/2+3),new Point(Width-11,Height/2-2)});
        }
        private void ShowOptions()
        {
            if (items.Count == 0)
            {
                return;
            }

            int popupHeight = Math.Min(460, Math.Max(42, items.Count * 30 + 4));
            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                Size = new Size(Math.Max(Width, 340), popupHeight),
                BackColor = Color.FromArgb(41, 8, 57),
                TopMost = true
            };

            Point screenPoint = PointToScreen(new Point(0, Height + 2));
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            if (screenPoint.Y + popupHeight > workArea.Bottom)
            {
                screenPoint.Y = PointToScreen(new Point(0, -popupHeight - 2)).Y;
            }
            popup.Location = screenPoint;

            ListBox list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(41, 8, 57),
                ForeColor = ForeColor,
                Font = Font,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30,
                IntegralHeight = false
            };
            list.HandleCreated += (sender, args) => SetWindowTheme(list.Handle, "DarkMode_Explorer", null);
            List<object> popupItems = items
                .OrderByDescending(item => IsFavorite != null && IsFavorite(item))
                .ThenBy(item => items.IndexOf(item))
                .ToList();
            list.Items.AddRange(popupItems.ToArray());
            list.DrawItem += (sender, args) =>
            {
                if (args.Index < 0)
                {
                    return;
                }
                bool selected = (args.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (SolidBrush brush = new SolidBrush(selected ? SelectedColor : list.BackColor))
                {
                    args.Graphics.FillRectangle(brush, args.Bounds);
                }
                bool canFavorite = ToggleFavorite != null && list.Items[args.Index] is ResolutionChoice;
                int textWidth = args.Bounds.Width - (canFavorite ? 52 : 12);
                Color itemColor = ItemColor == null ? list.ForeColor : ItemColor(list.Items[args.Index]);
                TextRenderer.DrawText(args.Graphics, Convert.ToString(list.Items[args.Index]), list.Font, new Rectangle(args.Bounds.X + 10, args.Bounds.Y, textWidth, args.Bounds.Height), itemColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (canFavorite)
                {
                    bool favorite = IsFavorite != null && IsFavorite(list.Items[args.Index]);
                    using (Font starFont = new Font("Segoe UI Symbol", 18f, FontStyle.Regular))
                    {
                        Color starColor = favorite ? Color.FromArgb(255, 207, 79) : Color.White;
                        TextRenderer.DrawText(args.Graphics, favorite ? "★" : "☆", starFont, new Rectangle(args.Bounds.Right - 38, args.Bounds.Y, 30, args.Bounds.Height), starColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
            };
            list.SelectedItem = selectedItem;
            list.MouseUp += (sender, args) =>
            {
                int index = list.IndexFromPoint(((MouseEventArgs)args).Location);
                if (index < 0)
                {
                    return;
                }
                MouseEventArgs mouse = (MouseEventArgs)args;
                object item = list.Items[index];
                if (mouse.X >= list.Width - 50 && ToggleFavorite != null && item is ResolutionChoice)
                {
                    ToggleFavorite(item);
                    list.Invalidate();
                    return;
                }
                SelectedItem = item;
                popup.Close();
            };
            popup.Deactivate += (sender, args) => popup.Close();
            popup.Controls.Add(list);
            popup.Show(this);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);
    }

    internal sealed class SymbolMark : Control
    {
        public Color AccentColor { get; set; }

        public SymbolMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            AccentColor = Color.FromArgb(102, 125, 255);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Synthwave.Mark(e.Graphics, ClientRectangle);
        }
    }

    internal sealed class SwapControl : Control
    {
        private readonly Timer spinTimer;
        private float angle;
        private float targetAngle;
        private float velocity;

        public Color AccentColor { get; set; }

        public SwapControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            AccentColor = Color.FromArgb(102, 125, 255);
            spinTimer = new Timer { Interval = 10 };
            spinTimer.Tick += (sender, args) =>
            {
                float remaining = targetAngle - angle;
                float desiredVelocity = 13f + Math.Min(37f, Math.Max(1f, remaining / 360f) * 8f);
                velocity += (desiredVelocity - velocity) * 0.16f;
                angle += Math.Min(remaining, velocity);
                Invalidate();
                if (targetAngle - angle < 0.1f)
                {
                    spinTimer.Stop();
                    angle = 0;
                    targetAngle = 0;
                    velocity = 0;
                    Invalidate();
                }
            };
        }

        public void Spin()
        {
            targetAngle += 360f;
            velocity = Math.Min(50f, velocity + 7f);
            spinTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            e.Graphics.TranslateTransform(Width / 2f, Height / 2f);
            e.Graphics.RotateTransform(angle);
            using (SolidBrush brush = new SolidBrush(AccentColor))
            using (Pen halo = new Pen(Color.FromArgb(110, 53, 240, 255), 2f))
            {
                e.Graphics.DrawEllipse(halo, -17, -17, 34, 34);
                using (Font font = new Font("Segoe UI Symbol", 18f, FontStyle.Bold))
                {
                    SizeF size = e.Graphics.MeasureString("⇄", font);
                    e.Graphics.DrawString("⇄", font, brush, -size.Width / 2f, -size.Height / 2f - 2f);
                }
            }
            e.Graphics.ResetTransform();
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }

        public RoundedPanel()
        {
            DoubleBuffered = true;
            CornerRadius = 14;
            BorderColor = Color.Transparent;
            Resize += (sender, args) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPath(bounds, CornerRadius))
            using (LinearGradientBrush fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(170, 46, 10, 65), Color.FromArgb(105, 132, 33, 139), 30f))
            using (Pen border = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
            base.OnPaint(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Synthwave.Background(e.Graphics, this);
        }

        internal static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class AccentButton : Button
    {
        private bool hovering;
        private bool pressed;
        private readonly Timer effectTimer;
        private float effect;

        public Color AccentColor { get; set; }

        public AccentButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            ForeColor = Color.White;
            BackColor = Color.Transparent;
            AccentColor = Color.FromArgb(102, 125, 255);
            DoubleBuffered = true;
            effectTimer = new Timer { Interval = 16 };
            effectTimer.Tick += (sender, args) => { effect -= 0.034f; if (effect <= 0) { effect = 0; effectTimer.Stop(); } Invalidate(); };
        }

        public void TriggerEffect()
        {
            effect = 1f;
            effectTimer.Start();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Synthwave.Background(e.Graphics, this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Synthwave.Background(e.Graphics, this);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Color fill = !Enabled
                ? Color.FromArgb(47, 56, 74)
                : pressed
                    ? Color.FromArgb(74, 92, 205)
                    : hovering
                        ? Color.FromArgb(123, 143, 255)
                        : AccentColor;

            using (GraphicsPath path = CreatePath(new Rectangle(0, 0, Width - 1, Height - 1), 14))
            {
                if (Enabled)
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(hovering ? 205 : 145, 203, 37, 176), Color.FromArgb(hovering ? 200 : 135, 75, 68, 170), LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                }
                if (effect > 0)
                {
                    using (Pen glow = new Pen(Color.FromArgb((int)(effect * 210), Color.White), 2.5f)) e.Graphics.DrawPath(glow, path);
                    int sweepX = (int)((1f - effect) * (Width + 160)) - 80;
                    GraphicsState flashState = e.Graphics.Save(); e.Graphics.SetClip(path, CombineMode.Intersect);
                    using (LinearGradientBrush flash = new LinearGradientBrush(new Rectangle(sweepX, 0, 100, Height), Color.FromArgb(0, Color.White), Color.FromArgb((int)(effect * 150), Color.White), LinearGradientMode.Horizontal)) e.Graphics.FillRectangle(flash, new Rectangle(sweepX, 0, 100, Height));
                    e.Graphics.Restore(flashState);
                }
            }

            Color textColor = Enabled ? Color.White : Color.FromArgb(147, 158, 179);
            Synthwave.Text(e.Graphics, Text, Font, ClientRectangle, textColor, true);
        }

        private static GraphicsPath CreatePath(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Lighten(Color color, int amount) { return Color.FromArgb(Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount)); }
        private static Color Darken(Color color, int amount) { return Color.FromArgb(Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount)); }

        protected override void Dispose(bool disposing) { if (disposing) effectTimer.Dispose(); base.Dispose(disposing); }
    }

    // A lightweight owner-drawn control avoids the duplicate native CheckBox
    // painting that can happen with dark themed WinForms controls.
    internal sealed class ToggleSwitch : Control
    {
        private bool isChecked;

        public Color AccentColor { get; set; }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value)
                {
                    return;
                }

                isChecked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccentColor = Color.FromArgb(102, 125, 255);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int toggleHeight = 18;
            int toggleWidth = 32;
            int top = Math.Max(0, (Height - toggleHeight) / 2);
            Rectangle toggle = new Rectangle(0, top, toggleWidth, toggleHeight);
            Color track = Checked ? AccentColor : Color.FromArgb(69, 80, 99);

            using (GraphicsPath path = CreatePath(toggle, toggleHeight / 2))
            using (SolidBrush brush = new SolidBrush(track))
            using (SolidBrush knob = new SolidBrush(Color.FromArgb(249, 250, 251)))
            {
                e.Graphics.FillPath(brush, path);
                int knobX = Checked ? toggle.Right - 15 : toggle.X + 3;
                e.Graphics.FillEllipse(knob, knobX, toggle.Y + 3, 12, 12);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(42, 0, Width - 42, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused)
            {
                using (Pen focus = new Pen(Color.FromArgb(135, AccentColor), 1f))
                {
                    e.Graphics.DrawRectangle(focus, 0, 0, Width - 1, Height - 1);
                }
            }
        }

        private static GraphicsPath CreatePath(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class HeaderButton : Label
    {
        public Color HoverColor { get; set; }
        private readonly Color normalColor;

        public HeaderButton(string text)
        {
            Text = text;
            normalColor = Color.FromArgb(185, 195, 212);
            ForeColor = normalColor;
            Font = new Font("Segoe UI", 16f, FontStyle.Regular);
            TextAlign = ContentAlignment.MiddleCenter;
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            HoverColor = normalColor;
            MouseEnter += (sender, args) => ForeColor = HoverColor;
            MouseLeave += (sender, args) => ForeColor = normalColor;
        }
    }
}
