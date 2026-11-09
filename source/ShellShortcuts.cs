using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shell;
using Forms=System.Windows.Forms;
using ResolutionSwitcher;
using System.Windows.Media.Imaging;

namespace Lucent
{
    // Global shortcuts and the fields that capture them.
    internal sealed partial class Shell
    {
        readonly Dictionary<string,HotkeyWindow> bindings=new Dictionary<string,HotkeyWindow>();
        readonly Dictionary<string,List<Button>> shortcutFields=new Dictionary<string,List<Button>>();
        void BindRow(StackPanel panel,string name,Func<int> get,Action<int> set,Action execute,string caption=null)
        {
            var row=new DockPanel {Margin=new Thickness(0,6,0,6)};
            var label=Text(caption??name,13);
            label.VerticalAlignment=VerticalAlignment.Center;
            var field=Action(BindText(get()),()=>{});
            System.Windows.Automation.AutomationProperties.SetName(field,name+" shortcut");
            // The same shortcut can appear on two pages; both must show the current key.
            List<Button> shown;
            if(!shortcutFields.TryGetValue(name,out shown))shortcutFields[name]=shown=new List<Button>();
            shown.Add(field);
            Action showAll=()=>{foreach(var other in shown)other.Content=BindText(get());};
            DockPanel.SetDock(field,Dock.Right);
            row.Children.Add(field);
            row.Children.Add(label);

            bool capturing=false;
            field.Click+=(s,e)=>{capturing=true;field.Content="Press key or Esc to clear…";field.Focus();};
            field.LostKeyboardFocus+=(s,e)=>{capturing=false;field.Content=BindText(get());};
            field.PreviewKeyDown+=(s,e)=>
            {
                if(!capturing)return;
                e.Handled=true;
                if(e.Key==Key.Escape)
                {
                    HotkeyWindow existing;
                    if(bindings.TryGetValue(name,out existing))
                    {
                        existing.Dispose();
                        bindings.Remove(name);
                    }
                    set(0);
                    capturing=false;
                    showAll();
                    SaveSoon();
                    return;
                }
                var key=e.Key==Key.System?e.SystemKey:e.Key;
                if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift)return;

                int value=KeyInterop.VirtualKeyFromKey(key);
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))value|=(int)Forms.Keys.Control;
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))value|=(int)Forms.Keys.Alt;
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))value|=(int)Forms.Keys.Shift;
                if(TestMode)
                {
                    set(value);
                    SaveSoon();
                    capturing=false;
                    showAll();
                    return;
                }
                HotkeyWindow hotkey;
                if(!bindings.TryGetValue(name,out hotkey))
                {
                    hotkey=new HotkeyWindow(execute);
                    bindings[name]=hotkey;
                }
                if(!hotkey.Register((Forms.Keys)value))
                {
                    field.Content="Key unavailable";
                    return;
                }
                set(value);
                SaveSoon();
                capturing=false;
                showAll();
            };(panel is SectionPanel?((SectionPanel)panel).Content:panel).Children.Add(row);
        }
        bool capturingZoomHold,waitHoldRelease,zoomHoldActive;
        static bool IsHoldBindingDown(int binding)
        {
            int key=binding&0xffff;
            if(key==0||(GetAsyncKeyState(key)&0x8000)==0)return false;
            if((binding&(int)Forms.Keys.Control)!=0&&(GetAsyncKeyState(17)&0x8000)==0)return false;
            if((binding&(int)Forms.Keys.Shift)!=0&&(GetAsyncKeyState(16)&0x8000)==0)return false;
            if((binding&(int)Forms.Keys.Alt)!=0&&(GetAsyncKeyState(18)&0x8000)==0)return false;
            return true;
        }
        // The zoom bind: one button that takes a key or a mouse button. Where it appears twice, both show the same bind.
        void AddZoomBind(StackPanel panel,string name)
        {
            var row=new DockPanel {Margin=new Thickness(0,6,0,6)};
            var field=Action(BindText(preferences.ZoomBind),()=>{});
            List<Button> shown;
            if(!shortcutFields.TryGetValue(name,out shown))shortcutFields[name]=shown=new List<Button>();
            shown.Add(field);
            DockPanel.SetDock(field,Dock.Right);
            row.Children.Add(field);
            var label=Text(name,13);
            label.VerticalAlignment=VerticalAlignment.Center;
            row.Children.Add(label);
            Action<int> assign=key=>
            {
                preferences.ZoomBind=key;
                capturingZoomHold=false;waitHoldRelease=true;
                foreach(var other in shown)other.Content=BindText(key);
                StartZoomPolling();
                SaveSoon();
            };
            field.Click+=(sender,args)=>
            {
                capturingZoomHold=true;zoomHoldActive=false;zoomed=false;
                UpdateEffects();field.Content="Press a key or mouse button, Esc to clear\u2026";field.Focus();
            };
            field.PreviewKeyDown+=(sender,args)=>
            {
                if(!capturingZoomHold)return;
                args.Handled=true;
                var key=args.Key==Key.System?args.SystemKey:args.Key;
                if(key==Key.Escape){assign(0);return;}
                if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift)return;
                int binding=KeyInterop.VirtualKeyFromKey(key);
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))binding|=(int)Forms.Keys.Control;
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))binding|=(int)Forms.Keys.Alt;
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))binding|=(int)Forms.Keys.Shift;
                assign(binding);
            };
            field.PreviewMouseDown+=(sender,args)=>
            {
                if(!capturingZoomHold)return;
                int key=0;
                switch(args.ChangedButton)
                {
                    case MouseButton.Left:key=1;break;
                    case MouseButton.Right:key=2;break;
                    case MouseButton.Middle:key=4;break;
                    case MouseButton.XButton1:key=5;break;
                    case MouseButton.XButton2:key=6;break;
                }
                args.Handled=true;assign(key);
            };
            field.LostKeyboardFocus+=(sender,args)=>{capturingZoomHold=false;field.Content=BindText(preferences.ZoomBind);};
            System.Windows.Automation.AutomationProperties.SetName(field,name);
            (panel is SectionPanel?((SectionPanel)panel).Content:panel).Children.Add(row);
        }
        // The bind is watched by polling, so it works for mouse buttons as well as keys: Hold zooms while it is down, Toggle on each new press.
        bool zoomBindWasDown;
        // The bind is also watched from a thread of its own, every few milliseconds, and the app's own thread is told the moment the key goes down or up.
        // The timer on the app's own thread only runs when that thread is free, so while the app was busy (drawing, animating, a game taking the PC) a press
        // could wait a noticeable time before zoom started.
        System.Threading.Timer zoomEdgeTimer;bool zoomEdgeDown;
        void StartZoomPolling()
        {
            if(TestMode||preferences.ZoomBind==0)return;
            if(!zoomHoldTimer.IsEnabled)zoomHoldTimer.Start();
            if(zoomEdgeTimer!=null)return;
            zoomEdgeTimer=new System.Threading.Timer(state=>
            {
                int bind=preferences.ZoomBind;
                if(bind==0)return;
                bool down=IsHoldBindingDown(bind);
                if(down==zoomEdgeDown)return;
                zoomEdgeDown=down;
                try{Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send,new Action(PollZoomBind));}catch(Exception){}
            },null,0,4);
        }
        void PollZoomBind()
        {
            if(capturingZoomHold||preferences.ZoomBind==0)return;
            bool down=IsHoldBindingDown(preferences.ZoomBind);
            if(waitHoldRelease){if(!down)waitHoldRelease=false;zoomBindWasDown=down;return;}
            if(preferences.HoldMouse5Zoom)
            {
                if(!preferences.RiskAccepted)return;
                if(down&&!zoomHoldActive){zoomHoldActive=true;zoomFromButton=false;StartZoomLevel();zoomed=true;Count(c=>c.ZoomUses++);UpdateEffects();}
                else if(!down&&zoomHoldActive){zoomHoldActive=false;zoomed=false;UpdateEffects();}
            }
            else if(down&&!zoomBindWasDown)ToggleZoom();
            zoomBindWasDown=down;
        }        string BindText(int key)
        {
            if(key==1)return "Left mouse";
            if(key==2)return "Right mouse";
            if(key==4)return "Middle mouse";
            if(key==5)return "Mouse 4";
            if(key==6)return "Mouse 5";
            return key==0?"Unbound":new Forms.KeysConverter().ConvertToString((Forms.Keys)key);
        }
        void SendMedia(byte key){if(!TestMode)DesktopTools.Media(key);}
        void RegisterKeys()
        {
            Register("Switch resolution",preferences.SwitchKey,TogglePair);
            Register("Toggle crosshair",preferences.CrosshairKey,ToggleCrosshair);
            Register("Dark Boost toggle",preferences.DarkKey,DarkBindPressed);
            Register("Music mini player",preferences.MediaKey,MediaPopup);
            Register("Play / pause",preferences.PlayKey,()=>SendMedia(0xB3));
            Register("Next track",preferences.NextKey,()=>SendMedia(0xB0));
            Register("Previous track",preferences.PreviousKey,()=>SendMedia(0xB1));
        }
        void Register(string name,int key,Action execute)
        {
            if(key==0)return;
            var window=new HotkeyWindow(execute);
            bindings[name]=window;
            if(!window.Register((Forms.Keys)key))SetStatus(name+" shortcut unavailable; change it in Settings.");
        }
    }
}
