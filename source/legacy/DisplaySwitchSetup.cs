using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class DisplaySwitchSetup
{
    [STAThread] private static void Main(string[] args) { if(args.Length>0 && args[0].StartsWith("--machine-", StringComparison.Ordinal)) { Environment.ExitCode=SetupScope.Worker(args); return; } Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new SetupForm()); }
}

internal sealed class SetupForm : Form
{
    private readonly CheckBox desktopShortcut, startMenuShortcut, launchAfterInstall, startWithWindows;
    private readonly Label status;
    private readonly Button installButton;
    private readonly Button cancelButton;
    private readonly Button uninstallButton;
    private readonly ComboBox installScope; private bool busy;
    private bool installed; private string installedAppPath;

    public SetupForm()
    {
        Text = "Lucent Setup";
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        ClientSize = new Size(510, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font(ResolutionSwitcher.Synthwave.FontFamily, 9f);
        ShowIcon = false;

        Panel header = new Panel { BackColor = SystemColors.Window, Location = Point.Empty, Size = new Size(510, 82) };
        header.Controls.Add(new Label { Text = "Install Lucent", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Location = new Point(24, 18) });
        header.Controls.Add(new Label { Text = "Choose your options, then click Install to continue.", AutoSize = true, Location = new Point(24, 44) });
        Controls.Add(header);
        Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, AutoSize = false, Location = new Point(0, 82), Size = new Size(510, 2) });

