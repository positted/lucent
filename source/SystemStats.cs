using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TextBlock=Lucent.SmoothText;

namespace Lucent
{
    // The link to Lucent Sensors, the helper that reads the processor temperature when Lucent's setup has installed it (setup puts the helper in
    // the "Lucent Sensors" folder in Program Files and creates the scheduled task that starts it with the rights it needs). The helper offers the number on a pipe only
    // the signed-in user can open; this reads it. If the helper is not there or not running, the answer is simply -1.
    internal sealed class SensorLink:IDisposable
    {
        // The helper lives in Program Files, where only an administrator can change it: a task that runs with extra rights must never run a file
        // that ordinary programs are able to replace.
        internal static string HelperFolder
        {
            get
            {
                string files=Environment.GetEnvironmentVariable("ProgramW6432");
                if(string.IsNullOrEmpty(files))files=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return System.IO.Path.Combine(files,"Lucent Sensors");
            }
        }
        // Setup writes task.ok only after it created the task that starts the helper.
        internal static bool Installed {get{return System.IO.File.Exists(System.IO.Path.Combine(HelperFolder,"LucentSensors.exe"))&&System.IO.File.Exists(System.IO.Path.Combine(HelperFolder,"task.ok"));}}
        internal static string DefaultPipe(){return "lucent-sensors-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;}
        internal string Pipe=DefaultPipe();
        // Tests turn this on to talk to a helper they started themselves, and replace how the helper is started.
        internal bool ForceAvailable;
        internal Func<bool> Launch=StartTask;
        volatile bool stopped;
        double latest=-1;DateTime latestAt=DateTime.MinValue;DateTime nextLaunch=DateTime.MinValue;
        System.Threading.Thread reader;
        readonly object gate=new object();
        static bool StartTask()
        {
            try
            {
                using(var process=Process.Start(new ProcessStartInfo("schtasks.exe","/run /tn \"Lucent Sensors\""){CreateNoWindow=true,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden}))return process!=null;
            }
            catch(Exception){return false;}
        }
        // The newest reading, or -1 when there is none or it is old.
        public double Read()
        {
            if(!ForceAvailable&&!Installed)return -1;
            lock(gate){if(reader==null){reader=new System.Threading.Thread(Listen){IsBackground=true,Name="Lucent sensor link"};reader.Start();}}
            lock(gate)return DateTime.UtcNow-latestAt<TimeSpan.FromSeconds(6)?latest:-1;
        }
        void Listen()
        {
            while(!stopped)
            {
                try
                {
                    using(var pipe=new System.IO.Pipes.NamedPipeClientStream(".",Pipe,System.IO.Pipes.PipeDirection.In))
                    {
                        pipe.Connect(1500);
                        using(var lines=new System.IO.StreamReader(pipe))
                        {
                            string line;
                            while(!stopped&&(line=lines.ReadLine())!=null)
                            {
                                double value;
                                if(line.StartsWith("cpu=")&&double.TryParse(line.Substring(4),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))
                                    lock(gate){latest=value;latestAt=DateTime.UtcNow;}
                            }
                        }
                    }
                }
                catch(TimeoutException)
                {
                    // Nobody is serving yet: ask for the helper to be started, but not more than twice a minute.
                    if(DateTime.UtcNow>=nextLaunch){nextLaunch=DateTime.UtcNow.AddSeconds(30);Launch();}
                }
                catch(System.IO.IOException){}
                catch(UnauthorizedAccessException){}
                for(int i=0;i<10&&!stopped;i++)System.Threading.Thread.Sleep(200);
            }
        }
        public void Dispose(){stopped=true;}
    }

    // One reading of this PC.

    // One reading of this PC. A value of -1 means "not available on this computer".
    internal sealed class SystemSnapshot
    {
        public double Cpu=-1,CpuTemp=-1,RamUsedGb,RamTotalGb,Gpu=-1,GpuTemp=-1,VramUsedGb=-1,VramTotalGb=-1;
        public string GpuName="";
        public double RamFraction {get{return RamTotalGb<=0?-1:Math.Max(0,Math.Min(1,RamUsedGb/RamTotalGb));}}
        public double VramFraction {get{return VramTotalGb<=0||VramUsedGb<0?-1:Math.Max(0,Math.Min(1,VramUsedGb/VramTotalGb));}}
    }

