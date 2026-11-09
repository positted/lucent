using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lucent;
class RefinementsTests
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static Assembly assembly=typeof(Preferences).Assembly;
    static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
    static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Private).Invoke(target,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread] static int Main()
    {
        var app=new Application();int code=0;
        app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run();}catch(Exception e){Console.WriteLine(e);code=1;}finally{app.Shutdown();}}));app.Run();return code;
    }
    static async Task Run()
    {
        string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","refinements");Directory.CreateDirectory(folder);
        assembly.GetType("Lucent.StateStore").GetField("Folder").SetValue(null,folder);
        var shell=(Window)Activator.CreateInstance(assembly.GetType("Lucent.Shell"),new object[]{true});
        shell.WindowStartupLocation=WindowStartupLocation.Manual;shell.Left=-20000;shell.Top=-20000;shell.Show();
        try
        {
            Call(shell,"ShowPage","Backgrounds");await Task.Delay(50);
            string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backgrounds","star-drift.gif");
            await (Task)Call(shell,"ImportCustomBackground",source);
            var prefs=(Preferences)Field(shell,"preferences");
            Check(prefs.CustomBackgrounds.Count==1,"Import did not create library card");
            string imported=prefs.CustomBackgrounds[0];Check(File.Exists(imported)&&imported!=source,"Managed import copy");
            Check(prefs.BackgroundFile==imported,"Import did not apply");
            var toggle=(CheckBox)Field(shell,"eclipseToggle");toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            var player=Field(shell,"animatedBackground");player.GetType().GetMethod("SetPlaying").Invoke(player,new object[]{true});
            await Task.Delay(100);Check(((System.Diagnostics.Stopwatch)Field(player,"clock")).IsRunning,"GIF stops with eclipse off");
            Call(shell,"ShowPage","Profiles");var content=(ContentControl)Field(shell,"content");Check(content.FocusVisualStyle==null&&!content.Focusable,"Page focus border");
            await (Task)Call(shell,"RemoveCustomBackground",imported,new Border());
            Check(prefs.CustomBackgrounds.Count==0&&!File.Exists(imported)&&File.Exists(source),"Remove did not isolate owned copy");
            Check(prefs.EclipseOnly&&prefs.BackgroundFile=="","Deleting active GIF did not restore eclipse");
            await (Task)Call(shell,"RemoveCustomBackground","library:star-drift.gif",new Border());Check(File.Exists(source),"Built-in removal accepted");
            Call(shell,"ShowPage","Crosshair");
            var pages=(IDictionary)Field(shell,"pages");var crosshair=(FrameworkElement)pages["Crosshair"];
            var expander=Find<Expander>(crosshair);Check(expander!=null,"Missing outline section");expander.IsExpanded=true;shell.UpdateLayout();
            var boxes=All<TextBox>(expander).ToArray();Check(boxes.Length==4,"Outline lacks same RGB and hex controls");
            prefs.Crosshair.OutlineColor="#00FFFF";
            foreach(string shape in new[]{"Classic","Dot","Square"})
            {
                using(var bitmap=new System.Drawing.Bitmap(64,64))using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                {
                    var art=assembly.GetType("ResolutionSwitcher.CrosshairArt");
                    art.GetMethod("Draw").Invoke(null,new object[]{graphics,new System.Drawing.Rectangle(0,0,64,64),shape,System.Drawing.Color.Red,.6f,true,System.Drawing.Color.Cyan});
                    int cyan=0;for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(bitmap.GetPixel(x,y).G>150&&bitmap.GetPixel(x,y).B>150)cyan++;
                    Check(cyan>0,shape+" outline did not render selected color");
                }
            }
            ((ScrollViewer)content.Parent).ScrollToEnd();shell.UpdateLayout();Call(shell,"CaptureFrame","Crosshair-outline-expanded");
            await (Task)Call(shell,"ImportCustomBackground",source);Call(shell,"ShowPage","Backgrounds");shell.UpdateLayout();Call(shell,"CaptureFrame","Backgrounds-custom-library");
            await (Task)Call(shell,"RemoveCustomBackground",prefs.CustomBackgrounds[0],new Border());
            Call(shell,"ShowPage","Game Boost");
            var clean=All<Button>((FrameworkElement)pages["Game Boost"]).First(b=>Equals(b.Content,"Clean RAM"));
            Call(shell,"ShowMemoryCleanup",clean);await Task.Delay(100);shell.UpdateLayout();Call(shell,"CaptureFrame","Memory-cleaning");
            var field=assembly.GetType("ResolutionSwitcher.NvidiaScalingConfig").GetMethod("SetScalingField",BindingFlags.NonPublic|BindingFlags.Static);
            foreach(int mode in new[]{2,3,5,8})
            {
                byte[] original=Enumerable.Range(0,128).Select(x=>(byte)x).ToArray();byte[] copy=(byte[])original.Clone();field.Invoke(null,new object[]{copy,mode});
                Check(BitConverter.ToInt32(copy,8)==mode,"Scaling mapping");for(int i=0;i<128;i++)if(i<8||i>11)Check(original[i]==copy[i],"Scaling altered unrelated target fields");
            }
            Console.WriteLine("PASS: custom GIF import/apply/remove, original-file preservation, protected built-ins, playback without eclipse, focus border suppression, outline RGB controls and colored rendering, scaling-field isolation.");
        }
        finally{shell.Close();}
    }
    static T Find<T>(DependencyObject node) where T:DependencyObject{return All<T>(node).FirstOrDefault();}
    static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject node) where T:DependencyObject
    {
        if(node is T)yield return (T)node;
        foreach(object child in LogicalTreeHelper.GetChildren(node))if(child is DependencyObject)foreach(T match in All<T>((DependencyObject)child))yield return match;
    }
}
