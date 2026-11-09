using System;
using System.Drawing;

namespace ResolutionSwitcher
{
    internal static class ShapePixelCheck
    {
        private static readonly Color Background = Color.Magenta;

        private static Bitmap Render(string shape, float scale, bool outline)
        {
            Bitmap bitmap = new Bitmap(201, 201);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Background);
                CrosshairArt.Draw(graphics, new Rectangle(0, 0, 201, 201), shape, Color.White, scale, outline);
            }
            return bitmap;
        }

        private static void AssertSymmetric(Bitmap bitmap, string name)
        {
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y) != bitmap.GetPixel(bitmap.Width - 1 - x, y) ||
                        bitmap.GetPixel(x, y) != bitmap.GetPixel(x, bitmap.Height - 1 - y))
                        throw new Exception(name + " is asymmetric.");
        }

        [STAThread]
        private static void Main()
        {
            foreach (float scale in new[] { .1f, .6f, 1f, 5f, 20f })
            {
                using (Bitmap dot = Render("Dot", scale, false))
                using (Bitmap square = Render("Square", scale, false))
                using (Bitmap outlinedDot = Render("Dot", scale, true))
                {
                    AssertSymmetric(dot, "Dot " + scale);
                    AssertSymmetric(square, "Square " + scale);
                    AssertSymmetric(outlinedDot, "Outlined dot " + scale);
                    int radius = Math.Max(1, (int)Math.Round(scale * 2));
                    int center = 100;
                    if (dot.GetPixel(center - radius, center - radius).ToArgb() != Background.ToArgb())
                        throw new Exception("Dot has square corners at " + scale + ".");
                    if (square.GetPixel(center - radius, center - radius).ToArgb() != Color.White.ToArgb())
                        throw new Exception("Square is missing its corner at " + scale + ".");
                }
            }
            Console.WriteLine("PASS: Dot is circular, Square is distinct, and both stay symmetric at five sizes.");
        }
    }
}