    // Reads how busy and how warm this PC is. Everything is read-only: the processor load comes from the system's own counters, the memory
    // from Windows, the graphics card from NVIDIA's monitoring library (the same one nvidia-smi uses) when an NVIDIA card is there, and the
    // processor temperature from the Windows thermal-zone counter when the PC has one. Nothing is changed and nothing leaves the PC.
    internal sealed class SystemStats:IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]struct FileTime{public uint Low,High;}
        [DllImport("kernel32.dll")]static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
        [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended;}
        [DllImport("kernel32.dll")]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        static class Nvml
        {
            [StructLayout(LayoutKind.Sequential)]public struct Utilization{public uint Gpu,Memory;}
            [StructLayout(LayoutKind.Sequential)]public struct Memory{public ulong Total,Free,Used;}
            [DllImport("nvml.dll")]public static extern int nvmlInit_v2();
            [DllImport("nvml.dll")]public static extern int nvmlShutdown();
            [DllImport("nvml.dll")]public static extern int nvmlDeviceGetHandleByIndex_v2(uint index,out IntPtr device);
            [DllImport("nvml.dll")]public static extern int nvmlDeviceGetTemperature(IntPtr device,int sensor,out uint temperature);
            [DllImport("nvml.dll")]public static extern int nvmlDeviceGetUtilizationRates(IntPtr device,out Utilization utilization);
            [DllImport("nvml.dll")]public static extern int nvmlDeviceGetMemoryInfo(IntPtr device,out Memory memory);
            [DllImport("nvml.dll",CharSet=CharSet.Ansi)]public static extern int nvmlDeviceGetName(IntPtr device,StringBuilder name,uint length);
        }

        // The circles Home can show, in their order, and the ones shown unless the user chooses otherwise.
        internal static readonly string[] RingNames={"CPU","CPU temp","RAM","GPU","GPU temp","Video memory"};
        internal const string DefaultRings="CPU,CPU temp,RAM,GPU,GPU temp";
        internal static string CleanRings(string text)
        {
            if(text==null)return DefaultRings;
            text=text.Replace("Memory,","RAM,").Replace(",Memory",",RAM");   // it used to be called Memory
            if(text=="Memory")text="RAM";
            var wanted=new System.Collections.Generic.HashSet<string>(text.Split(new[]{","},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()),StringComparer.OrdinalIgnoreCase);
            return string.Join(",",RingNames.Where(wanted.Contains).ToArray());
        }
        ulong lastIdle,lastBusy;
        bool haveCpu,nvidiaTried,nvidiaOk,heatTried;
        IntPtr gpu;
        string gpuName="";
        PerformanceCounter heat;

        static ulong Join(FileTime time){return ((ulong)time.High<<32)|time.Low;}

