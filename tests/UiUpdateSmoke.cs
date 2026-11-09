using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
class UiUpdateSmoke
{
 [STAThread] static void Main()
 {
  var app=new Application();var assembly=Assembly.LoadFrom("Lucent.exe");
  assembly.GetType("Lucent.StateStore").GetField("Folder").SetValue(null,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","ui-update"));
  var type=assembly.GetType("Lucent.Shell");
  var shell=(Window)Activator.CreateInstance(type,new object[]{true});
  shell.WindowStartupLocation=WindowStartupLocation.Manual;shell.Left=-20000;shell.Top=-20000;shell.Show();shell.Hide();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  type.GetMethod("SetStatus",flags).Invoke(shell,new object[]{"Test notification"});
  var status=(TextBlock)type.GetField("status",flags).GetValue(shell);
  var timer=(DispatcherTimer)type.GetField("statusTimer",flags).GetValue(shell);
  if(status.Visibility!=Visibility.Visible||timer.Interval.TotalSeconds!=5)throw new Exception("Notification timeout");
  type.GetMethod("ShowPage",flags).Invoke(shell,new object[]{"Backgrounds"});
  if(status.Visibility!=Visibility.Collapsed||timer.IsEnabled)throw new Exception("Notification did not clear on navigation");
  var toggle=(CheckBox)type.GetField("eclipseToggle",flags).GetValue(shell);
  toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
  var preferences=type.GetField("preferences",flags).GetValue(shell);
  if(!(bool)preferences.GetType().GetField("HideEclipse").GetValue(preferences))throw new Exception("Eclipse off not applied");
  toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
  if((bool)preferences.GetType().GetField("HideEclipse").GetValue(preferences))throw new Exception("Eclipse on not applied");
  Console.WriteLine("PASS: five-second notifications, navigation dismissal and inline Eclipse toggle.");
  shell.Close();app.Shutdown();
 }
}
