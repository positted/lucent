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
    // The Visual tools page: Dark Boost and zoom, behind the game-rules notice.
    internal sealed partial class Shell
    {
        FrameworkElement DarkPage()
        {
            var stack=Page(TabTitle("Visual tools"),"Tools that help you see more in a game. Each one is separate and off until you switch it on.");
            var warning=Section("Game rules may prohibit these tools","Dark-scene enhancement, zoom, color highlights and the sound radar can create an advantage. No anti-cheat or ban-safety guarantee is made. Keep these off where prohibited.");
            stack.Children.Add(warning);
            // A warning sign before the notice's title.
            var notice=warning.Content.Children[0] as FrameworkElement;
            if(notice!=null)
            {
                warning.Content.Children.RemoveAt(0);
                // The title's own spacing moves to the row, so the sign is centered on the words themselves (and lifted a touch to their capital letters).
                var heading=new StackPanel {Orientation=Orientation.Horizontal,Margin=notice.Margin};
                notice.Margin=new Thickness(0);
                var sign=Design.Icon("Warning",20);sign.Margin=new Thickness(0,0,9,0);sign.VerticalAlignment=VerticalAlignment.Center;sign.RenderTransform=new TranslateTransform(0,-1.5);
                notice.VerticalAlignment=VerticalAlignment.Center;
                heading.Children.Add(sign);heading.Children.Add(notice);
                warning.Content.Children.Insert(0,heading);
            }
            warning.Children.Add(Toggle("I have reviewed the rules for my game",preferences.RiskAccepted,v=>
            {
                preferences.RiskAccepted=v;
                if(!v)
                {
                    current.DarkEnabled=false;
                    darkBoostToggle.IsChecked=false;
                    zoomed=false;
                    if(activeProfile=="Desktop")preferences.Desktop=current.Copy();
                    UpdateEffects();
                }
                SaveSoon();
            }));
            // Zoom and the radar on the left, the two ways of seeing in the dark on the right.
            var left=new StackPanel();
            var right=new StackPanel();
            stack.Children.Add(ResponsiveColumns(left,right,1.1));

            // ---- Zoom: how it starts (Hold or Toggle, and one bind), how it is sharpened, and a safe way to try it. The mouse wheel sets how far.
            var zoom=Section("Zoom","Magnifies your primary display while you play, easing in and out. While zoom is on, the mouse wheel zooms in and out.");
            left.Children.Add(zoom);
            var modeRow=new WrapPanel();
            foreach(string name in new[]{"Hold","Toggle"})
            {
                string chosen=name;
                var chip=Action(name,()=>SetZoomMode(chosen=="Hold"));
                chip.Tag=name;chip.Padding=new Thickness(22,8,22,8);chip.FontSize=13;chip.MinHeight=0;chip.Margin=new Thickness(0,0,6,6);
                StyleChoice(chip,name==(preferences.HoldMouse5Zoom?"Hold":"Toggle"));
                zoomModeChoices.Add(chip);
                modeRow.Children.Add(chip);
            }
            zoom.Children.Add(modeRow);
            AddZoomBind(zoom,"Zoom bind");
            var how=Text("Hold zooms while the button is down. Toggle zooms on the first press and off on the next. The bind can be a key or a mouse button. Esc turns zoom off.",12);
            how.Foreground=Muted;
            zoom.Children.Add(how);
            zoom.Children.Add(Step("Sharpening"));
            zoom.Children.Add(Choices(new[]{"Adaptive","Manual","Off"},()=>current.ZoomSharp,chosen=>current.ZoomSharp=chosen,zoomSharpChoices));
            var sharpNote=Text("Adaptive sharpens edges more the further you zoom, and leaves noise and edges that are already sharp alone. Manual uses the slider. Off leaves the magnified picture soft.",12);
            sharpNote.Foreground=Muted;
            zoom.Children.Add(sharpNote);
            zoomManualBox=new StackPanel {Visibility=current.ZoomSharp=="Manual"?Visibility.Visible:Visibility.Collapsed};
            AddSlider(zoomManualBox,"Sharpness",0,100,current.ZoomSharpness,v=>current.ZoomSharpness=v,"%");
            zoom.Children.Add(zoomManualBox);
            var tryRow=new WrapPanel {Margin=new Thickness(0,14,0,0)};
            tryRow.Children.Add(Action("Try zoom for 5 seconds",TryZoom));
            zoom.Children.Add(tryRow);
            var tryNote=Text("How far it zooms and how it is sharpened are saved with the look in use, so a game profile can have its own.",12);
            tryNote.Foreground=Muted;
            zoom.Children.Add(tryNote);
            // ---- Sound radar
            var radarBox=Section("Sound radar","Shows which side a loud sound is coming from, as glowing arcs around the middle of the screen. It reads only how loud each speaker is, from all sound your PC plays (game, voice chat, music). It does not hear or record anything.");
            left.Children.Add(radarBox);
            radarToggle=Toggle("Show where loud sounds come from",current.RadarOn,v=>
            {
                if(v&&!preferences.RiskAccepted)throw new InvalidOperationException("Review the game-rule notice first.");
                current.RadarOn=v;
                Changed();
            });
            radarBox.Children.Add(radarToggle);
            AddSlider(radarBox,"Radar sensitivity",0,100,current.RadarSensitivity,v=>current.RadarSensitivity=v,"%");
            radarBox.Children.Add(Step("Size"));
            radarBox.Children.Add(Choices(new[]{"Small","Medium","Large"},()=>current.RadarSize,chosen=>current.RadarSize=chosen,radarSizeChoices));
            radarBox.Children.Add(Step("Place"));
            radarBox.Children.Add(Choices(new[]{"Center","Top","Bottom"},()=>current.RadarPlace,chosen=>current.RadarPlace=chosen,radarPlaceChoices));

            // ---- Dark Boost
            var box=Section("Dark Boost","Lifts dark scenes. Separate from closing apps in the Boost tab.");
            right.Children.Add(box);
            darkBoostToggle=Toggle("Enable Dark Boost",current.DarkEnabled,v=>
            {
                if(v&&!preferences.RiskAccepted)throw new InvalidOperationException("Review the game-rule notice first.");
                current.DarkEnabled=v;
                Changed();
            });
            box.Children.Add(darkBoostToggle);
            BindRow(box,"Dark Boost toggle",()=>preferences.DarkKey,v=>preferences.DarkKey=v,DarkBindPressed);
            var darkHint=Text("Tap the shortcut to switch Dark Boost on or off. Hold it and turn the mouse wheel to change the Strength (up for more).",12);
            darkHint.Foreground=Muted;
            box.Children.Add(darkHint);
            box.Children.Add(Step("Mode"));
            box.Children.Add(Choices(new[]{"Fast","Smart shadows"},()=>current.DarkSmart?"Smart shadows":"Fast",chosen=>
            {
                if(chosen=="Smart shadows"&&!preferences.RiskAccepted)throw new InvalidOperationException("Review the game-rule notice first.");
                current.DarkSmart=chosen=="Smart shadows";
            },darkModeChoices));
            var modes=Text("Fast brightens through the display with no delay. Smart shadows open up the dark parts and leave bright areas alone, but draw through the GPU overlay and add about a frame of delay.",12);
            modes.Foreground=Muted;
            box.Children.Add(modes);
            box.Children.Add(Step("Tint"));
            var colors=new WrapPanel();
            foreach(string name in new[]{"Green","Amber","Cyan","White"})
            {
                string chosen=name;
                var chip=Action(name,()=>
                {
                    current.DarkColor=chosen;
                    UpdateLookChoices();
                    Changed();
                });
                chip.Tag=name;
                StyleChoice(chip,name==current.DarkColor);
                darkColorChoices.Add(chip);
                colors.Children.Add(chip);
            }
            box.Children.Add(colors);
            AddSlider(box,"Strength",0,100,current.DarkStrength,v=>current.DarkStrength=v,"%");
            AddSlider(box,"Black level",0,100,current.BlackLevel,v=>current.BlackLevel=v,"%");

            // ---- Color pop
            var pop=Section("Color pop","Makes one color stand out and calms the rest, so a colored outline, item or ability is easier to spot. Drawn by the GPU overlay, like Smart shadows. It does not sharpen anything.");
            right.Children.Add(pop);
            var popRow=new WrapPanel();
            foreach(string name in new[]{"Off","Red","Orange","Yellow","Green","Cyan","Blue","Magenta"})
            {
                string chosen=name;
                var chip=Action(name,()=>
                {
                    if(chosen!="Off"&&!preferences.RiskAccepted){Message("Review the game-rule notice first.");return;}
                    current.PopColor=chosen;
                    UpdateLookChoices();
                    Changed();
                });
                chip.Tag=name;chip.Padding=new Thickness(11,8,11,8);chip.FontSize=12;chip.MinHeight=0;chip.Margin=new Thickness(0,0,6,6);
                StyleChoice(chip,name==current.PopColor);
                popChoices.Add(chip);
                popRow.Children.Add(chip);
            }
            pop.Children.Add(popRow);
            AddSlider(pop,"Pop strength",0,100,current.PopStrength,v=>current.PopStrength=v,"%");
            return stack;
        }
        // A small heading inside a card.
        static TextBlock Step(string text)
        {
            var label=new TextBlock {Text=text,FontSize=12,FontWeight=FontWeights.Bold,Foreground=Muted,Margin=new Thickness(0,16,0,6)};
            return label;
        }
        // A row of choices where exactly one is picked. The list keeps them so loading a look can mark the right one.
        FrameworkElement Choices(string[] names,Func<string> get,Action<string> set,List<Button> into)
        {
            var row=new WrapPanel();
            foreach(string name in names)
            {
                string chosen=name;
                var chip=Action(name,()=>
                {
                    set(chosen);
                    UpdateLookChoices();
                    Changed();
                });
                chip.Tag=name;chip.Padding=new Thickness(11,8,11,8);chip.FontSize=12;chip.MinHeight=0;chip.Margin=new Thickness(0,0,6,6);
                StyleChoice(chip,name==get());
                into.Add(chip);
                row.Children.Add(chip);
            }
            return row;
        }
        // Zoom for a few seconds from the button, to see what it does. It ends by itself, or with Esc.
        readonly DispatcherTimer tryZoomTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};
        void TryZoom()
        {
            if(!preferences.RiskAccepted)
            {
                Message("Review the game-rule notice in Vision first.");
                return;
            }
            zoomFromButton=true;
            StartZoomLevel();zoomed=true;Count(c=>c.ZoomUses++);
            UpdateEffects();
            if(TestMode)return;
            tryZoomTimer.Tick-=OnTryZoomEnd;tryZoomTimer.Tick+=OnTryZoomEnd;
            tryZoomTimer.Stop();tryZoomTimer.Start();
        }
        void OnTryZoomEnd(object sender,EventArgs e){EndTryZoom();}
        internal void EndTryZoom()
        {
            tryZoomTimer.Stop();
            if(!zoomFromButton||!zoomed)return;
            zoomed=false;zoomFromButton=false;
            UpdateEffects();
        }        CheckBox radarToggle;
        StackPanel zoomManualBox;
        // The mode chips: Hold or Toggle. This is a preference, not part of a look.
        void SetZoomMode(bool hold)
        {
            preferences.HoldMouse5Zoom=hold;
            zoomHoldActive=false;
            if(zoomed&&!zoomFromButton){zoomed=false;UpdateEffects();}
            foreach(var chip in zoomModeChoices)StyleChoice(chip,Equals(chip.Tag,hold?"Hold":"Toggle"));
            SaveSoon();
        }
        readonly List<Button> popChoices=new List<Button>(),radarSizeChoices=new List<Button>(),radarPlaceChoices=new List<Button>(),zoomSharpChoices=new List<Button>(),zoomModeChoices=new List<Button>(),darkModeChoices=new List<Button>();
        // The same switch as the checkbox, for a shortcut pressed in a game.
        void ToggleDarkBoost()
        {
            if(!preferences.RiskAccepted)
            {
                Message("Review the game-rule notice in Vision first.");
                return;
            }
            current.DarkEnabled=!current.DarkEnabled;
            if(darkBoostToggle!=null)darkBoostToggle.IsChecked=current.DarkEnabled;
            Changed();
        }
        // ---- Dark Boost: the toggle shortcut held down, plus the mouse wheel, changes the strength (up for more). A quick tap with no wheel still switches it on or off,
        // so the shortcut now acts when it is let go. The wheel is only read, and kept from scrolling the game, while the shortcut is held.
        ZoomWheelHook darkWheel;
        bool darkHold,darkWheelUsed;
        System.Threading.Timer darkReleaseTimer;
        const double DarkWheelStep=5;
        void DarkBindPressed()
        {
            if(darkHold)return;
            if(TestMode||!preferences.RiskAccepted){ToggleDarkBoost();return;}
            darkWheelUsed=false;
            darkWheel=new ZoomWheelHook(delta=>DarkWheel(delta));
            if(!darkWheel.Start()){darkWheel=null;ToggleDarkBoost();return;}
            darkHold=true;
            // Watched from a thread of its own, so the release is seen at once even while the app is busy.
            darkReleaseTimer=new System.Threading.Timer(state=>
            {
                if(IsHoldBindingDown(preferences.DarkKey))return;
                var timer=darkReleaseTimer;darkReleaseTimer=null;if(timer!=null)timer.Dispose();
                try{Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send,new Action(DarkBindReleased));}catch(Exception){}
            },null,0,4);
        }
        void DarkBindReleased()
        {
            if(!darkHold)return;
            darkHold=false;
            if(darkWheel!=null){darkWheel.Dispose();darkWheel=null;}
            if(!darkWheelUsed)ToggleDarkBoost();
        }
        internal void DisposeDarkHold()
        {
            var timer=darkReleaseTimer;darkReleaseTimer=null;if(timer!=null)timer.Dispose();
            if(darkWheel!=null){darkWheel.Dispose();darkWheel=null;}
            darkHold=false;
        }
        // One notch is 5 percent. Turning the wheel also switches Dark Boost on if it was off: it would otherwise change a strength nobody can see.
        internal void DarkWheel(int delta)
        {
            darkWheelUsed=true;
            double next=Math.Max(0,Math.Min(100,current.DarkStrength+DarkWheelStep*(delta/120d)));
            if(!current.DarkEnabled){current.DarkEnabled=true;if(darkBoostToggle!=null)darkBoostToggle.IsChecked=true;}
            Slider slider;
            // The slider carries the value to the look, the shown number and the screen.
            if(sliders.TryGetValue("Strength",out slider)&&Math.Abs(slider.Value-current.DarkStrength)<.5)slider.Value=next;
            else{current.DarkStrength=next;Changed();}
        }
        bool zoomFromButton;
        void ToggleZoom()
        {
            if(!preferences.RiskAccepted)
            {
                Message("Review the game-rule notice in Vision first.");
                return;
            }
            zoomFromButton=false;
            zoomed=!zoomed;
            if(zoomed){StartZoomLevel();Count(c=>c.ZoomUses++);}
            if(!zoomed)zoomFromButton=false;
            UpdateEffects();
        }
    }
}
