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
    // Sends the current look to the color worker and the zoom overlay.
    internal sealed partial class Shell
    {
        void Changed()
        {
            if(building)return;
            applyLook=true;
            if(activeProfile=="Desktop")preferences.Desktop=current.Copy();
            SaveSoon();
            if(!effectTimer.IsEnabled)effectTimer.Start();
        }
        void SaveSoon(){if(building||TestMode)return;saveTimer.Stop();saveTimer.Start();}
        // The look that is applied now: the current one, or only the zoom and color-pop settings while comparing with the desktop.
        internal Look EffectiveLook()
        {
            var look=applyLook&&!comparing?current.Copy():new Look {Zoom=current.Zoom,ZoomSharp=current.ZoomSharp,ZoomSharpness=current.ZoomSharpness,PopColor=current.PopColor,PopStrength=current.PopStrength};
            look.DarkEnabled=look.DarkEnabled&&preferences.RiskAccepted;
            return look;
        }
        // What the GPU overlay should draw. Sharpening belongs to zoom alone, so Color pop and Smart shadows never sharpen the picture.
        internal ZoomRequest BuildZoomRequest(Look look)
        {
            bool activeZoom=zoomed&&!comparing,smart=look.DarkEnabled&&look.DarkSmart;
            return new ZoomRequest
            {
                Zoom=activeZoom?look.Zoom:1,Follow=false,HideCursor=!zoomFromButton,
                // Adaptive sharpening has a fixed base amount and lets the shader decide where and how much; Manual is the slider; Off is none.
                Sharpness=!activeZoom||look.ZoomSharp=="Off"?0:look.ZoomSharp=="Manual"?look.ZoomSharpness/100:.6,Adaptive=look.ZoomSharp=="Adaptive",
                Lift=smart?look.DarkStrength/100:0,Black=smart?look.BlackLevel/100:0,Tint=TintFor(look.DarkColor),
                // Color pop is not part of the "compare with desktop" look: it is on whenever it is chosen and the rules were reviewed.
                PopHue=preferences.RiskAccepted&&!comparing?PopHueFor(current.PopColor):-1,PopStrength=current.PopStrength/100
            };
        }
        void UpdateEffects()
        {
            if(TestMode||closing)return;
            var look=EffectiveLook();
            UpdateGpuZoom(BuildZoomRequest(look));
            UpdateZoomWheel(zoomed&&!comparing&&preferences.RiskAccepted);
            UpdateRadar();
            bool hasColor=!ColorMath.Equal(ColorMath.Build(look),ColorMath.Identity());
            if(effects==null)
            {
                if(!hasColor)return;
                var client=new EffectsClient();
                effects=client;
                client.Status=message=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(effects==client)SetStatus(message);
                }));
                client.Error=message=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(effects!=client)return;
                    DisableEffects();
                    // A leftover record from a session that did not end cleanly is put right by itself, and the colors are asked for again.
                    if(message.IndexOf("recovery record",StringComparison.OrdinalIgnoreCase)>=0&&RecoverColorsQuietly()){applyLook=true;effectTimer.Start();return;}
                    SetStatus(message);
                }));
            }
            effects.Set(look,false);
        }
        // The color worker writes a recovery record before it changes the screen colors and removes it when it has put them back. If Lucent was
        // closed from outside or crashed in between, the record stays. Recovering only puts the colors back if nobody changed them since, so it
        // is safe to do by itself; it is tried at start-up and when turning colors on finds a record, but never more than once a minute.
        DateTime lastQuietRecovery=DateTime.MinValue;
        internal bool RecoverColorsQuietly()
        {
            if(TestMode||DateTime.UtcNow-lastQuietRecovery<TimeSpan.FromMinutes(1))return false;
            lastQuietRecovery=DateTime.UtcNow;
            if(!File.Exists(Path.Combine(StateStore.Folder,"software-recovery.xml")))return true;
            try{NativeEffects.Recover();return !File.Exists(Path.Combine(StateStore.Folder,"software-recovery.xml"));}
            catch(Exception){return false;}
        }
        DesktopGpuZoom gpuZoom;
        ZoomWheelHook zoomWheel;
        // While zoom is on, the mouse wheel changes how far it zooms, and does not scroll whatever is under it. The hook only exists while zoom is on.
        void UpdateZoomWheel(bool on)
        {
            if(on&&zoomWheel==null)
            {
                zoomWheel=new ZoomWheelHook(delta=>ZoomBy(delta/120d));
                if(!zoomWheel.Start()){zoomWheel=null;SetStatus("Windows would not let Lucent read the mouse wheel for zoom.");}
            }
            else if(!on&&zoomWheel!=null){zoomWheel.Dispose();zoomWheel=null;}
        }
        // One wheel notch zooms by about a fifth. The new level goes straight to the overlay, which eases toward it within a frame or two,
        // so the zoom follows the wheel as it turns instead of a moment after. Saving the look can wait.
        const double WheelStep=1.2;
        // Every time zoom starts it starts from 1.5x, whatever the wheel last made it: how far you zoomed in last time is not kept.
        internal const double StartZoom=1.5;
        void StartZoomLevel()
        {
            current.Zoom=StartZoom;
            if(activeProfile=="Desktop")preferences.Desktop=current.Copy();
            SaveSoon();
        }
        internal void ZoomBy(double notches)
        {
            double level=Math.Max(Look.MinimumZoom,Math.Min(Look.MaximumZoom,current.Zoom*Math.Pow(WheelStep,notches)));
            if(Math.Abs(level-current.Zoom)<.0005)return;
            current.Zoom=level;
            if(activeProfile=="Desktop")preferences.Desktop=current.Copy();
            SaveSoon();
            if(gpuZoom!=null&&!TestMode)gpuZoom.Apply(BuildZoomRequest(EffectiveLook()));
        }
        // The Dark Boost colors as per-channel multipliers, the same ones the display matrix uses.
        internal static float[] TintFor(string color)
        {
            switch(color)
            {
                case "Green":return new[]{.12f,1f,.12f};
                case "Amber":return new[]{1f,.65f,.08f};
                case "Cyan":return new[]{.1f,1f,1f};
                default:return new[]{1f,1f,1f};
            }
        }
        internal static double PopHueFor(string color){return ColorPop.HueFor(color);}
        SoundRadar radar;
        // The sound radar follows the look that is in use, so a game profile can turn it on for that game alone.
        void UpdateRadar()
        {
            bool want=current.RadarOn&&preferences.RiskAccepted&&!comparing;
            if(!want)
            {
                if(radar!=null){radar.Dispose();radar=null;}
                return;
            }
            if(radar==null)
            {
                var created=new SoundRadar();
                radar=created;
                created.Failed+=message=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(radar!=created)return;
                    radar=null;created.Dispose();
                    current.RadarOn=false;
                    if(radarToggle!=null)radarToggle.IsChecked=false;
                    SetStatus(message);
                }));
            }
            radar.Apply(new RadarOptions {Sensitivity=current.RadarSensitivity,Size=current.RadarSize,Place=current.RadarPlace});
        }
        void UpdateGpuZoom(double zoomFactor){UpdateGpuZoom(new ZoomRequest {Zoom=zoomFactor});}
        void UpdateGpuZoom(ZoomRequest request)
        {
            if(!request.Wanted)
            {
                if(gpuZoom!=null)gpuZoom.Apply(request);
                return;
            }
            try
            {
                if(gpuZoom==null)
                {
                    var created=new DesktopGpuZoom();
                    gpuZoom=created;
                    created.Failed+=message=>Dispatcher.BeginInvoke(new Action(()=>{
                        if(gpuZoom==created){zoomed=false;zoomHoldActive=false;gpuZoom=null;created.Dispose();Message(message);}
                    }));
                }
                gpuZoom.Apply(request);
            }
            catch(Exception error)
            {
                UpdateGpuZoom(1);
                SetStatus(error.Message);
            }
        }
        void DisableEffects()
        {
            applyLook=false;
            zoomed=false;

            UpdateGpuZoom(1);

            if(gpuZoom!=null){gpuZoom.Dispose();gpuZoom=null;}
            if(radar!=null){radar.Dispose();radar=null;}
            UpdateZoomWheel(false);
            var client=effects;
            effects=null;
            if(client!=null)client.Dispose();
        }
    }
}
