using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
namespace ResolutionSwitcher
{
    internal static class RefreshSound
    {
        private static readonly MemoryStream stream=Create();
        private static readonly SoundPlayer player=new SoundPlayer(stream);
        public static void Play(){try{player.Play();}catch{}}
        private static MemoryStream Create()
        {
            const int rate=44100,count=7938;MemoryStream data=new MemoryStream();BinaryWriter writer=new BinaryWriter(data);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
            for(int i=0;i<count;i++){double t=i/(double)rate,envelope=Math.Sin(Math.PI*i/count)*Math.Exp(-t*14);double note=t<.075?1046.5:1396.9;writer.Write((short)(Math.Sin(2*Math.PI*note*t)*envelope*4300));}
            data.Position=0;return data;
        }
    }
    internal static class SwitchSound
    {
        private static readonly MemoryStream stream = Create(false);
        private static readonly SoundPlayer player = new SoundPlayer(stream);
        // Going back to the first size answers with the same two notes the other way round, so the pair sounds related but you can tell which way you went.
        private static readonly MemoryStream backStream = Create(true);
        private static readonly SoundPlayer backPlayer = new SoundPlayer(backStream);
        public static void Play() { try { player.Play(); } catch { } }
        public static void PlayBack() { try { backPlayer.Play(); } catch { } }
        private static MemoryStream Create(bool back)
        {
            const int rate=44100, count=10584;
            MemoryStream data=new MemoryStream(); BinaryWriter w=new BinaryWriter(data);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
            for(int i=0;i<count;i++)
            {
                double t=i/(double)rate;
                double tone=back?Note(t,523.25,.12)+.8*Note(t-.072,783.99,.16):Note(t,783.99,.12)+.8*Note(t-.072,523.25,.16);
                w.Write((short)(tone*3900));
            }
            data.Position=0; return data;
        }
        private static double Note(double time,double frequency,double duration)
        {
            if(time<0||time>=duration)return 0;
            double envelope=Math.Min(1,time/.008)*Math.Pow(1-time/duration,2);
            return envelope*Math.Sin(2*Math.PI*frequency*time);
        }
    }
    internal sealed class HotkeyDialog : Form
    {
        public Keys SelectedHotkey;
        public Func<Keys,bool> TrySave = null;
        public HotkeyDialog(Keys current)
        {
            SelectedHotkey=current;Text="Switch hotkey";Font=new Font(Synthwave.FontFamily,9);ClientSize=new Size(380,175);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;
            Controls.Add(new Label {Text="Click below and press your new shortcut.",AutoSize=true,Location=new Point(20,18)});
            TextBox capture=new TextBox {ReadOnly=true,Text=current==Keys.None ? "Unbound" : new KeysConverter().ConvertToString(current),Location=new Point(20,46),Size=new Size(340,26)};Controls.Add(capture);
            Label hint=new Label {Text="Use an F-key, or Ctrl / Alt with another key.",Location=new Point(20,80),Size=new Size(340,40)};Controls.Add(hint);
            Button save=new Button {Text="Save",Location=new Point(184,132),Size=new Size(84,28)};Controls.Add(save);
            Button cancel=new Button {Text="Cancel",Location=new Point(276,132),Size=new Size(84,28),DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
            Button unbind=new Button {Text="Unbind",Location=new Point(20,132),Size=new Size(84,28)};Controls.Add(unbind);
            unbind.Click+=(sender,e)=> { if(TrySave!=null && TrySave(Keys.None)) { SelectedHotkey=Keys.None; DialogResult=DialogResult.OK; Close(); } };
            capture.KeyDown+=(sender,e)=> {e.SuppressKeyPress=true; Keys k=e.KeyCode; if(k==Keys.ControlKey||k==Keys.ShiftKey||k==Keys.Menu)return; if(k==Keys.Escape){Close();return;} if(!((k>=Keys.F1&&k<=Keys.F24)||((e.Control||e.Alt)&&k!=Keys.None))) {hint.Text="Choose an F-key, or include Ctrl / Alt.";return;} SelectedHotkey=e.KeyData;capture.Text=new KeysConverter().ConvertToString(SelectedHotkey);hint.Text="Click Save to use this shortcut.";};
            save.Click+=(sender,e)=> {if(TrySave!=null&&TrySave(SelectedHotkey)){DialogResult=DialogResult.OK;Close();}else hint.Text="That shortcut is unavailable. Try another combination.";};
            Shown+=(sender,e)=>capture.Focus();
            Synthwave.StyleDialog(this);
        }
    }
}

