using System;
using System.Windows.Forms;
using System.Threading;

internal static class DummyBoostApp
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Thread load = new Thread(() => { double value = 1; while (true) value = Math.Sqrt(value + 1); });
        load.IsBackground = true;
        load.Start();
        Application.Run(new Form { Text = "Lucent Boost Test App", ShowInTaskbar = true });
    }
}