        Controls.Add(new Label { Text="Install for", AutoSize=true, Location=new Point(24,101) });
        installScope=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Location=new Point(24,124), Size=new Size(462,26), TabIndex=0 };
        installScope.Items.AddRange(new object[]{"Only for me", "All users (administrator permission required)"});
        installScope.SelectedIndex=0; Controls.Add(installScope);
        FormClosing += (sender,args) => { if(busy) args.Cancel=true; };
        GroupBox options = new GroupBox { Text = "Additional options", Location = new Point(24, 170), Size = new Size(462, 153) };
        Controls.Add(options);
        desktopShortcut = AddOption(options, "Create a desktop shortcut", true, 25);
        startMenuShortcut = AddOption(options, "Add Lucent to the Start menu", true, 54);
        launchAfterInstall = AddOption(options, "Launch Lucent when setup finishes", true, 83);
        startWithWindows = AddOption(options, "Start Lucent when users sign in to Windows", true, 112);
        status = new Label { ForeColor = SystemColors.ControlText, AutoSize = false, Size = new Size(462, 52), Location = new Point(24, 336) };
        Controls.Add(status);
        Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, AutoSize = false, Location = new Point(0, 394), Size = new Size(510, 2) });
        uninstallButton = NewButton("Uninstall", new Point(24, 409));
        uninstallButton.Click += Uninstall; Controls.Add(uninstallButton);
        installButton = NewButton("Install", new Point(294, 409));
        installButton.Click += Install; Controls.Add(installButton);
        cancelButton = NewButton("Cancel", new Point(394, 409));
        cancelButton.Click += (sender, args) => Close(); Controls.Add(cancelButton);
        AcceptButton = installButton; CancelButton = cancelButton;
        options.TabIndex = 1; installButton.TabIndex = 1; cancelButton.TabIndex = 2; uninstallButton.TabIndex = 3;
    }

    private static CheckBox AddOption(Control parent, string text, bool value, int top)
    {
        CheckBox option = new CheckBox { Text = text, Checked = value, AutoSize = true, Location = new Point(16, top), UseVisualStyleBackColor = true, TabIndex = parent.Controls.Count };
        parent.Controls.Add(option); return option;
    }

    private static Button NewButton(string text, Point location)
    { return new Button { Text = text, Size = new Size(92, 28), Location = location, FlatStyle = FlatStyle.Standard, UseVisualStyleBackColor = true }; }
    private void Install(object sender, EventArgs e)
    {
        if (installed)
        {
            try
            {
                if (launchAfterInstall.Checked) Process.Start(installedAppPath);
                Close();
            }
            catch (Exception exception) { status.Text = "Could not launch Lucent: " + exception.Message; }
            return;
        }
        int flags=(desktopShortcut.Checked?1:0)|(startMenuShortcut.Checked?2:0)|(startWithWindows.Checked?4:0);
        if(installScope.SelectedIndex==1) { RunMachine(false,flags); return; }
        try { SetupScope.Install(false,flags); InstallationFinished(false); }
        catch(Exception exception) { status.Text="Setup could not finish: " + exception.Message; }
    }

    private void InstallationFinished(bool all)
    {
        installedAppPath=Path.Combine(SetupScope.AppFolder(all),"Lucent.exe");
        installed=true; installButton.Text="Finish"; cancelButton.Visible=false;
        installScope.Enabled=false; desktopShortcut.Enabled=false; startMenuShortcut.Enabled=false; startWithWindows.Enabled=false; uninstallButton.Enabled=false;
        status.Text="Installed successfully. Select Finish to close setup.";
    }

    private void Uninstall(object sender, EventArgs e)
    {
        bool all=installScope.SelectedIndex==1;
        string prompt=all ? "Remove the all-users installation and shared shortcuts? Personal settings will remain." : "Remove Lucent, its shortcuts, and saved settings from this account?";
        if(MessageBox.Show(prompt,"Uninstall Lucent",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        if(all) {RunMachine(true,0);return;}
        try {SetupScope.Uninstall(false);RemovalFinished();}
        catch(Exception exception) {status.Text="Could not remove Lucent. Close the app and retry. " + exception.Message;}
    }

    private void RemovalFinished()
    {
        status.Text="Lucent was removed for the selected users.";
        installButton.Visible=false;uninstallButton.Visible=false;cancelButton.Text="Finish";
    }

    private void RunMachine(bool remove,int flags)
    {
        Process worker;
        try
        {
            worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,remove?"--machine-uninstall":"--machine-install "+flags) {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
        }
        catch(System.ComponentModel.Win32Exception exception)
        {
            status.Text=exception.NativeErrorCode==1223 ? "Administrator permission was canceled. No changes were made." : "Could not start setup: " + exception.Message;
            return;
        }
        if(worker==null) {status.Text="Could not start the installation.";return;}
        busy=true; installButton.Enabled=false;uninstallButton.Enabled=false;cancelButton.Enabled=false;installScope.Enabled=false;
        desktopShortcut.Enabled=false;startMenuShortcut.Enabled=false;startWithWindows.Enabled=false;
        status.Text=remove?"Removing Lucent for all users…":"Installing Lucent for all users…";
        System.ComponentModel.BackgroundWorker monitor=new System.ComponentModel.BackgroundWorker();
        monitor.DoWork+=(sender,args)=> {using(worker){worker.WaitForExit();args.Result=worker.ExitCode;}};
        monitor.RunWorkerCompleted+=(sender,args)=> {
            busy=false;installButton.Enabled=true;uninstallButton.Enabled=true;cancelButton.Enabled=true;installScope.Enabled=true;
            desktopShortcut.Enabled=true;startMenuShortcut.Enabled=true;startWithWindows.Enabled=true;
            if(args.Error!=null || (int)args.Result!=0)status.Text="Setup could not finish. Close Lucent in all signed-in accounts and try again.";
            else if(remove)RemovalFinished();else InstallationFinished(true);
            monitor.Dispose();
        };
        monitor.RunWorkerAsync();
    }
    private static string StartupShortcutPath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Lucent.lnk"); }
    }

    private static void ConfigureStartup(bool enabled, string appPath)
    {
        ConfigureStartupAt(StartupShortcutPath, enabled, appPath);
    }

    internal static void ConfigureStartupAt(string shortcutPath, bool enabled, string appPath)
    {
        if (enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            CreateShortcut(shortcutPath, appPath);
        }
        else DeleteFileIfPresent(shortcutPath);
    }

    private static void DeleteFileIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void WriteEmbeddedFile(string name, string destination)
    { using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) { if (source == null) throw new InvalidOperationException("The app files were not found in this installer."); using (FileStream output = File.Create(destination)) source.CopyTo(output); } }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell"); object shell = Activator.CreateInstance(shellType); object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath }); Type type = shortcut.GetType();
        type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath }); type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Lucent — switch your saved display modes" }); type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" }); type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null); Marshal.ReleaseComObject(shortcut); Marshal.ReleaseComObject(shell);
    }
}

