using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal static class BoostSmoke
    {
        private static bool WaitFor(Func<bool> condition, int milliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds)
            {
                if (condition()) return true;
                Application.DoEvents(); Thread.Sleep(40);
            }
            return condition();
        }

        [STAThread]
        private static void Main()
        {
            string[] protectedNames = { "VALORANT-Win64-Shipping", "RiotClientServices", "vgc", "vgtray", "explorer", "audiodg", "svchost", "MsMpEng", "lghub_agent" };
            if (protectedNames.Any(name => !BackgroundAppBoost.IsProtectedProcessName(name))) throw new Exception("Protected process list is incomplete.");
            if (BackgroundAppBoost.IsProtectedProcessName("DummyBoostApp")) throw new Exception("Test process was unexpectedly protected.");
            if (BackgroundAppBoost.Catalog.Length < 25) throw new Exception("Smart scan catalog is too small.");

            Process dummy = Process.Start("DummyBoostApp.exe");
            try
            {
                if (!WaitFor(() => Process.GetProcessesByName("DummyBoostApp").Length > 0, 4000)) throw new Exception("Dummy app did not start.");
                BoostAppDefinition definition = new BoostAppDefinition("dummy", "Dummy", "DummyBoostApp", "DummyBoostApp");
                BackgroundAppBoost engine = new BackgroundAppBoost(new[] { definition });
                if (!engine.Detect()["dummy"]) throw new Exception("Running optional app was not detected.");
                BoostCandidate candidate = engine.DetectCandidates().Single();
                if (candidate.Instances < 1 || candidate.MemoryBytes <= 0 || candidate.CpuPercent <= 0) throw new Exception("CPU and RAM usage were not measured.");
                BoostResult stopped = engine.StopSelected(new[] { "dummy" });
                if (stopped.AffectedApps != 1 || !WaitFor(() => Process.GetProcessesByName("DummyBoostApp").Length == 0, 4000)) throw new Exception("Selected app was not stopped.");
                if (!engine.HasRestorableApps) throw new Exception("Restore state was not captured.");
                BoostResult restored = engine.RestoreStopped();
                if (restored.AffectedApps != 1 || !WaitFor(() => Process.GetProcessesByName("DummyBoostApp").Length > 0, 4000)) throw new Exception("Stopped app was not restored.");
                Console.WriteLine("PASS: smart process scan, CPU and RAM measurement, opt-in stop, protected process guard, and restore.");
            }
            finally
            {
                foreach (Process process in Process.GetProcessesByName("DummyBoostApp"))
                {
                    try { process.Kill(); process.WaitForExit(2000); } catch { }
                    process.Dispose();
                }
                if (dummy != null) dummy.Dispose();
            }
        }
    }
}
