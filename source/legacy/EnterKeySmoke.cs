using System;
using System.Reflection;
using System.Windows.Forms;
namespace ResolutionSwitcher {
 internal sealed class EnterKeySmoke {
  [STAThread] static void Main() {
   Application.EnableVisualStyles();
   using (var form = new MainForm()) {
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var button = (Button)typeof(MainForm).GetField("switchButton", flags).GetValue(form);
    button.Enabled = true;
    var args = new KeyEventArgs(Keys.Enter);
    typeof(MainForm).GetMethod("OnFormKeyDown", flags).Invoke(form, new object[] { form, args });
    bool preparing = (bool)typeof(MainForm).GetField("switchPreparation", flags).GetValue(form);
    if (preparing) throw new Exception("Enter still starts a resolution switch.");
    form.Close();
   }
   Console.WriteLine("Enter does not switch resolution.");
  }
 }
}
