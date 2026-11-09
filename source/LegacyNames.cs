namespace Lucent
{
    // Lucent used to be called something else. These few names are shared with the versions that are already installed
    // (they are how two copies find each other, and how a crash is recovered from), so they keep their old spelling. Everything
    // a person can see is called Lucent.
    internal static class LegacyNames
    {
        // Only one copy runs at a time, whichever version it is.
        public const string SingleInstanceMutex="Local\\resly.Orbital.UI";
        // Locks that keep two copies from changing the screen's color or resolution at the same moment.
        public const string SoftwareColorMutex="Local\\resly.SoftwareColor";
        public const string GameVibranceMutex="Local\\Resly.GameVibrance";
        public const string CustomTrialMutex="Local\\Resly.CustomResolutionTrial";
        public const string CustomTrialEvent="Local\\Resly.CustomTrial.";
        // Headers of the small recovery files an older version may have left behind.
        public const string DesktopVibranceV2="ReslyDesktopVibrance2";
        public const string DesktopVibranceV1="ReslyDesktopVibrance1";
        public const string VibranceV1="ReslyVibrance1";
        // The settings folder and the start-with-Windows entry of earlier versions. Settings are copied across once on first start.
        public const string DataFolder="resly-next";
        public const string StartupValue="resly-next";
    }
}