        public SystemSnapshot Sample()
        {
            var snapshot=new SystemSnapshot();
            try
            {
                FileTime idle,kernel,user;
                if(GetSystemTimes(out idle,out kernel,out user))
                {
                    ulong idleNow=Join(idle),busyNow=Join(kernel)+Join(user)-idleNow;
                    if(haveCpu)
                    {
                        ulong idleGap=idleNow-lastIdle,busyGap=busyNow-lastBusy,total=idleGap+busyGap;
                        if(total>0)snapshot.Cpu=Math.Max(0,Math.Min(100,100.0*busyGap/total));
                    }
                    lastIdle=idleNow;lastBusy=busyNow;haveCpu=true;
                }
                var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
                if(GlobalMemoryStatusEx(ref memory))
                {
                    snapshot.RamTotalGb=memory.TotalPhys/1073741824.0;
                    snapshot.RamUsedGb=(memory.TotalPhys-memory.AvailPhys)/1073741824.0;
                }
            }
            catch(Exception){}
            snapshot.CpuTemp=ReadHeat();
            ReadGraphics(snapshot);
            return snapshot;
        }
        // The temperature Windows itself shares: the thermal-zone counter, in kelvin. Laptops and many boards have it; many desktops do not.
        // Reading a processor's own sensor directly takes a kernel driver and administrator rights, which is how other tools do it. Lucent does
        // not install drivers (anti-cheat treats them as a risk), so where Windows shares nothing, no processor temperature is shown.
        SensorLink sensors;
        double ReadHeat()
        {
            double windows=ReadWindowsHeat();
            if(windows>0)return windows;
            if(!SensorLink.Installed)return -1;
            if(sensors==null)sensors=new SensorLink();
            return sensors.Read();
        }
        double ReadWindowsHeat()
        {
            try
            {
                if(!heatTried)
                {
                    heatTried=true;
                    if(PerformanceCounterCategory.Exists("Thermal Zone Information"))
                    {
                        var names=new PerformanceCounterCategory("Thermal Zone Information").GetInstanceNames();
                        if(names.Length>0)heat=new PerformanceCounter("Thermal Zone Information","Temperature",names[0],true);
                    }
                }
                if(heat==null)return -1;
                double celsius=heat.NextValue()-273.15;
                return celsius>5&&celsius<130?celsius:-1;
            }
            catch(Exception){heat=null;return -1;}
        }        void ReadGraphics(SystemSnapshot snapshot)
        {
            try
            {
                if(!nvidiaTried)
                {
                    nvidiaTried=true;
                    try
                    {
                        if(Nvml.nvmlInit_v2()==0&&Nvml.nvmlDeviceGetHandleByIndex_v2(0,out gpu)==0)
                        {
                            nvidiaOk=true;
                            var name=new StringBuilder(96);
                            if(Nvml.nvmlDeviceGetName(gpu,name,96)==0)gpuName=name.ToString();
                        }
                    }
                    catch(DllNotFoundException){}
                    catch(EntryPointNotFoundException){}
                }
                if(!nvidiaOk)return;
                snapshot.GpuName=gpuName;
                Nvml.Utilization use;uint temperature;Nvml.Memory video;
                if(Nvml.nvmlDeviceGetUtilizationRates(gpu,out use)==0)snapshot.Gpu=use.Gpu;
                if(Nvml.nvmlDeviceGetTemperature(gpu,0,out temperature)==0)snapshot.GpuTemp=temperature;
                if(Nvml.nvmlDeviceGetMemoryInfo(gpu,out video)==0){snapshot.VramTotalGb=video.Total/1073741824.0;snapshot.VramUsedGb=video.Used/1073741824.0;}
            }
            catch(Exception){nvidiaOk=false;}
        }
        public void Dispose()
        {
            if(heat!=null){heat.Dispose();heat=null;}
            if(sensors!=null){sensors.Dispose();sensors=null;}
            if(nvidiaOk){try{Nvml.nvmlShutdown();}catch(Exception){}nvidiaOk=false;}
        }
    }

    // A circle that fills as a number rises, with the number in the middle. It eases to each new value. It stays in Lucent's black and white;
    // only a high reading warms it, to amber and then a soft red.
    internal sealed class StatRing:Grid
    {
        public static readonly DependencyProperty ShownProperty=DependencyProperty.Register("Shown",typeof(double),typeof(StatRing),new PropertyMetadata(0d,(d,e)=>((StatRing)d).dial.InvalidateVisual()));
        const double Size=84,Thickness=7;
        sealed class Dial:FrameworkElement
        {
            internal Func<double> Shown=()=>0;
            internal Color Ink=Color.FromRgb(245,245,247);
            protected override void OnRender(DrawingContext dc)
            {
                double middle=Size/2,radius=middle-Thickness/2-1,fraction=Math.Max(0,Math.Min(1,Shown()));
                var centre=new Point(middle,middle);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(12,255,255,255)),new Pen(new SolidColorBrush(Color.FromArgb(36,255,255,255)),Thickness),centre,radius,radius);
                if(fraction<=.004)return;
                var pen=new Pen(new SolidColorBrush(Ink),Thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                if(fraction>=.999){dc.DrawEllipse(null,pen,centre,radius,radius);return;}
                double sweep=fraction*2*Math.PI,start=-Math.PI/2;
                var from=new Point(middle+radius*Math.Cos(start),middle+radius*Math.Sin(start));
                var to=new Point(middle+radius*Math.Cos(start+sweep),middle+radius*Math.Sin(start+sweep));
                var arc=new StreamGeometry();
                using(var draw=arc.Open()){draw.BeginFigure(from,false,false);draw.ArcTo(to,new Size(radius,radius),0,sweep>Math.PI,SweepDirection.Clockwise,true,false);}
                arc.Freeze();
                dc.DrawGeometry(null,pen,arc);
            }
        }
        readonly Dial dial=new Dial();
        readonly TextBlock value=new TextBlock {FontSize=19,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        double target;
        Brush ink=Inkfor(-1);
        internal double Fraction {get{return target;}}
        internal string Reading {get{return value.Text;}}
        internal Brush Ink {get{return ink;}}
        double Shown {get{return (double)GetValue(ShownProperty);}set{SetValue(ShownProperty,value);}}

        public StatRing()
        {
            Width=Height=Size;
            dial.Width=dial.Height=Size;dial.Shown=()=>Shown;
            Children.Add(dial);
            Children.Add(value);
        }
        // fraction is 0 to 1; a negative one shows an empty circle with a dash.
        public void Set(double fraction,string text)
        {
            value.Text=text;
            double next=fraction<0?0:Math.Max(0,Math.Min(1,fraction));
            ink=Inkfor(fraction<0?-1:next);
            dial.Ink=((SolidColorBrush)ink).Color;
            target=next;
            if(IsLoaded&&Visibility==Visibility.Visible)BeginAnimation(ShownProperty,new DoubleAnimation(Shown,next,TimeSpan.FromMilliseconds(700)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            else{BeginAnimation(ShownProperty,null);Shown=next;dial.InvalidateVisual();}
        }
        internal static Brush Inkfor(double fraction)
        {
            if(fraction<0)return new SolidColorBrush(Color.FromArgb(90,255,255,255));
            if(fraction<.7)return new SolidColorBrush(Color.FromRgb(245,245,247));
            if(fraction<.9)return new SolidColorBrush(Color.FromRgb(255,196,98));
            return new SolidColorBrush(Color.FromRgb(255,112,96));
        }
    }
}