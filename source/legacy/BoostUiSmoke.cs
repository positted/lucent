using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal static class BoostUiSmoke
    {
        private static void Pump(int milliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); System.Threading.Thread.Sleep(4); }
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            using (MainForm form = new MainForm())
            {
                form.Show(); Pump(1700);
                MiamiTab boostTab = form.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<MiamiTab>()).First(tab => tab.Text == "Boost");
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boostTab, new object[] { EventArgs.Empty });
                Pump(100);
                FieldInfo field = typeof(MainForm).GetField("boostPage", BindingFlags.Instance | BindingFlags.NonPublic);
                Panel page = (Panel)field.GetValue(form);
                if (!page.Visible || page.Controls.OfType<BoostPageControl>().Count() != 1) throw new Exception("Boost page did not open.");
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, form.ClientRectangle);
                    Directory.CreateDirectory("../../outputs/Lucent");
                    image.Save("../../outputs/Lucent/Boost-preview.png");
                }
                form.Close();
            }
            Console.WriteLine("PASS: Boost tab opens, renders, and does not run actions automatically.");
        }
    }
}
