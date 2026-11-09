using System;
using System.Drawing;
using System.Windows.Forms;
class SetupPreview
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(SetupForm form=new SetupForm())
        {
            form.Show(); Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save("../../outputs/Lucent/Setup-preview.png"); }
            form.Close();
        }
    }
}
