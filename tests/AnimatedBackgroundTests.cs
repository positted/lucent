using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Lucent
{
    internal static class AnimatedBackgroundTests
    {
        // A 2x1 GIF with a red canvas, a blue partial frame, and a white partial
        // frame. The blue frame requests restore-to-background disposal.
        static readonly byte[] Fixture = {
            71,73,70,56,57,97,2,0,1,0,129,0,0,0,0,0,255,255,255,255,0,0,0,0,255,
            33,249,4,4,5,0,0,0,44,0,0,0,0,2,0,1,0,0,2,2,148,10,0,
            33,249,4,8,10,0,0,0,44,1,0,0,0,1,0,1,0,0,2,2,92,1,0,
            33,249,4,4,10,0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,76,1,0,59
        };

        [STAThread]
        public static int Main()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            int result = 0;
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gif-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "fixture.gif");
            File.WriteAllBytes(path, Fixture);
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await Run(path); }
                catch (Exception error) { Console.WriteLine(error); result = 1; }
                finally { app.Shutdown(); }
            }));
            app.Run();
            File.Delete(path);
            Directory.Delete(folder);
            return result;
        }

        static async Task Run(string path)
        {
            using (var background = new AnimatedBackground())
            {
                Assert(await background.LoadFileAsync(path), "GIF load failed.");
                object animation = GetField(background, "animation");
                var frames = (BitmapSource[])GetField(animation, "Frames");
                var delays = (int[])GetField(animation, "Delays");
                Assert(frames.Length == 3 && delays[0] == 50 && delays[1] == 100 && delays[2] == 100, "GIF timing changed.");
                CheckPixels(frames[0], new byte[] { 0,0,255,255, 0,0,255,255 });
                CheckPixels(frames[1], new byte[] { 0,0,255,255, 255,0,0,255 });
                CheckPixels(frames[2], new byte[] { 255,255,255,255, 0,0,0,255 });

                try
                {
                    await background.LoadFileAsync(path + ".missing");
                    throw new Exception("A missing GIF was accepted.");
                }
                catch (FileNotFoundException) { }
                Assert(GetField(background, "animation") == animation, "A failed load removed the previous GIF.");

                Task<bool> superseded = background.LoadFileAsync(path);
                Task<bool> replacement = background.LoadFileAsync(path);
                Assert(!await superseded && await replacement, "Latest GIF selection did not win.");

                var window = new Window
                {
                    Content = background, Width = 64, Height = 64, Left = -10000, Top = -10000,
                    ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None
                };
                try
                {
                    window.Show();
                    background.SetPlaying(true);
                    await Task.Delay(90);
                    var clock = (Stopwatch)GetField(background, "clock");
                    Assert(clock.IsRunning && clock.ElapsedMilliseconds >= 50, "GIF playback did not start.");
                    background.SetPlaying(false);
                    Assert(!clock.IsRunning, "Disabling motion left playback running.");
                    background.SetPlaying(true);
                    window.Hide();
                    Assert(!clock.IsRunning, "Hidden GIF continued playing.");
                }
                finally { window.Close(); }

                Task<bool> canceled = background.LoadFileAsync(path);
                background.Clear();
                Assert(!await canceled && GetField(background, "animation") == null, "Clearing the background allowed a pending load to restore it.");
            }
            Console.WriteLine("PASS GIF backgrounds: partial frames, disposal, exact delays, frozen pixels, async replacement, failed-load preservation, pause/hide, pending-load cancellation and disposal.");
        }

        static object GetField(object instance, string name)
        {
            return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        static void CheckPixels(BitmapSource frame, byte[] expected)
        {
            Assert(frame.IsFrozen, "Decoded bitmap is not frozen.");
            var pixels = new byte[8];
            frame.CopyPixels(pixels, 8, 0);
            for (int i = 0; i < pixels.Length; i++) Assert(pixels[i] == expected[i], "GIF compositing or disposal is incorrect.");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
