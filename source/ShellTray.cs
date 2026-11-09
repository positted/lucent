using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Forms=System.Windows.Forms;

namespace Lucent
{
    // One line of the tray menu. A separator has no title.
    internal sealed class TrayEntry
    {
        public string Icon,Title,Detail;
        public Action Run;
        public bool Danger,Separator;
        public static TrayEntry Line(string icon,string title,Action run,string detail=null,bool danger=false){return new TrayEntry {Icon=icon,Title=title,Run=run,Detail=detail,Danger=danger};}
        public static TrayEntry Divider(){return new TrayEntry {Separator=true};}
    }

    // The menu that opens from the tray icon: a rounded card with the display mode on top and one row per place to go, drawn by Lucent
    // so it matches the app. It closes when you click elsewhere or press Escape, and works with the arrow keys.
    internal sealed class TrayPopup:Window
    {
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
        readonly List<Border> rows=new List<Border>();
        readonly List<TrayEntry> lines=new List<TrayEntry>();
        readonly Border card;
        int hot=-1;
        bool chosen;
        internal Border Card {get{return card;}}
        internal IList<TrayEntry> Lines {get{return lines;}}
        internal int Hot {get{return hot;}}
        internal bool Chosen {get{return chosen;}}

        public TrayPopup(string status,IEnumerable<TrayEntry> entries)
        {
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;
            ResizeMode=ResizeMode.NoResize;SizeToContent=SizeToContent.WidthAndHeight;ShowActivated=true;
            Foreground=Brushes.White;FontFamily=Design.Font;FontSize=14;FontWeight=FontWeights.Medium;UseLayoutRounding=true;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
            var list=new StackPanel {Width=272};
            // Display mode on top, with the Lucent mark.
            var head=new Grid {Margin=new Thickness(12,10,12,8)};
            head.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            head.ColumnDefinitions.Add(new ColumnDefinition());
            var mark=Art.Logo(24);mark.Margin=new Thickness(0,0,12,0);mark.VerticalAlignment=VerticalAlignment.Center;
            head.Children.Add(mark);
            var titles=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            titles.Children.Add(new TextBlock {Text="Lucent",FontSize=15,FontWeight=FontWeights.Bold,FontFamily=Design.DisplayFont});
            titles.Children.Add(new TextBlock {Text=status,FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(160,160,166)),Margin=new Thickness(0,1,0,0)});
            Grid.SetColumn(titles,1);head.Children.Add(titles);
            list.Children.Add(head);
            list.Children.Add(Rule());
            foreach(var entry in entries)
            {
                if(entry.Separator){list.Children.Add(Rule());continue;}
                lines.Add(entry);
                var row=Row(entry,lines.Count-1);
                rows.Add(row);
                list.Children.Add(row);
            }
            card=new Border {Background=new SolidColorBrush(Color.FromRgb(28,28,30)),BorderBrush=new SolidColorBrush(Color.FromArgb(46,255,255,255)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(6),Child=list,
                RenderTransform=new TranslateTransform(0,8)};
            // The shadow is its own plain shape behind the card and is kept as a picture, so lighting a row never makes the window
            // redraw a blurred shadow (that is what made the menu lag when the pointer moved quickly over it).
            var shadow=new Border {Background=new SolidColorBrush(Color.FromRgb(28,28,30)),CornerRadius=new CornerRadius(18),CacheMode=new BitmapCache(),
                RenderTransform=card.RenderTransform,Effect=new DropShadowEffect {BlurRadius=24,ShadowDepth=4,Opacity=.5,Color=Colors.Black}};
            var stack=new Grid();stack.Children.Add(shadow);stack.Children.Add(card);
            // Room around the card for its shadow, which a transparent window can show.
            Content=new Border {Padding=new Thickness(14,14,14,14),Child=stack};
            Opacity=0;
            Deactivated+=(s,e)=>Dismiss();
            PreviewKeyDown+=OnKey;
            Loaded+=(s,e)=>
            {
                Design.Animate(this,OpacityProperty,0,1,130);
                ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,TimeSpan.FromMilliseconds(170)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
                Activate();Focus();
            };
        }
        static Border Rule()
        {
            return new Border {Height=1,Background=new SolidColorBrush(Color.FromArgb(34,255,255,255)),Margin=new Thickness(8,5,8,5)};
        }
        Border Row(TrayEntry entry,int index)
        {
            var dock=new DockPanel();
            var ink=entry.Danger?new SolidColorBrush(Color.FromRgb(255,128,128)):Brushes.White;
            var icon=Design.Icon(entry.Icon,19);icon.Margin=new Thickness(0,0,13,0);icon.VerticalAlignment=VerticalAlignment.Center;icon.Opacity=.9;
            DockPanel.SetDock(icon,Dock.Left);dock.Children.Add(icon);
            if(!string.IsNullOrEmpty(entry.Detail))
            {
                var detail=new TextBlock {Text=entry.Detail,FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(160,160,166)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};
                DockPanel.SetDock(detail,Dock.Right);dock.Children.Add(detail);
            }
            dock.Children.Add(new TextBlock {Text=entry.Title,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=ink,VerticalAlignment=VerticalAlignment.Center});
            var row=new Border {CornerRadius=new CornerRadius(11),Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,1,0,1),Background=new SolidColorBrush(Color.FromArgb(0,255,255,255)),Cursor=Cursors.Hand,Child=dock,Tag=entry.Title};
            System.Windows.Automation.AutomationProperties.SetName(row,entry.Title);
            row.MouseEnter+=(s,e)=>Highlight(index);
            row.MouseLeftButtonUp+=(s,e)=>Choose(index);
            return row;
        }
        // One row is lit at a time, whether the pointer or the arrow keys put it there.
        internal void Highlight(int index)
        {
            int before=hot;
            hot=index;
            // Only the row that lost the light and the row that gained it change; the rest are left alone.
            if(before!=hot)Light(before,false);
            Light(hot,true);
        }
        void Light(int index,bool on)
        {
            if(index<0||index>=rows.Count)return;
            byte alpha=(byte)(on?(lines[index].Danger?34:22):0);
            var brush=(SolidColorBrush)rows[index].Background;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Color.FromArgb(alpha,255,lines[index].Danger?(byte)110:(byte)255,lines[index].Danger?(byte)110:(byte)255),TimeSpan.FromMilliseconds(60)));
        }
        internal void Choose(int index)
        {
            if(chosen||index<0||index>=lines.Count)return;
            chosen=true;
            var run=lines[index].Run;
            Close();
            // After the menu is gone, so what it opens is not pulled behind it.
            if(run!=null)Dispatcher.BeginInvoke(new Action(run));
        }
        void Dismiss(){if(!chosen&&IsVisible){chosen=true;Close();}}
        void OnKey(object sender,KeyEventArgs e)
        {
            if(e.Key==Key.Escape){Dismiss();e.Handled=true;}
            else if(e.Key==Key.Down){Highlight(hot<0?0:(hot+1)%rows.Count);e.Handled=true;}
            else if(e.Key==Key.Up){Highlight(hot<=0?rows.Count-1:hot-1);e.Handled=true;}
            else if(e.Key==Key.Enter||e.Key==Key.Space){if(hot>=0)Choose(hot);e.Handled=true;}
        }
        // Where the menu goes: above the pointer when the tray is at the bottom, always fully on the screen.
        internal static System.Drawing.Point Place(System.Drawing.Point pointer,System.Drawing.Size size,System.Drawing.Rectangle work)
        {
            int x=Math.Min(pointer.X,work.Right-size.Width-6);
            x=Math.Max(work.Left+6,x);
            int y=pointer.Y<work.Top?work.Top+6:Math.Min(pointer.Y-size.Height+10,work.Bottom-size.Height-6);
            y=Math.Max(work.Top+6,y);
            return new System.Drawing.Point(x,y);
        }
        // Shows the menu next to the pointer, in pixels of the screen the pointer is on.
        public void ShowAt(System.Drawing.Point pointer)
        {
            Show();
            var handle=new WindowInteropHelper(this).Handle;
            uint dpi=handle==IntPtr.Zero?96:GetDpiForWindow(handle);
            double scale=dpi<=0?1:dpi/96d;
            UpdateLayout();
            var size=new System.Drawing.Size((int)Math.Ceiling(ActualWidth*scale),(int)Math.Ceiling(ActualHeight*scale));
            var work=Forms.Screen.FromPoint(pointer).WorkingArea;
            var at=Place(pointer,size,work);
            SetWindowPos(handle,new IntPtr(-1),at.X,at.Y,0,0,0x0001|0x0010);
        }
    }

    internal sealed partial class Shell
    {
        void SetupTray()
        {
            tray=new Forms.NotifyIcon {Text="Lucent",Icon=new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Lucent.ico")),Visible=true};
            tray.MouseUp+=(s,e)=>{if(e.Button==Forms.MouseButtons.Right)ShowTrayMenu();};
            tray.DoubleClick+=(s,e)=>Open(page);
            Microsoft.Win32.SystemEvents.SessionEnding+=OnSessionEnding;
        }
        internal List<TrayEntry> TrayEntries()
        {
            return new List<TrayEntry>
            {
                TrayEntry.Line("Home","Open Lucent",()=>Open("Home")),
                TrayEntry.Divider(),
                TrayEntry.Line("Video","Color",()=>Open("Video")),
                TrayEntry.Line("Resolution","Display",()=>Open("Resolution")),
                TrayEntry.Line("Profiles","Profiles",()=>Open("Profiles")),
                TrayEntry.Line("Crosshair","Crosshair",()=>Open("Crosshair"),crosshair==null?null:"On"),
                TrayEntry.Line("Game Boost","Boost",()=>Open("Game Boost")),
                TrayEntry.Line("Visual tools","Vision",()=>Open("Visual tools")),
                TrayEntry.Divider(),
                TrayEntry.Line("Settings","Settings",()=>Open("Settings")),
                TrayEntry.Divider(),
                TrayEntry.Line("Swap","Switch resolution",TogglePair,preferences.SwitchKey==0?null:BindText(preferences.SwitchKey)),
                TrayEntry.Divider(),
                TrayEntry.Line("Power","Close Lucent",ExitApp,null,true)
            };
        }
        void ShowTrayMenu()
        {
            if(trayPopup!=null){try{trayPopup.Close();}catch(Exception){}trayPopup=null;}
            var popup=new TrayPopup(CurrentModeText(),TrayEntries());
            trayPopup=popup;
            popup.Closed+=(s,e)=>{if(trayPopup==popup)trayPopup=null;};
            popup.ShowAt(Forms.Cursor.Position);
        }
    }
}
