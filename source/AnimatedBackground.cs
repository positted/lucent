using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;

namespace Lucent
{
    // Frames are decoded once off the UI thread, then shared as frozen WPF bitmaps.
    // The bounded frame cache avoids decoding a GIF on every composition frame.
    internal sealed class AnimatedBackground : FrameworkElement, IDisposable
    {
        const long MaximumFileBytes = 50L * 1024 * 1024;
        const long MaximumFrameBytes = 128L * 1024 * 1024;
        const int MaximumFrames = 600;
        readonly Stopwatch clock = new Stopwatch();

        public Color AccentColor {get;private set;}
        public void RefreshAccent(){if(animation!=null)AccentColor=Design.Palette(animation.Frames[frameIndex]);}

        CancellationTokenSource pendingLoad;
        Animation animation;
        int frameIndex;
        long nextFrameAt;
        bool playing, disposed;

        sealed class Animation
        {
            internal BitmapSource[] Frames;
            internal int[] Delays;
            internal long Duration;
            internal bool BuiltIn;
        }

        public AnimatedBackground()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            IsVisibleChanged += (sender, args) => UpdatePlayback();
            Unloaded += (sender, args) => StopClock();
            Loaded += (sender, args) => UpdatePlayback();
        }

        public async Task<bool> LoadFileAsync(string path, bool builtIn = false)
        {
            Dispatcher.VerifyAccess();
            if (disposed) throw new ObjectDisposedException("AnimatedBackground");
            CancelLoad();
            var request = new CancellationTokenSource();
            pendingLoad = request;
            try
            {
                if(builtIn)throw new InvalidDataException("Preset backgrounds have been removed. Import your own GIF.");
                var decoded = await Task.Factory.StartNew(() => Decode(path, request.Token, builtIn),
                    request.Token, TaskCreationOptions.None, TaskScheduler.Default);
                if (disposed || request.IsCancellationRequested || pendingLoad != request) return false;
                StopClock();
                animation = decoded;AccentColor=Design.Palette(decoded.Frames[0]);

                frameIndex = 0;
                clock.Reset();
                nextFrameAt = animation.Delays[0];
                InvalidateVisual();
                UpdatePlayback();
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception error)
            {
                if (request.IsCancellationRequested) return false;
                if (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException) throw;
                throw new InvalidDataException("This GIF could not be opened. Try a different animation.", error);
            }
            finally
            {
                if (pendingLoad == request) pendingLoad = null;
                request.Dispose();
            }
        }

        public void SetPlaying(bool value)
        {
            Dispatcher.VerifyAccess();
            if (disposed) return;
            playing = value;
            UpdatePlayback();
        }

        public void Clear()
        {
            Dispatcher.VerifyAccess();
            CancelLoad();
            StopClock();
            clock.Reset();
            animation = null;

            frameIndex = 0;
            InvalidateVisual();
        }

        internal void CancelLoad()
        {
            if (pendingLoad == null) return;
            pendingLoad.Cancel();
            pendingLoad = null;
        }

        void UpdatePlayback()
        {
            if (disposed || !playing || !IsVisible || !IsLoaded || (animation == null || animation.Frames.Length < 2))
            {
                StopClock();
                return;
            }
            if (clock.IsRunning) return;
            clock.Start();
            CompositionTarget.Rendering += AdvanceFrame;
        }

        void StopClock()
        {
            CompositionTarget.Rendering -= AdvanceFrame;
            clock.Stop();
        }

