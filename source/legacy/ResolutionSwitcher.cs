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
            if (args.Length > 0 && args[0] == "--refresh-only-trial")
            {
                Environment.ExitCode = RefreshRateWorker.Run(args);
                return;
            }
            if (args.Length > 0 && args[0] == "--custom-resolution-trial")
            {
                Environment.ExitCode = CustomResolutionWorker.Run(args);
                return;
            }
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
                DisplayMode modeA = DisplayService.FindResolutionOnlyMode(modes, settings.ModeAWidth, settings.ModeAHeight, current);
                DisplayMode modeB = DisplayService.FindResolutionOnlyMode(modes, settings.ModeBWidth, settings.ModeBHeight, current);

                if (current == null || modeA == null || modeB == null)
                {
                    Environment.ExitCode = 11;
                    return;
                }

                DisplayMode target;
                if (modeA.HasResolution(modeB)) target = null;
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
        public int SwitchHotkey = (int)Keys.F10; public string RecentModes = string.Empty;
        public int FavoriteWidth = 0;
        public int FavoriteHeight = 0;
        public string FavoriteResolutions = string.Empty;
        public bool PreferGpuScaling = true;
        public bool RememberAfterRestart = false;
        public bool BackgroundEnabled = true;
        public bool BackgroundMotion = true;
        public bool CometsEnabled = true;
        public bool CrosshairEnabled = false;
        public string CrosshairShape = "Classic";
        public int CrosshairColor = Color.White.ToArgb();
        public string CrosshairScale = "0.6";
        public bool CrosshairOutline = true;
        public int CrosshairDesignVersion = 2;
        public int CrosshairHotkey = (int)(Keys.Control | Keys.Alt | Keys.C);

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
            return LoadFrom(FilePath);
        }
        internal static SwitcherSettings LoadFrom(string filePath)
        {
            SwitcherSettings settings = new SwitcherSettings();

            try
            {
                if (!File.Exists(filePath))
                {
                    return settings;
                }

                Dictionary<string, string> values = File.ReadAllLines(filePath)
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
                settings.SwitchHotkey = ReadInt(values, "SwitchHotkey", settings.SwitchHotkey, true); settings.RecentModes = ReadString(values, "RecentModes", settings.RecentModes);
                settings.FavoriteWidth = ReadInt(values, "FavoriteWidth", settings.FavoriteWidth);
                settings.FavoriteHeight = ReadInt(values, "FavoriteHeight", settings.FavoriteHeight);
                settings.FavoriteResolutions = ReadString(values, "FavoriteResolutions", settings.FavoriteResolutions);
                settings.PreferGpuScaling = ReadBool(values, "PreferGpuScaling", settings.PreferGpuScaling);
                settings.RememberAfterRestart = ReadBool(values, "RememberAfterRestart", settings.RememberAfterRestart);
                settings.BackgroundEnabled = ReadBool(values,"BackgroundEnabled",true);
                settings.BackgroundMotion = ReadBool(values,"BackgroundMotion",true);
                settings.CometsEnabled = ReadBool(values,"CometsEnabled",true);
                settings.CrosshairEnabled = ReadBool(values, "CrosshairEnabled", settings.CrosshairEnabled);
                settings.CrosshairShape = ReadString(values, "CrosshairShape", settings.CrosshairShape);
                settings.CrosshairColor = ReadIntAny(values, "CrosshairColor", settings.CrosshairColor);
                settings.CrosshairScale = ReadString(values, "CrosshairScale", settings.CrosshairScale);
                settings.CrosshairOutline = ReadBool(values, "CrosshairOutline", settings.CrosshairOutline);
                settings.CrosshairHotkey = ReadIntAny(values, "CrosshairHotkey", settings.CrosshairHotkey);
                settings.CrosshairDesignVersion = ReadInt(values, "CrosshairDesignVersion", 0, true);
                if (settings.CrosshairDesignVersion < 2)
                {
                    settings.CrosshairShape = "Classic"; settings.CrosshairColor = Color.White.ToArgb();
                    settings.CrosshairScale = "0.6"; settings.CrosshairDesignVersion = 2;
                }
            }
            catch
            {
                // A damaged preference file should never stop the switcher from opening.
            }

            return settings;
        }

        public void Save()
        {
            SaveTo(FilePath);
        }
        internal void SaveTo(string filePath)
        {
            try
            {
                string folder = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(folder);
                File.WriteAllLines(filePath, new[]
                {
                    "DeviceName=" + DeviceName,
                    "ModeAWidth=" + ModeAWidth,
                    "ModeAHeight=" + ModeAHeight,
                    "ModeARefresh=" + ModeARefresh,
                    "ModeBWidth=" + ModeBWidth,
                    "ModeBHeight=" + ModeBHeight,
                    "ModeBRefresh=" + ModeBRefresh,
                    "SwitchHotkey=" + SwitchHotkey,
                    "RecentModes=" + RecentModes,
                    "FavoriteWidth=" + FavoriteWidth,
                    "FavoriteHeight=" + FavoriteHeight,
                    "FavoriteResolutions=" + FavoriteResolutions,
                    "PreferGpuScaling=" + PreferGpuScaling,
                    "RememberAfterRestart=" + RememberAfterRestart
                    ,"CrosshairEnabled=" + CrosshairEnabled
                    ,"CrosshairShape=" + CrosshairShape
                    ,"CrosshairColor=" + CrosshairColor
                    ,"CrosshairScale=" + CrosshairScale
                    ,"CrosshairOutline=" + CrosshairOutline
                    ,"CrosshairDesignVersion=" + CrosshairDesignVersion
                    ,"CrosshairHotkey=" + CrosshairHotkey
                    ,"BackgroundEnabled=" + BackgroundEnabled
                    ,"BackgroundMotion=" + BackgroundMotion
                    ,"CometsEnabled=" + CometsEnabled
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

        private static int ReadIntAny(IDictionary<string, string> values, string key, int fallback)
        {
            string value; int parsed;
            return values.TryGetValue(key, out value) && int.TryParse(value, out parsed) ? parsed : fallback;
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
        public string MonitorName { get; private set; }

        public DisplayInfo(string deviceName, string friendlyName, bool isPrimary, string monitorName = null)
        {
            DeviceName = deviceName;
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? deviceName : friendlyName;
            IsPrimary = isPrimary;
            MonitorName = monitorName;
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
                var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
                string monitorName = EnumDisplayDevices(device.DeviceName, 0, ref monitor, 0) ? monitor.DeviceString : null;
                displays.Add(new DisplayInfo(device.DeviceName, device.DeviceString, primary, monitorName));
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

        public static DisplayMode FindResolutionOnlyMode(IEnumerable<DisplayMode> modes, int width, int height, DisplayMode current)
        {
            if (current == null) return null;
            return modes.FirstOrDefault(mode => mode.Width == width && mode.Height == height && mode.Frequency == current.Frequency && mode.BitsPerPixel == current.BitsPerPixel);
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

    internal sealed class MainForm : Form, ISceneInteractionHost
    {
        private readonly Color background = Color.FromArgb(20, 3, 35);
        private readonly Color card = Color.FromArgb(41, 8, 57);
        private readonly Color subtleText = Color.FromArgb(219, 166, 236);
        private readonly Color primaryText = Color.FromArgb(255, 240, 253);
        private readonly Color accent = Color.FromArgb(255, 54, 190);
        private readonly Color success = Color.FromArgb(53, 240, 255);
        private readonly Timer stateTimer; private readonly Timer sceneTimer = new Timer { Interval = 16 };
        private readonly System.Diagnostics.Stopwatch animationClock = System.Diagnostics.Stopwatch.StartNew();
        private double nextFrame; private double scenePhaseOffset; private bool movingWindow;
        private bool precisionTimer;
        private readonly HashSet<object> sceneInteractions=new HashSet<object>();
        private float interactionPhase;
        public void SetSceneInteraction(object source,bool active)
        {
            if(IsDisposed || Disposing)return;
            if(active) {if(sceneInteractions.Count==0)interactionPhase=Synthwave.Phase;sceneInteractions.Add(source);}
            else if(sceneInteractions.Remove(source) && sceneInteractions.Count==0) {double now=animationClock.Elapsed.TotalSeconds;scenePhaseOffset=interactionPhase-now;nextFrame=now+1.0/60;}
            UpdateAnimationTiming();
        }
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
        private void UpdateAnimationTiming()
        {
            bool active = Visible && WindowState != FormWindowState.Minimized && !movingWindow && sceneInteractions.Count==0 && settingsOverlay==null && Synthwave.BackgroundEnabled && Synthwave.BackgroundMotion;
            if (active) sceneTimer.Start(); else sceneTimer.Stop();
            bool precise=Visible && WindowState!=FormWindowState.Minimized && (active || sceneInteractions.Count>0);
            if(precise && !precisionTimer) precisionTimer = timeBeginPeriod(1) == 0;
            else if(!precise && precisionTimer) { timeEndPeriod(1); precisionTimer = false; }
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
        private DarkDropDown currentRefreshPicker;
        private AccentButton applyCurrentRefresh;
        private bool loadingCurrentRefresh;
        private string currentRefreshKey;
        private int observedRefresh;
        private MonitorPreview monitorPreview;
        // Kept as a nonvisual state sink so mode validation remains unchanged without a second line of UI.
        private readonly Label targetHint = new NeonLabel();
        private AccentButton switchButton;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private HotkeyWindow hotkeyWindow;
        private HotkeyWindow crosshairHotkeyWindow;
        private CrosshairOverlay crosshairOverlay;
        private Panel resolutionPage;
        private Panel crosshairPage;
        private Panel boostPage;
        private VibrancePageControl vibranceControl;
        private DarkDropDown crosshairShapePicker;
        private DecimalEntry crosshairSizeEntry;
        private AccentButton crosshairToggleButton;
        private AccentButton crosshairOutlineButton;
        private HotkeyCapture crosshairHotkeyCapture;
        private CrosshairPreview crosshairPreview;
        private Color crosshairSelectedColor = Color.White;
        private StartupOverlay startupOverlay;
        private SettingsOverlay settingsOverlay;
        private SettingsOverlay cachedSettingsOverlay;
        private readonly Dictionary<Control,bool> suspendedControls=new Dictionary<Control,bool>();
        private SettingsPageControl settingsControl;
        private float settingsScenePhase;

        private bool hotkeyRegistered;
        private bool applyingRefresh,closeAfterRefresh;
        private bool switchPreparation; private Timer pendingSwitchTimer;
        private readonly DesktopVibranceStore vibranceStore;

        public MainForm() : this(null) { }
        internal MainForm(DesktopVibranceStore vibranceStore)
        {
            this.vibranceStore=vibranceStore;
            Text = "Lucent";
            Font=new Font(Synthwave.FontFamily,9f);
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

            settings = SwitcherSettings.Load();
            BuildInterface();
            BuildPages();
            ApplyAppearance();
            sceneTimer.Tick += (sender, args) => {
                if (!Visible || WindowState == FormWindowState.Minimized || startupOverlay != null) return;
                double now = animationClock.Elapsed.TotalSeconds;
                if(now < nextFrame) return;
                nextFrame = Math.Max(nextFrame + 1.0 / 60.0, now);
                Synthwave.Phase = (float)(now + scenePhaseOffset);
                Invalidate(true);
            };
            VisibleChanged += (sender,args) => UpdateAnimationTiming();
            Resize += (sender,args) => { UpdateAnimationTiming(); if (WindowState == FormWindowState.Minimized && trayIcon != null) MinimizeToTray(); };
            UpdateAnimationTiming();
            FormClosed += (sender, args) => { sceneTimer.Dispose(); if(precisionTimer) { timeEndPeriod(1); precisionTimer=false; } };
            Load += OnLoad;

            FormClosed += OnFormClosed;
            FormClosing += (sender, args) => {
                if(applyingRefresh) {closeAfterRefresh=true;args.Cancel=true;return;}
                if (vibranceControl != null && !vibranceControl.StopAndRestore())
                {
                    if (MessageBox.Show(this, "The previous vibrance level could not be restored. Retry with Turn off in Video, or exit and keep a recovery record for the next launch. Exit anyway?", "Lucent vibrance", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        args.Cancel = true;
                }
            };
            KeyDown += OnFormKeyDown;

            stateTimer = new Timer();
            stateTimer.Interval = 1800;
            stateTimer.Tick += (sender, args) => RefreshLiveDisplay(false);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateWindowCorners();
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateWindowCorners();
        }
        private void UpdateWindowCorners()
        {
            if (!IsHandleCreated || Width < 1 || Height < 1 || WindowState == FormWindowState.Minimized) return;
            Region previous = Region;
            if (WindowState == FormWindowState.Maximized) Region = null;
            else
            {
                float scale; using (Graphics graphics = CreateGraphics()) scale = graphics.DpiX / 96f;
                int radius = Math.Max(1, (int)Math.Round(16 * scale));
                radius = Math.Min(radius, Math.Min(Width,Height)/2);
                using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(0,0,Width+1,Height+1),radius)) Region = new Region(path);
            }
            if (previous != null) previous.Dispose();
        }
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

            BrandWordmark brandMark = new BrandWordmark
            {
                Size = new Size(121, 42),
                Location = new Point(23, 12),
                BackColor = Color.Transparent
            };
            brandMark.MouseDown += BeginWindowDrag;
            header.Controls.Add(brandMark);
            GearButton gear = new GearButton { Bounds = new Rectangle(588,17,32,30) };
            gear.Click += (s,e) => OpenSettings(); header.Controls.Add(gear);

            HeaderButton minimize = new HeaderButton("—") { Location = new Point(638, 16), HoverColor = Color.FromArgb(94, 170, 255) };
            minimize.Click += (sender, args) => MinimizeToTray();
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
                Font = new Font(Synthwave.FontFamily, 24f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 38),
                BackColor = Color.Transparent
            };
            liveCard.Controls.Add(currentResolution);

            currentDetails = new RefreshGlowLabel
            {
                Text = string.Empty,
                ForeColor = subtleText,
                Font = new Font(Synthwave.FontFamily, 10f),
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

            Label modeAHeading = NewKicker("MODE A", new Point(22, 49));
            modesCard.Controls.Add(modeAHeading);

            Label modeAHertz = NewKicker("REFRESH RATE", new Point(193, 49));
            modesCard.Controls.Add(modeAHertz);

            Label modeBHeading = NewKicker("MODE B", new Point(368, 49));
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
            swapGlyph.Click += (sender, args) =>
            {
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
                Font = new Font(Synthwave.FontFamily, 12f, FontStyle.Bold),
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
                Font = new Font(Synthwave.FontFamily, 9f, FontStyle.Regular)
            });

            startupOverlay = new StartupOverlay { Bounds = ClientRectangle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = background };
            startupOverlay.Finished += (sender, args) => {
                // Continue from the last painted intro frame, not the older app clock.
                double now = animationClock.Elapsed.TotalSeconds;
                scenePhaseOffset = Synthwave.Phase - now;
                nextFrame = now + 1.0 / 60.0;
                Controls.Remove(startupOverlay); startupOverlay.Dispose(); startupOverlay = null;
                Invalidate(true);Update();
                stateTimer.Start();
            };
            Controls.Add(startupOverlay);
            startupOverlay.BringToFront();

        }

        private void OnLoad(object sender, EventArgs e)
        {
            // The window region supplies the shape. Do not add a second DWM
            // curve or an outline around its clipped corners.
            int roundedCorners = 1;
            DwmSetWindowAttribute(Handle, 33, ref roundedCorners, Marshal.SizeOf(typeof(int)));
            int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
            DwmSetWindowAttribute(Handle, 34, ref borderColor, Marshal.SizeOf(typeof(int)));

            settings = SwitcherSettings.Load();
            ApplyAppearance();
            LoadCrosshairSettings();
            PopulateDisplays();
            SetUpTrayIcon();
            hotkeyWindow = new HotkeyWindow(SwitchFromHotkey);
            hotkeyRegistered = hotkeyWindow.Register((Keys)settings.SwitchHotkey);
            crosshairHotkeyWindow = new HotkeyWindow(ToggleCrosshair);
            crosshairHotkeyWindow.Register((Keys)settings.CrosshairHotkey);
            ApplyCrosshair();
            SetStatus(settings.SwitchHotkey == 0 ? "Resolution shortcut is unbound." : hotkeyRegistered ? "Ready — " + HotkeyText + " switches from anywhere." : "Hotkey unavailable. Choose another in Settings.", success);
            // Prepare the destination and its cached surfaces before the intro
            // clock runs, so the reveal never exposes controls painting in stages.
            PrepareSettings();
            Bitmap readyFrame=new Bitmap(ClientSize.Width,ClientSize.Height);
            startupOverlay.Visible=false;
            try {Synthwave.Phase=0;DrawToBitmap(readyFrame,ClientRectangle);startupOverlay.PrepareReveal(readyFrame);}
            finally {startupOverlay.Visible=true;startupOverlay.BringToFront();}
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
                Text = "Lucent",
                Visible = true
            };
            trayIcon.DoubleClick += (sender, args) => ShowFromTray();
            RebuildTrayMenu();
        }

        private string HotkeyText { get { return SettingsPageControl.BindingText(settings.SwitchHotkey); } }
        private void ConfigureHotkey()
        {
            if(settingsOverlay==null) OpenSettings(); settingsControl.BeginBinding(false);
        }
        private bool SetBinding(bool crosshair, Keys key)
        {
            HotkeyWindow window=crosshair ? crosshairHotkeyWindow : hotkeyWindow;
            if (window==null || !window.Register(key)) return false;
            if (crosshair) {settings.CrosshairHotkey=(int)key; crosshairHotkeyCapture.Hotkey=key;}
            else {hotkeyRegistered=key!=Keys.None;settings.SwitchHotkey=(int)key;}
            settings.Save();
            if (trayIcon!=null) trayIcon.Text="Lucent";
            return true;
        }
        private void RebuildTrayMenu()
        {
            if (trayIcon == null) return;
            ContextMenuStrip previous = trayMenu;
            LucentTrayMenu menu = new LucentTrayMenu(); trayMenu = menu;
            menu.AddAction("Video", "video", () => OpenPage("Video"));
            menu.AddAction("Crosshair", "crosshair", () => OpenPage("Crosshair"));
            menu.AddAction("Boost", "boost", () => OpenPage("Boost"));
            menu.AddAction("Settings", "settings", OpenSettings);
            menu.Items.Add(new ToolStripSeparator());
            menu.AddAction("Switch resolution", "switch", SwitchFromHotkey);
            menu.Items.Add(new ToolStripSeparator());
            menu.AddAction("Close Lucent", "quit", ExitApplication);
            menu.Opening += (s,e) => {
                DisplayMode mode = primaryDisplay == null ? null : DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
                menu.SetStatus(mode == null ? "Display unavailable" : mode.Width+" × "+mode.Height+"  ·  "+mode.Frequency+" Hz");
            };
            trayIcon.ContextMenuStrip = trayMenu;
            if (previous != null) previous.Dispose();
        }
        private void OpenPage(string title)
        {
            CloseSettings(); ShowFromTray();
            MiamiTab[] tabs = Controls.OfType<Panel>().Where(p=>p.Dock==DockStyle.Top).SelectMany(p=>p.Controls.OfType<MiamiTab>()).ToArray();
            MiamiTab tab = tabs.First(t=>t.Text==title);
            ShowPage(title=="Video" ? resolutionPage : title=="Crosshair" ? crosshairPage : boostPage,tab,tabs);
        }
        private void OpenSettings()
        {
            if(settingsOverlay!=null) {CloseSettings();return;}
            ShowFromTray(); sceneTimer.Stop(); settingsScenePhase=Synthwave.Phase;
            Bitmap snapshot=new Bitmap(ClientSize.Width,ClientSize.Height);DrawToBitmap(snapshot,ClientRectangle);
            SuspendLayout();
            try
            {
                PrepareSettings();settingsOverlay=cachedSettingsOverlay;settingsOverlay.SetBackdrop(snapshot);settingsControl.RefreshBindings();
                foreach(Control control in Controls)if(control!=settingsOverlay) {suspendedControls[control]=control.Enabled;control.Enabled=false;}
                settingsOverlay.Visible=true;settingsOverlay.BringToFront();
            }
            finally {ResumeLayout(false);}
            Invalidate(true);Update();UpdateAnimationTiming();settingsControl.Focus();
        }
        private void PrepareSettings()
        {
            if(IsDisposed || Disposing || cachedSettingsOverlay!=null)return;
            cachedSettingsOverlay=new SettingsOverlay(new Bitmap(ClientSize.Width,ClientSize.Height),CloseSettings) {Bounds=ClientRectangle,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Visible=false};
            settingsControl=new SettingsPageControl(settings,()=>{settings.Save();ApplyAppearance();},SetBinding,CloseSettings) {Dock=DockStyle.Fill};
            cachedSettingsOverlay.Content.Add(settingsControl);Controls.Add(cachedSettingsOverlay);
            PrepareHandles(cachedSettingsOverlay);
            using(Bitmap image=new Bitmap(ClientSize.Width,ClientSize.Height))cachedSettingsOverlay.DrawToBitmap(image,ClientRectangle);
        }
        private static void PrepareHandles(Control control) {IntPtr handle=control.Handle;control.PerformLayout();foreach(Control child in control.Controls)PrepareHandles(child);}
        private void CloseSettings()
        {
            if(settingsOverlay==null)return;
            SettingsOverlay overlay=settingsOverlay;
            bool freezeRedraw=IsHandleCreated && Visible && !Disposing;
            if(freezeRedraw) SuspendLayout();
            try
            {
                settingsControl.CancelCapture();
                foreach(var entry in suspendedControls)if(!entry.Key.IsDisposed)entry.Key.Enabled=entry.Value;
                suspendedControls.Clear();
                double now=animationClock.Elapsed.TotalSeconds;
                scenePhaseOffset=settingsScenePhase-now;nextFrame=now+1.0/60.0;
                Synthwave.Phase=settingsScenePhase;
                overlay.Visible=false;settingsOverlay=null;
            }
            finally {if(freezeRedraw)ResumeLayout(false);}
            Invalidate(true);Update();UpdateAnimationTiming();
        }
        private void MinimizeToTray()
        {
            Hide();
            ShowInTaskbar = false;
            foreach (ShimmerLabel label in Controls.OfType<ShimmerLabel>()) label.SetAnimationPaused(true);
        }

        private void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            foreach (ShimmerLabel label in Controls.OfType<ShimmerLabel>()) label.SetAnimationPaused(false);
            Activate();
        }

        private void ExitApplication()
        {
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

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            if (pendingSwitchTimer != null) { pendingSwitchTimer.Stop(); pendingSwitchTimer.Dispose(); pendingSwitchTimer = null; }
            stateTimer.Stop();
            if (hotkeyWindow != null) hotkeyWindow.Dispose();
            if (crosshairHotkeyWindow != null) crosshairHotkeyWindow.Dispose();
            if (crosshairOverlay != null) crosshairOverlay.Dispose();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (trayMenu != null) trayMenu.Dispose();
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
            currentRefreshKey=null;
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
                if(settingsOverlay!=null)
                {
                    if(settingsControl!=null && settingsControl.IsCapturing) settingsControl.CancelCapture();
                    else CloseSettings();
                }
                e.SuppressKeyPress=true;
                return;
            }

            if(settingsOverlay!=null) return;

            if (e.KeyCode == Keys.F5)
            {
                ReloadDisplayModes();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Space && resolutionPage.Visible && switchButton.Enabled && (ActiveControl == null || ActiveControl == switchButton || ActiveControl is Panel))
            {
                OnSwitchClicked(switchButton, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void SwitchFromHotkey()
        {
            if (switchPreparation || sceneInteractions.Count>0 || (settingsOverlay!=null) || IsDisposed || Disposing) return;
            DisplayMode current = primaryDisplay == null ? null : DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode modeA = SelectedMode(modeAPicker, modeARefreshPicker, current);
            DisplayMode modeB = SelectedMode(modeBPicker, modeBRefreshPicker, current);
            DisplayMode target = ChooseTarget(current, modeA, modeB);

            if (current == null || target == null)
            {
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(1600, "Lucent", "Choose a valid toggle pair before using the switch hotkey.", ToolTipIcon.Warning);
                }
                return;
            }

            DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
            if (result.IsSuccess)
            {
                SwitchSound.Play();
                RecordRecent(target);
                SetStatus("Hotkey switched to " + target.ResolutionText + ".", success);
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
            if (m.Msg == 0x0231) SetMovingWindow(true);
            if (m.Msg == 0x0232) SetMovingWindow(false);
            if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)7721)
            {
                BeginInvoke(new Action(SwitchFromHotkey));
            }
            base.WndProc(ref m);
        }

        private void SetMovingWindow(bool moving)
        {
            if (movingWindow == moving) return;
            movingWindow = moving;
            foreach (ShimmerLabel label in Controls.OfType<ShimmerLabel>()) label.SetAnimationPaused(moving);
            if (moving)
            {
                animationClock.Stop(); sceneTimer.Stop();
                if (stateTimer != null) stateTimer.Stop();
            }
            else
            {
                animationClock.Start(); nextFrame = animationClock.Elapsed.TotalSeconds + 1.0 / 60.0;
                UpdateAnimationTiming();
                if (stateTimer != null) stateTimer.Start();
                Invalidate(true);
            }
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
            if (resolution == null)
            {
                return null;
            }

            return DisplayService.FindResolutionOnlyMode(modes, resolution.Width, resolution.Height, current);
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
            if(!forceStatus && (sceneInteractions.Count>0 || settingsOverlay!=null))return;
            if (crosshairOverlay != null && !crosshairOverlay.IsDisposed) crosshairOverlay.RefreshDisplayBounds();
            if (switchPreparation || IsDisposed || Disposing) return;
            if (primaryDisplay == null)
            {
                return;
            }

            DisplayMode current = DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            DisplayMode modeA = SelectedMode(modeAPicker, modeARefreshPicker, current);
            DisplayMode modeB = SelectedMode(modeBPicker, modeBRefreshPicker, current);

            if (current == null)
            {
                if (currentRefreshPicker != null) { currentRefreshPicker.Enabled = false; applyCurrentRefresh.Enabled = false; }
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
            UpdateCurrentRefresh(current);
            monitorPreview.SetMode(current);
            ApplyRefreshColor(modeARefreshPicker, modeA == null ? 0 : modeA.Frequency);
            ApplyRefreshColor(modeBRefreshPicker, modeB == null ? 0 : modeB.Frequency);

            DisplayMode target = ChooseTarget(current, modeA, modeB);
            if (target == null)
            {
                switchButton.Enabled = false;
                switchButton.Text = modeA == null || modeB == null ? "Choose resolutions supporting " + current.Frequency + " Hz" : "Select two different resolutions";
                targetHint.Text = "The active resolution must match Mode A or Mode B before it can toggle.";
                if (forceStatus)
                {
                    SetStatus("Choose a valid toggle pair.", Color.FromArgb(255, 193, 92));
                }
                return;
            }

            switchButton.Enabled = true;
            switchButton.Text = "Switch to " + target.ResolutionText;
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
                modeA.HasResolution(modeB))
            {
                return null;
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
            Timer launchTimer = new Timer { Interval = 430 }; pendingSwitchTimer = launchTimer;
            launchTimer.Tick += (timerSender, timerArgs) =>
            {
                launchTimer.Stop();
                launchTimer.Dispose(); pendingSwitchTimer = null;
                if (IsDisposed || Disposing) return;
                switchButton.Text = "Switching…";
                ExecuteSwitch(target, current);
            };
            launchTimer.Start();
        }

        private void ExecuteSwitch(DisplayMode target, DisplayMode current)
        {
            current = primaryDisplay == null ? null : DisplayService.GetCurrentMode(primaryDisplay.DeviceName);
            target = current == null ? null : DisplayService.FindResolutionOnlyMode(modes, target.Width, target.Height, current);
            if (target == null) { switchPreparation = false; RefreshLiveDisplay(true); return; }
            DisplayChangeResult result = DisplayService.Apply(target, current, true, false);
            if (result.IsSuccess)
            {
                SwitchSound.Play();
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
            }
            if (modeB != null)
            {
                settings.ModeBWidth = modeB.Width;
                settings.ModeBHeight = modeB.Height;
            }

            settings.Save();
        }

        private void BuildPages()
        {
            Panel header = Controls.OfType<Panel>().First(panel => panel.Dock == DockStyle.Top);
            resolutionPage = new Panel { Location = new Point(0, 66), Size = new Size(720, 400), BackColor = Color.Transparent };
            List<Control> pageControls = Controls.Cast<Control>().Where(control => control != header && control != startupOverlay && control.Top >= 66).ToList();
            foreach (Control control in pageControls) { Controls.Remove(control); control.Top -= 66; resolutionPage.Controls.Add(control); }
            Controls.Add(resolutionPage);
            resolutionPage.SendToBack();

            crosshairPage = new Panel { Location = new Point(0, 66), Size = new Size(720, 400), BackColor = Color.Transparent, Visible = false };
            Controls.Add(crosshairPage);
            BuildCrosshairPage();

            boostPage = new Panel { Location = new Point(0, 66), Size = new Size(720, 400), BackColor = Color.Transparent, Visible = false };
            boostPage.Controls.Add(new BoostPageControl { Location = new Point(24, 18), Size = new Size(672, 345) });
            Controls.Add(boostPage);

            CompactVideoPage();
            vibranceControl = vibranceStore==null ? new VibrancePageControl() : new VibrancePageControl(vibranceStore);
            vibranceControl.Location=new Point(24,278);vibranceControl.Size=new Size(672,90);
            resolutionPage.Controls.Add(vibranceControl);

            MiamiTab resolutionTab = new MiamiTab("Video") { Selected = true };
            MiamiTab crosshairTab = new MiamiTab("Crosshair");
            MiamiTab boostTab = new MiamiTab("Boost");
            MiamiTab[] tabs = { resolutionTab, crosshairTab, boostTab };
            resolutionTab.Click += (sender, args) => ShowPage(resolutionPage, resolutionTab, tabs);
            crosshairTab.Click += (sender, args) => ShowPage(crosshairPage, crosshairTab, tabs);
            boostTab.Click += (sender, args) => ShowPage(boostPage, boostTab, tabs);
            header.Controls.Add(resolutionTab); header.Controls.Add(crosshairTab); header.Controls.Add(boostTab);
            Action centerTabs = () => {
                int gap = 8, total = tabs.Sum(tab => tab.Width) + gap * (tabs.Length - 1);
                int left = (header.ClientSize.Width - total) / 2;
                foreach (MiamiTab tab in tabs) { tab.Left = left; tab.Top = 13; left += tab.Width + gap; }
            };
            header.Resize += (sender, args) => centerTabs();
            centerTabs();
            if (startupOverlay != null) { startupOverlay.Bounds = ClientRectangle; startupOverlay.BringToFront(); }
        }

        private void ShowPage(Panel selectedPage, MiamiTab selectedTab, params MiamiTab[] tabs)
        {
            resolutionPage.Visible = selectedPage == resolutionPage;
            crosshairPage.Visible = selectedPage == crosshairPage;
            boostPage.Visible = selectedPage == boostPage;
            foreach (MiamiTab tab in tabs) { tab.Selected = tab == selectedTab; tab.Invalidate(); }
        }

        private void CompactVideoPage()
        {
            RoundedPanel live = resolutionPage.Controls.OfType<RoundedPanel>().OrderBy(panel => panel.Top).First();
            RoundedPanel pair = resolutionPage.Controls.OfType<RoundedPanel>().OrderBy(panel => panel.Top).Last();
            live.Bounds = new Rectangle(24, 12, 672, 74);
            foreach (Label label in live.Controls.OfType<Label>())
                if (label != currentResolution && label != currentDetails) label.Top = 8;
            currentResolution.Top = 27; currentDetails.Visible = false;
            currentRefreshPicker = NewDropDown(new Size(88, 38)); currentRefreshPicker.Location = new Point(280, 29); currentRefreshPicker.Font = new Font(Synthwave.FontFamily, 9f);
            applyCurrentRefresh = new AccentButton { Text = "Apply", SimpleStyle = true, Bounds = new Rectangle(374, 29, 66, 38), AccentColor = Color.FromArgb(65, 43, 88), Font = new Font(Synthwave.FontFamily, 8.5f, FontStyle.Bold), Enabled = false };
            currentRefreshPicker.SelectedIndexChanged += (s, e) => {
                if (loadingCurrentRefresh) return;
                RefreshChoice choice = currentRefreshPicker.SelectedItem as RefreshChoice;
                applyCurrentRefresh.Enabled = choice != null && choice.Hertz != observedRefresh;
            };
            applyCurrentRefresh.Click += (s, e) => ApplyCurrentRefreshOnly();
            live.Controls.Add(currentRefreshPicker); live.Controls.Add(applyCurrentRefresh);
            AccentButton custom = new AccentButton { Text = "Custom resolutions…", SimpleStyle = true, Bounds = new Rectangle(460, 29, 190, 38), AccentColor = Color.FromArgb(65, 43, 88), Font = new Font(Synthwave.FontFamily, 9, FontStyle.Bold) };
            custom.Click += (s, e) => {
                if(switchPreparation)return;
                if (switchPreparation) return;
                bool wasMoving = movingWindow; SetMovingWindow(true);
                // Do not let a global resolution hotkey race the custom trial.
                switchPreparation = true;
                try { using (CustomResolutionDialog dialog = new CustomResolutionDialog()) dialog.ShowDialog(this); }
                finally { switchPreparation = false; if (!IsDisposed) { ReloadDisplayModes(); SetMovingWindow(wasMoving); } }
            };
            live.Controls.Add(custom);
            pair.Bounds = new Rectangle(24, 98, 672, 106);
            foreach (Label label in pair.Controls.OfType<Label>()) label.Top = label.Top < 30 ? 8 : 32;
            foreach (Label label in pair.Controls.OfType<Label>().Where(label => label.Text == "REFRESH RATE")) label.Visible = false;
            modeARefreshPicker.Visible = modeBRefreshPicker.Visible = false;
            modeAPicker.Bounds = new Rectangle(22, 52, 278, 36);
            modeBPicker.Bounds = new Rectangle(370, 52, 278, 36);
            foreach (SwapControl swap in pair.Controls.OfType<SwapControl>()) swap.Top = 47;
            switchButton.Bounds = new Rectangle(24, 216, 672, 46);
            foreach (ShimmerLabel footer in resolutionPage.Controls.OfType<ShimmerLabel>()) footer.Top = 372;
        }
        private void UpdateCurrentRefresh(DisplayMode current)
        {
            if (currentRefreshPicker == null || current == null) return;
            int[] rates = modes.Where(mode => mode.Width == current.Width && mode.Height == current.Height && mode.BitsPerPixel == current.BitsPerPixel && mode.Frequency > 1).Select(mode => mode.Frequency).Distinct().OrderByDescending(rate => rate).ToArray();
            string key = current.DeviceName + ":" + current.Width + "x" + current.Height + ":" + string.Join(",", rates);
            if (key != currentRefreshKey || observedRefresh != current.Frequency)
            {
                loadingCurrentRefresh = true;
                currentRefreshPicker.SetItems(rates.Select(rate => new RefreshChoice(rate)).ToArray());
                currentRefreshPicker.SelectedItem = currentRefreshPicker.Items.OfType<RefreshChoice>().FirstOrDefault(choice => choice.Hertz == current.Frequency);
                currentRefreshKey = key; observedRefresh = current.Frequency; loadingCurrentRefresh = false;
            }
            RefreshChoice selected = currentRefreshPicker.SelectedItem as RefreshChoice;
            currentRefreshPicker.Enabled = rates.Length > 0;
            applyCurrentRefresh.Enabled = selected != null && selected.Hertz != current.Frequency && !switchPreparation;
        }
        private void ApplyCurrentRefreshOnly()
        {
            if (switchPreparation || primaryDisplay == null) return;
            RefreshChoice choice = currentRefreshPicker.SelectedItem as RefreshChoice;
            if (choice == null) return;
            string device=primaryDisplay.DeviceName;int hertz=choice.Hertz;
            bool wasMoving=movingWindow;SetMovingWindow(true);switchPreparation=true;applyingRefresh=true;
            currentRefreshPicker.Enabled=false;applyCurrentRefresh.Enabled=false;applyCurrentRefresh.Text="Applying…";switchButton.Enabled=false;
            System.Threading.Tasks.Task.Factory.StartNew(()=>RefreshRateTransaction.ApplySupported(device,hertz)).ContinueWith(task=>{
                RefreshRateOutcome outcome=task.IsFaulted?new RefreshRateOutcome {Error=task.Exception.GetBaseException().Message}:task.Result;
                if(IsDisposed || Disposing)return;
                BeginInvoke(new Action(()=>{
                    applyingRefresh=false;switchPreparation=false;currentRefreshKey=null;applyCurrentRefresh.Text="Apply";
                    RefreshLiveDisplay(true);SetMovingWindow(wasMoving);
                    if(outcome.Kept)RefreshSound.Play();
                    else MessageBox.Show(this,outcome.Error,"Lucent · Refresh rate",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    if(closeAfterRefresh)Close();
                }));
            });
        }

        private void BuildCrosshairPage()
        {
            Color miamiPink = Color.FromArgb(255, 79, 203), miamiBlue = Color.FromArgb(50, 224, 255);
            RoundedPanel previewCard = NewCard(new Rectangle(24, 18, 260, 270));
            previewCard.BorderColor = Color.Transparent;
            crosshairPage.Controls.Add(previewCard);
            previewCard.Controls.Add(NewKicker("LIVE PREVIEW", new Point(20, 16)));
            crosshairPreview = new CrosshairPreview { Location = new Point(20, 48), Size = new Size(220, 190), BackColor = Color.Transparent };
            previewCard.Controls.Add(crosshairPreview);

            RoundedPanel settingsCard = NewCard(new Rectangle(304, 18, 392, 270));
            settingsCard.BorderColor = Color.Transparent;
            crosshairPage.Controls.Add(settingsCard);
            settingsCard.Controls.Add(NewKicker("CROSSHAIR STYLE", new Point(20, 16)));
            settingsCard.Controls.Add(NewKicker("SHAPE", new Point(20, 50)));
            crosshairShapePicker = NewDropDown(new Size(164, 36)); crosshairShapePicker.Location = new Point(20, 70);
            crosshairShapePicker.SetItems(new object[] { "Classic", "Dot", "Square", "positted" });
            settingsCard.Controls.Add(crosshairShapePicker);
            settingsCard.Controls.Add(NewKicker("SIZE", new Point(210, 50)));
            crosshairSizeEntry = new DecimalEntry { Location = new Point(202, 69), Size = new Size(165, 37), Value = 0.6f };
            settingsCard.Controls.Add(crosshairSizeEntry);
            settingsCard.Controls.Add(NewKicker("COLOR", new Point(20, 124)));
            AccentButton colorButton = new AccentButton { Text = "Choose color (RGB)", SimpleStyle = true, Location = new Point(20, 147), Size = new Size(225, 34), AccentColor = Color.FromArgb(71, 32, 91), Font = new Font(Synthwave.FontFamily, 9f, FontStyle.Bold) };
            colorButton.Click += (sender, args) => {
                bool wasMoving = movingWindow;
                SetMovingWindow(true);
                try {
                using (ColorDialog picker = new ColorDialog { Color = crosshairSelectedColor, FullOpen = true, AnyColor = true }) {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    crosshairSelectedColor = picker.Color;
                    crosshairPreview.CrosshairColor = picker.Color;
                    SaveCrosshairSettings(); ApplyCrosshair();
                }
                }
                finally { if (!IsDisposed && !Disposing) SetMovingWindow(wasMoving); }
            };
            settingsCard.Controls.Add(colorButton);
            crosshairOutlineButton = new AccentButton { SimpleStyle = true, Location = new Point(260, 147), Size = new Size(107, 34), AccentColor = Color.FromArgb(91, 50, 138), Font = new Font(Synthwave.FontFamily, 8.5f, FontStyle.Bold) };
            settingsCard.Controls.Add(crosshairOutlineButton);
            crosshairHotkeyCapture = new HotkeyCapture { Location = new Point(20, 201), Size = new Size(347, 40), AccentColor = miamiBlue };
            settingsCard.Controls.Add(crosshairHotkeyCapture);

            crosshairToggleButton = new AccentButton { SimpleStyle = true, Text = "Turn crosshair on", Location = new Point(24, 307), Size = new Size(672, 56), AccentColor = miamiPink, Font = new Font(Synthwave.FontFamily, 12f, FontStyle.Bold) };
            crosshairPage.Controls.Add(crosshairToggleButton);

            crosshairShapePicker.SelectedIndexChanged += (sender, args) => { crosshairPreview.Shape = Convert.ToString(crosshairShapePicker.SelectedItem); SaveCrosshairSettings(); ApplyCrosshair(); };
            crosshairSizeEntry.ValueChanged += (sender, args) => { crosshairPreview.DisplayScale = crosshairSizeEntry.Value; SaveCrosshairSettings(); ApplyCrosshair(); };
            crosshairToggleButton.Click += (sender, args) => ToggleCrosshair();
            crosshairOutlineButton.Click += (sender, args) => { settings.CrosshairOutline = !settings.CrosshairOutline; UpdateOutlineButton(); SaveCrosshairSettings(); ApplyCrosshair(); };
            crosshairHotkeyCapture.HotkeyChanged += (sender, args) => SaveCrosshairHotkey();
        }

        private void LoadCrosshairSettings()
        {
            loading = true;
            crosshairShapePicker.SelectedItem = crosshairShapePicker.Items.FirstOrDefault(item => Convert.ToString(item) == settings.CrosshairShape) ?? "Classic";
            float scale; if (!float.TryParse(settings.CrosshairScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale)) scale = 0.6f;
            crosshairSizeEntry.Value = Math.Max(0.1f, Math.Min(20f, scale));
            crosshairSelectedColor = Color.FromArgb(settings.CrosshairColor);
            crosshairPreview.CrosshairColor = crosshairSelectedColor;
            settings.CrosshairShape = Convert.ToString(crosshairShapePicker.SelectedItem);
            crosshairPreview.Shape = settings.CrosshairShape;
            crosshairPreview.DisplayScale = crosshairSizeEntry.Value;
            crosshairPreview.Outline = settings.CrosshairOutline;
            UpdateColorSwatches();
            UpdateOutlineButton();
            crosshairHotkeyCapture.Hotkey = (Keys)settings.CrosshairHotkey;
            UpdateCrosshairButton();
            loading = false;
        }

        private void SaveCrosshairSettings()
        {
            if (loading || settings == null) return;
            settings.CrosshairShape = Convert.ToString(crosshairShapePicker.SelectedItem) ?? "Classic";
            settings.CrosshairScale = crosshairSizeEntry.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            settings.CrosshairColor = crosshairSelectedColor.ToArgb();
            crosshairPreview.Outline = settings.CrosshairOutline;
            settings.Save();
        }

        private void ApplyCrosshair()
        {
            if (settings == null) return;
            if (crosshairOverlay == null || crosshairOverlay.IsDisposed) crosshairOverlay = new CrosshairOverlay();
            float scale; if (!float.TryParse(settings.CrosshairScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale)) scale = 0.6f;
            crosshairOverlay.Configure(settings.CrosshairShape, Color.FromArgb(settings.CrosshairColor), scale, settings.CrosshairOutline);
            if (settings.CrosshairEnabled) crosshairOverlay.Show(); else crosshairOverlay.Hide();
        }

        private void ToggleCrosshair()
        {
            if (settings == null || (settingsOverlay!=null)) return;
            settings.CrosshairEnabled = !settings.CrosshairEnabled; settings.Save();
            UpdateCrosshairButton();
            ApplyCrosshair();
        }

        private void UpdateOutlineButton()
        {
            if (crosshairOutlineButton == null || settings == null) return;
            crosshairOutlineButton.Text = settings.CrosshairOutline ? "Outline: ON" : "Outline: OFF";
            crosshairOutlineButton.AccentColor = settings.CrosshairOutline ? Color.FromArgb(114, 42, 142) : Color.FromArgb(55, 35, 76);
            if (crosshairPreview != null) crosshairPreview.Outline = settings.CrosshairOutline;
            crosshairOutlineButton.Invalidate();
        }

        private void UpdateColorSwatches()
        {
            if (crosshairPage == null) return;
            foreach (NeonColorSwatch swatch in crosshairPage.Controls.OfType<RoundedPanel>().SelectMany(panel => panel.Controls.OfType<NeonColorSwatch>()))
                swatch.Selected = swatch.SwatchColor.ToArgb() == crosshairSelectedColor.ToArgb();
        }

        private void UpdateCrosshairButton()
        {
            if (crosshairToggleButton != null) { bool on = settings != null && settings.CrosshairEnabled; crosshairToggleButton.Text = on ? "Crosshair ON  •  Click to turn off" : "Crosshair OFF  •  Click to turn on"; crosshairToggleButton.AccentColor = on ? Color.FromArgb(24, 104, 105) : Color.FromArgb(61, 43, 73); crosshairToggleButton.Invalidate(); }
        }

        private void SaveCrosshairHotkey()
        {
            if (loading || settings == null) return;
            Keys key = crosshairHotkeyCapture.Hotkey;
            if (!crosshairHotkeyWindow.Register(key)) { crosshairHotkeyCapture.ShowError(); crosshairHotkeyCapture.Hotkey = (Keys)settings.CrosshairHotkey; return; }
            settings.CrosshairHotkey = (int)key; settings.Save();
        }

        private void SetStatus(string message, Color color)
        {
            // Status is intentionally kept out of the compact window. Relevant
            // messages are surfaced through the tray when the app is hidden.
        }
        private void ApplyAppearance()
        {
            Synthwave.ConfigureBackground(settings.BackgroundEnabled,settings.BackgroundMotion,settings.CometsEnabled);
            BackColor = Color.Black;
            if (IsHandleCreated) { int border = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref border, 4); }
            UpdateAnimationTiming(); Invalidate(true);
        }

        private RoundedPanel NewCard(Rectangle bounds)
        {
            return new RoundedPanel
            {
                BackColor = Color.Transparent,
                BorderColor = Color.Transparent,
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
                Font = new Font(Synthwave.FontFamily, 9f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(TextRenderer.MeasureText(text, new Font(Synthwave.FontFamily, 9f, FontStyle.Bold)).Width + 12, 18),
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
                Font = new Font(Synthwave.FontFamily, 10f)
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
            using (SolidBrush fill = Synthwave.Brush(Color.FromArgb(49, 10, 68)))
            using (Pen border = Synthwave.Pen(glow, 1.6f))
            using (Pen stand = Synthwave.Pen(Color.FromArgb(151, 165, 194), 2f))
            {
                if (pulse > 0)
                {
                    using (Pen ring = Synthwave.Pen(Color.FromArgb((int)(pulse * 100), glow), 2f))
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
        internal const float Duration=1.4f;
        private readonly Timer timer;
        private readonly System.Diagnostics.Stopwatch bootClock = new System.Diagnostics.Stopwatch();
        private Bitmap logo,scene,halo,caption,features;
        private Bitmap reveal;
        private bool completed,waitingForFirstPaint;
        public event EventHandler Finished;
        public StartupOverlay()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer = new Timer { Interval = 16 };
            timer.Tick += (sender, args) => Invalidate();
        }
        public void PrepareReveal(Bitmap readyFrame)
        {
            if(reveal!=null)reveal.Dispose();reveal=readyFrame;
            using(Bitmap warm=new Bitmap(Math.Max(1,Width),Math.Max(1,Height)))
            using(Graphics graphics=Graphics.FromImage(warm))Render(graphics,0);
        }
        public void Start() { completed=false;waitingForFirstPaint=true;bootClock.Reset();timer.Stop();Invalidate(); }
        private static float Ease(float value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(waitingForFirstPaint) {waitingForFirstPaint=false;bootClock.Restart();timer.Start();}
            float seconds=(float)bootClock.Elapsed.TotalSeconds;
            Render(e.Graphics,Math.Min(Duration,seconds));
            if(seconds>=Duration && !completed)
            {
                completed=true;timer.Stop();
                BeginInvoke(new Action(()=>{if(IsDisposed)return;EventHandler handler=Finished;if(handler!=null)handler(this,EventArgs.Empty);}));
            }
        }
        internal void Render(Graphics g, float seconds)
        {
            PrepareAssets();
            float enter=Ease(seconds/.42f),exit=Ease((seconds-.88f)/.28f),opacity=enter*(1-exit);
            float appReveal=Ease((seconds-.99f)/.41f);
            g.DrawImageUnscaled(scene,0,0);
            if(reveal!=null)DrawLayer(g,reveal,ClientRectangle,appReveal);
            GraphicsState state=g.Save();g.ScaleTransform(Width/720f,Height/466f);
            g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            float rise=(1-enter)*12,stretch=.94f+.06f*enter;
            RectangleF hero=new RectangleF(360-126*stretch,166+rise,252*stretch,102*stretch);
            DrawLayer(g,halo,new RectangleF(120,85,480,240),opacity);
            // Fine corner marks frame the logo without giving it a button border.
            float edge=Ease((seconds-.08f)/.38f)*(1-exit);
            using(Pen pen=new Pen(Color.FromArgb((int)(44*edge),Color.White),.8f))
            {
                float left=181-(1-enter)*14,right=539+(1-enter)*14,top=150+rise,bottom=283+rise;
                g.DrawLines(pen,new[]{new PointF(left,top+17),new PointF(left,top),new PointF(left+17,top)});
                g.DrawLines(pen,new[]{new PointF(right-17,top),new PointF(right,top),new PointF(right,top+17)});
                g.DrawLines(pen,new[]{new PointF(left,bottom-17),new PointF(left,bottom),new PointF(left+17,bottom)});
                g.DrawLines(pen,new[]{new PointF(right-17,bottom),new PointF(right,bottom),new PointF(right,bottom-17)});
            }
            DrawLayer(g,logo,hero,opacity*.83f);
            if(seconds>.3f && seconds<1.02f)
            {
                float sweep=Ease((seconds-.3f)/.72f),x=hero.Left-70+sweep*(hero.Width+140);
                GraphicsState shine=g.Save();
                using(GraphicsPath stripe=new GraphicsPath())
                {
                    stripe.AddPolygon(new[]{new PointF(x,hero.Top-4),new PointF(x+42,hero.Top-4),new PointF(x+4,hero.Bottom+4),new PointF(x-38,hero.Bottom+4)});
                    g.SetClip(stripe,CombineMode.Intersect);DrawLayer(g,logo,hero,opacity);
                }
                g.Restore(shine);
            }
            float details=Ease((seconds-.15f)/.4f)*(1-exit);
            DrawLayer(g,caption,new RectangleF(160,307+(1-enter)*8,400,24),details);
            DrawLayer(g,features,new RectangleF(160,399,400,22),details*.6f);
            float line=Ease((seconds-.2f)/.5f)*72;
            if(line>0 && opacity>0)
            {
                using(Pen baseLine=new Pen(Color.FromArgb((int)(35*opacity),Color.White),1))g.DrawLine(baseLine,360-line,349,360+line,349);
                float pulse=Ease((seconds-.35f)/.6f),x=288+pulse*144;
                using(LinearGradientBrush brush=new LinearGradientBrush(new PointF(x-18,349),new PointF(x+18,349),Color.Transparent,Color.FromArgb((int)(165*opacity),Color.White)))
                using(Pen light=new Pen(brush,1.2f))g.DrawLine(light,x-18,349,x,349);
            }
            g.Restore(state);Synthwave.Phase=0;
        }
        private void PrepareAssets()
        {
            if(scene==null || scene.Size!=ClientSize)
            {
                if(scene!=null)scene.Dispose();scene=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));
                using(Graphics canvas=Graphics.FromImage(scene))
                {Synthwave.Draw(canvas,ClientRectangle,0);using(SolidBrush shade=new SolidBrush(Color.FromArgb(205,Color.Black)))canvas.FillRectangle(shade,ClientRectangle);}
            }
            if(logo!=null)return;
            logo=new Bitmap(1008,408,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(Graphics canvas=Graphics.FromImage(logo))Brand.DrawWordmark(canvas,new RectangleF(0,0,1008,408));
            halo=new Bitmap(480,240,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(Graphics canvas=Graphics.FromImage(halo))
            using(GraphicsPath path=new GraphicsPath())
            {
                path.AddEllipse(0,0,480,240);
                using(PathGradientBrush glow=new PathGradientBrush(path)) {glow.CenterColor=Color.FromArgb(29,Color.White);glow.SurroundColors=new[]{Color.Transparent};canvas.FillPath(glow,path);}
            }
            caption=Caption("F I N D   Y O U R   F R E Q U E N C Y",8.2f,Color.FromArgb(174,174,174));
            features=Caption("V I D E O     /     C R O S S H A I R     /     B O O S T",7f,Color.FromArgb(145,145,145));
        }
        private static Bitmap Caption(string text,float size,Color color)
        {
            using(Bitmap large=new Bitmap(1600,96))
            {
                using(Graphics canvas=Graphics.FromImage(large))
                using(Font font=new Font(Synthwave.FontFamily,size))
                using(SolidBrush ink=new SolidBrush(color))
                using(StringFormat center=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
                {canvas.ScaleTransform(4,4);canvas.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;canvas.DrawString(text,font,ink,new RectangleF(0,0,400,24),center);}
                return Synthwave.Downsample(large,400,24);
            }
        }
        private static void DrawLayer(Graphics graphics,Bitmap layer,RectangleF bounds,float opacity)
        {
            if(opacity<=0)return;
            using(var attributes=new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix33=Math.Min(1,opacity);attributes.SetColorMatrix(matrix);
                graphics.DrawImage(layer,new[]{new PointF(bounds.Left,bounds.Top),new PointF(bounds.Right,bounds.Top),new PointF(bounds.Left,bounds.Bottom)},new RectangleF(0,0,layer.Width,layer.Height),GraphicsUnit.Pixel,attributes);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) {timer.Dispose();foreach(Bitmap image in new[]{logo,reveal,scene,halo,caption,features})if(image!=null)image.Dispose();}
            base.Dispose(disposing);
        }
    }

    internal sealed class ShimmerLabel : Control
    {
        private readonly Timer shimmerTimer;
        private int offset; private readonly Dictionary<string, Bitmap> footerFrames = new Dictionary<string, Bitmap>();

        public void SetAnimationPaused(bool paused) { if (paused) shimmerTimer.Stop(); else shimmerTimer.Start(); }
        public ShimmerLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            shimmerTimer = new Timer { Interval = 16 };
            shimmerTimer.Tick += (sender, args) => { offset = (offset + 7) % (Width + 120); };
            shimmerTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if(string.IsNullOrEmpty(Text)||Width<=0||Height<=0)return;
            if (Synthwave.DarkMode) { Synthwave.Text(e.Graphics, Text, Font, ClientRectangle, Color.FromArgb(165,165,165), true); return; }
            SizeF size=e.Graphics.MeasureString(Text,Font);
            int w=(int)Math.Ceiling(size.Width)+8,h=Math.Max(Height,(int)Math.Ceiling(size.Height)+4);
            int frameIndex=(offset%(w+100))*24/(w+100);
            string cacheKey=Text+Font.ToString()+w+":"+h+":"+e.Graphics.DpiY+":"+frameIndex;
            Bitmap cached; if(footerFrames.TryGetValue(cacheKey,out cached)) { e.Graphics.DrawImageUnscaled(cached,(Width-w)/2,0); return; }
            if(footerFrames.Count>=48) {foreach(Bitmap old in footerFrames.Values)old.Dispose();footerFrames.Clear();}
            using(Bitmap large=new Bitmap(w*3,h*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                large.SetResolution(e.Graphics.DpiX,e.Graphics.DpiY);
                using(Graphics canvas=Graphics.FromImage(large))
                {
                    canvas.ScaleTransform(3,3);
                    canvas.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using(LinearGradientBrush fill=Synthwave.Gradient(new Rectangle(0,0,w,h),Color.FromArgb(184,164,204),Color.FromArgb(184,164,204),0f))
                    {
                        float center=(frameIndex*(w+100)/24f-50f)/w;
                        float left=Math.Max(0,Math.Min(1,center-.16f)),middle=Math.Max(0,Math.Min(1,center)),right=Math.Max(0,Math.Min(1,center+.16f));
                        fill.InterpolationColors=new ColorBlend {Colors=new[]{Color.FromArgb(184,164,204),Color.FromArgb(184,164,204),Color.FromArgb(246,232,255),Color.FromArgb(184,164,204),Color.FromArgb(184,164,204)},Positions=new[]{0f,left,middle,right,1f}};
                        canvas.DrawString(Text,Font,fill,4,0);
                    }
                }
                Bitmap smooth=Synthwave.Downsample(large,w,h); footerFrames.Add(cacheKey,smooth); e.Graphics.DrawImageUnscaled(smooth,(Width-w)/2,0);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) {shimmerTimer.Dispose();foreach(Bitmap b in footerFrames.Values)b.Dispose();footerFrames.Clear();}
            base.Dispose(disposing);
        }
    }

    // Owns a message-only handle. Unlike the form handle, it remains valid while the app is hidden in the tray.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private int HotkeyId = 7721; private Keys registeredKey;
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
            if (key == Keys.None) { if (registered) UnregisterHotKey(Handle,HotkeyId); registered=false; registeredKey=Keys.None; return true; }
            if(registered && key==registeredKey) return true;
            uint modifiers=0x4000;
            if((key & Keys.Control)!=0) modifiers|=2;
            if((key & Keys.Alt)!=0) modifiers|=1;
            if((key & Keys.Shift)!=0) modifiers|=4;
            int candidate=HotkeyId==7721?7722:7721;
            if(!RegisterHotKey(Handle,candidate,modifiers,(uint)(key & Keys.KeyCode))) return false;
            if(registered) UnregisterHotKey(Handle,HotkeyId);
            HotkeyId=candidate; registered=true; registeredKey=key; return true;
        }

        protected override void WndProc(ref Message message)
        {
            if (registered && message.Msg == WmHotkey && message.WParam == (IntPtr)HotkeyId && pressed != null)
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
        private ModePickerPopup popupMenu;
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
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using(GraphicsPath path=RoundedPanel.RoundedPath(new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3)),8))
            using(SolidBrush fill=new SolidBrush(Color.FromArgb(Focused?31:24,Focused?31:24,Focused?31:24)))
            using(Pen edge=new Pen(Focused?Color.FromArgb(78,78,78):Color.FromArgb(41,41,41),1))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(edge,path);}
            Synthwave.Text(e.Graphics, selectedItem==null ? "Choose a mode" : Convert.ToString(selectedItem), Font, new RectangleF(10,0,Width-35,Height),ForeColor,false);
            int direction=popupMenu==null?1:-1;
            using(Pen chevron=new Pen(Color.FromArgb(185,185,185),1.5f)) e.Graphics.DrawLines(chevron,new[]{new Point(Width-21,Height/2-2*direction),new Point(Width-16,Height/2+3*direction),new Point(Width-11,Height/2-2*direction)});
        }
        private void ShowOptions()
        {
            if(items.Count==0)return;
            if(popupMenu!=null){popupMenu.Close();return;}
            ISceneInteractionHost host=FindForm() as ISceneInteractionHost;if(host!=null)host.SetSceneInteraction(this,true);
            ModePickerPopup popup=new ModePickerPopup();popupMenu=popup;Invalidate();
            int width=Math.Max(Width,120),height=Math.Min(374,items.Count*36+14);
            ModePickerList list=new ModePickerList(items.OrderByDescending(item=>IsFavorite!=null&&IsFavorite(item)).ThenBy(item=>items.IndexOf(item)).ToArray(),selectedItem,IsFavorite,ToggleFavorite,item=>SelectedItem=item,()=>{if(!popup.IsDisposed && popup.Visible)popup.Close();});
            list.Size=new Size(width,height);ToolStripControlHost content=new ToolStripControlHost(list) {AutoSize=false,Margin=Padding.Empty,Padding=Padding.Empty,Size=list.Size};
            popup.Items.Add(content);popup.Size=list.Size;
            Point point=PointToScreen(new Point(0,Height+5));Rectangle area=Screen.FromControl(this).WorkingArea;
            if(point.Y+height>area.Bottom)point.Y=PointToScreen(Point.Empty).Y-height-5;
            point.X=Math.Max(area.Left,Math.Min(area.Right-width,point.X));point.Y=Math.Max(area.Top,point.Y);
            popup.Closed+=(s,e)=>{popupMenu=null;if(host!=null)host.SetSceneInteraction(this,false);Invalidate();try{BeginInvoke(new Action(()=>popup.Dispose()));}catch(InvalidOperationException){}};
            try {popup.Show(point);list.RevealSelection();list.Focus();}
            catch {popupMenu=null;popup.Dispose();if(host!=null)host.SetSceneInteraction(this,false);throw;}
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
        private bool hovering;

        public Color AccentColor { get; set; }

        public SwapControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            AccentColor = Color.FromArgb(102, 125, 255);
            TabStop = true;
            AccessibleName = "Swap Mode A and Mode B";
            AccessibleRole = AccessibleRole.PushButton;
            MouseEnter += (s, e) => { hovering = true; Invalidate(); };
            MouseLeave += (s, e) => { hovering = false; Invalidate(); };
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
            targetAngle += 180f;
            velocity = Math.Min(50f, velocity + 7f);
            spinTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Synthwave.Background(e.Graphics, this); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            e.Graphics.TranslateTransform(Width / 2f, Height / 2f);
            e.Graphics.RotateTransform(angle);
            using (GraphicsPath pill = RoundedPanel.RoundedPath(new Rectangle(-19, -18, 38, 36), 10))
            using (SolidBrush fill = Synthwave.Brush(Synthwave.Surface(Color.FromArgb(hovering ? 210 : 150, 87, 31, 110), hovering ? 32 : 22)))
            using (Pen border = Synthwave.Pen(Color.FromArgb(hovering || Focused ? 190 : 70, 156, 115, 202), 1f))
            {
                e.Graphics.FillPath(fill, pill); e.Graphics.DrawPath(border, pill);
            }
            using (Pen top = Synthwave.Pen(Color.FromArgb(255, 112, 211), 2f))
            using (Pen bottom = Synthwave.Pen(Color.FromArgb(255, 112, 211), 2f))
            {
                top.StartCap = top.EndCap = bottom.StartCap = bottom.EndCap = LineCap.Round;
                top.LineJoin = bottom.LineJoin = LineJoin.Round;
                e.Graphics.DrawLine(top, -10, -6, 10, -6);
                e.Graphics.DrawLines(top, new[] { new Point(5, -11), new Point(10, -6), new Point(5, -1) });
                e.Graphics.DrawLine(bottom, 10, 6, -10, 6);
                e.Graphics.DrawLines(bottom, new[] { new Point(-5, 1), new Point(-10, 6), new Point(-5, 11) });
            }
            e.Graphics.ResetTransform();
        }
        protected override void Dispose(bool disposing) { if (disposing) spinTimer.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class RoundedPanel : Panel, IThemeSurface
    {
        private Bitmap surface;
        private string surfaceKey;
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
            PaintSurface(e.Graphics);
            base.OnPaint(e);
        }

        public void PaintSurface(Graphics graphics)
        {
            if(Width<4 || Height<4)return;
            string key=Width+"|"+Height+"|"+CornerRadius+"|"+BorderColor.ToArgb();
            if(surface==null || key!=surfaceKey)
            {
            if(surface!=null)surface.Dispose();surface=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);surfaceKey=key;
            using(Graphics canvas=Graphics.FromImage(surface))
            {
            var e = new PaintEventArgs(canvas, ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = RoundedPath(bounds, CornerRadius))
            using (LinearGradientBrush fill = Synthwave.Gradient(ClientRectangle, Synthwave.Surface(Color.FromArgb(170, 46, 10, 65),13), Synthwave.Surface(Color.FromArgb(105, 132, 33, 139),17), 30f))
            using (Pen border = Synthwave.Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
            }
            }
            graphics.DrawImageUnscaled(surface,0,0);
        }
        protected override void Dispose(bool disposing) {if(disposing && surface!=null)surface.Dispose();base.Dispose(disposing);}

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
        public bool SimpleStyle { get; set; }

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
            if (SimpleStyle)
            {
                using (GraphicsPath path = RoundedPanel.RoundedPath(new Rectangle(1,1,Width-3,Height-3), 10))
                using (SolidBrush brush = Synthwave.Brush(Synthwave.Surface(hovering ? ControlPaint.Light(AccentColor) : AccentColor, hovering ? 36 : 25)))
                using (Pen border = Synthwave.Pen(Color.FromArgb(150, 145, 120, 160)))
                { e.Graphics.FillPath(brush,path); }
                Synthwave.Text(e.Graphics, Text, Font, ClientRectangle, Color.White, true);
                return;
            }
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
                    using (LinearGradientBrush brush = Synthwave.Gradient(ClientRectangle, Synthwave.Surface(Color.FromArgb(hovering ? 205 : 145, 203, 37, 176), hovering ? 38 : 25), Synthwave.Surface(Color.FromArgb(hovering ? 200 : 135, 75, 68, 170), hovering ? 32 : 20), LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
                else
                {
                    using (SolidBrush brush = Synthwave.Brush(fill)) e.Graphics.FillPath(brush, path);
                }
                if (effect > 0)
                {

                    int sweepX = (int)((1f - effect) * (Width + 160)) - 80;
                    GraphicsState flashState = e.Graphics.Save(); e.Graphics.SetClip(path, CombineMode.Intersect);
                    using (LinearGradientBrush flash = Synthwave.Gradient(new Rectangle(sweepX, 0, 100, Height), Color.FromArgb(0, Color.White), Color.FromArgb((int)(effect * 150), Color.White), LinearGradientMode.Horizontal)) e.Graphics.FillRectangle(flash, new Rectangle(sweepX, 0, 100, Height));
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
            using (SolidBrush brush = Synthwave.Brush(track))
            using (SolidBrush knob = Synthwave.Brush(Color.FromArgb(249, 250, 251)))
            {
                e.Graphics.FillPath(brush, path);
                int knobX = Checked ? toggle.Right - 15 : toggle.X + 3;
                e.Graphics.FillEllipse(knob, knobX, toggle.Y + 3, 12, 12);
            }

            Synthwave.DrawText(e.Graphics, Text, Font, new Rectangle(42, 0, Width - 42, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused)
            {
                using (Pen focus = Synthwave.Pen(Color.FromArgb(135, AccentColor), 1f))
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
            Font = new Font(Synthwave.FontFamily, 16f, FontStyle.Regular);
            TextAlign = ContentAlignment.MiddleCenter;
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            HoverColor = normalColor;
            MouseEnter += (sender, args) => ForeColor = HoverColor;
            MouseLeave += (sender, args) => ForeColor = normalColor;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Synthwave.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

