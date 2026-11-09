using System;
using System.IO;
using System.Windows.Forms;

// A disposable app that refuses to close, like one showing an unsaved-work prompt.
// Boost must leave it running. It exits only when the test writes stop.txt.
static class WindowFixture
{
    [STAThread]
    static void Main(string[] args)
    {
        string folder = args[0];
        var form = new Form { StartPosition = FormStartPosition.Manual, Left = -20000, Top = -20000, Text = "Fixture" };
        form.FormClosing += (s, e) => e.Cancel = true;
        var timer = new Timer { Interval = 50 };
        timer.Tick += (s, e) => { if (File.Exists(Path.Combine(folder, "stop.txt"))) Environment.Exit(0); };
        form.Shown += (s, e) => { timer.Start(); File.WriteAllText(Path.Combine(folder, "ready.txt"), "ready"); };
        Application.Run(form);
    }
}