        void AdvanceFrame(object sender, EventArgs args)
        {
            if (!clock.IsRunning) return;
            long elapsed = clock.ElapsedMilliseconds;
            if(animation==null||elapsed<nextFrameAt)return;
            // Skip whole cycles after a busy UI interval, without enqueueing old frames.
            if (elapsed - nextFrameAt >= animation.Duration)
                nextFrameAt += ((elapsed - nextFrameAt) / animation.Duration) * animation.Duration;
            while (elapsed >= nextFrameAt)
            {
                frameIndex = (frameIndex + 1) % animation.Frames.Length;
                nextFrameAt += animation.Delays[frameIndex];
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);

            if (animation == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            var frame = animation.Frames[frameIndex];
            double fitWidth = ActualWidth / frame.PixelWidth;
            double fitHeight = ActualHeight / frame.PixelHeight;
            double scale = animation.BuiltIn ? Math.Min(fitWidth, fitHeight) * .93 : Math.Max(fitWidth, fitHeight);
            double width = frame.PixelWidth * scale;
            double height = frame.PixelHeight * scale;
            var bounds = new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
            if (animation.BuiltIn)
            {
                context.PushOpacityMask(HorizontalFade);
                context.PushOpacityMask(VerticalFade);
            }
            context.DrawImage(frame, bounds);
            if (animation.BuiltIn)
            {
                context.Pop();
                context.Pop();
            }
        }

        static readonly Brush HorizontalFade = CreateEdgeFade(new Point(1, 0));
        static readonly Brush VerticalFade = CreateEdgeFade(new Point(0, 1));

        static Brush CreateEdgeFade(Point end)
        {
            var brush = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.White, .13),
                new GradientStop(Colors.White, .87),
                new GradientStop(Colors.Transparent, 1)
            }, new Point(0, 0), end);
            brush.Freeze();
            return brush;
        }
        static Animation Decode(string path, CancellationToken cancellation, bool builtIn)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumFileBytes)
                    throw new InvalidDataException("Choose a GIF smaller than 50 MB.");
                var header = new byte[10];
                if (stream.Read(header, 0, header.Length) != header.Length || header[0] != 'G' || header[1] != 'I' || header[2] != 'F'
                    || header[3] != '8' || (header[4] != '7' && header[4] != '9') || header[5] != 'a')
                    throw new InvalidDataException("Choose a valid GIF animation.");
                int originalWidth = header[6] | header[7] << 8;
                int originalHeight = header[8] | header[9] << 8;
                if (originalWidth == 0 || originalHeight == 0 || originalWidth > 8192 || originalHeight > 8192
                    || (long)originalWidth * originalHeight > 32000000)
                    throw new InvalidDataException("This GIF is too large. Choose an animation up to 8192 pixels per side and 32 megapixels.");
                cancellation.ThrowIfCancellationRequested();
                stream.Position = 0;
                using (var source = Drawing.Image.FromStream(stream, false, false))
                {
                    int count = source.GetFrameCount(Imaging.FrameDimension.Time);
                    if (count > MaximumFrames)
                        throw new InvalidDataException("Choose a shorter GIF with no more than 600 frames.");
                    // Keep the full animation within the cache budget by scaling large frames.
                    double scale = Math.Min(1, 1600d / Math.Max(source.Width, source.Height));
                    scale = Math.Min(scale, Math.Sqrt(MaximumFrameBytes / (4d * source.Width * source.Height * count)));
                    int width = Math.Max(1, (int)(source.Width * scale));
                    int height = Math.Max(1, (int)(source.Height * scale));
                    var result = new Animation { BuiltIn = builtIn, Frames = new BitmapSource[count], Delays = new int[count] };
                    byte[] delays = null;
                    if (Array.IndexOf(source.PropertyIdList, 0x5100) >= 0) delays = source.GetPropertyItem(0x5100).Value;
                    using (var bitmap = new Drawing.Bitmap(width, height, Imaging.PixelFormat.Format32bppPArgb))
                    using (var graphics = Drawing.Graphics.FromImage(bitmap))
                    using (var colors = new Imaging.ImageAttributes())
                    {
                        if (builtIn) colors.SetGamma(.60f);
                        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        for (int index = 0; index < count; index++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            source.SelectActiveFrame(Imaging.FrameDimension.Time, index);
                            graphics.Clear(Drawing.Color.Transparent);
                            graphics.DrawImage(source, new Drawing.Rectangle(0, 0, width, height),
                                0, 0, source.Width, source.Height, Drawing.GraphicsUnit.Pixel, colors);
                            var bits = bitmap.LockBits(new Drawing.Rectangle(0, 0, width, height),
                                Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppPArgb);
                            try
                            {
                                var frame = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null,
                                    bits.Scan0, bits.Stride * height, bits.Stride);
                                frame.Freeze();
                                result.Frames[index] = frame;
                            }
                            finally { bitmap.UnlockBits(bits); }
                            long delay = delays != null && delays.Length >= (index + 1) * 4 ? BitConverter.ToUInt32(delays, index * 4) * 10L : 100;
                            result.Delays[index] = delay == 0 ? 100 : (int)Math.Max(20,Math.Min(delay, 60000));
                            result.Duration += result.Delays[index];
                        }
                    }
                    return result;
                }
            }
        }

        public void Dispose()
        {
            Dispatcher.VerifyAccess();
            if (disposed) return;
            Clear();
            disposed = true;
        }
    }
}






