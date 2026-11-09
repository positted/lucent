using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lucent
{
    // A speaker button and a slider for the volume of the app that is playing, never the whole PC. Clicking the speaker mutes that app; dragging the slider sets its level.
    internal sealed class VolumeControl:StackPanel
    {
        readonly Button speaker=new Button();
        readonly SeekBar bar;
        readonly bool testMode;
        readonly double iconSize;
        readonly DispatcherTimer follow=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
        double level=.5;
        bool muted,available=true;
        string shownIcon;
        // The media source id of the app being controlled, such as "Spotify.exe". The owner supplies it from the current track.
        internal Func<string> Source=()=>null;
        internal bool Available {get{return available;}}
        internal double Level {get{return level;}}
        internal bool Muted {get{return muted;}}
        internal SeekBar Slider {get{return bar;}}
        internal Button Speaker {get{return speaker;}}

        public VolumeControl(bool testMode,double iconSize,double barWidth)
        {
            this.testMode=testMode;
            this.iconSize=iconSize;
            Orientation=Orientation.Horizontal;
            VerticalAlignment=VerticalAlignment.Center;
            PlayerCore.Frame(speaker,30);
            speaker.Margin=new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(speaker,"Mute");
            speaker.Click+=(s,e)=>{muted=!muted;Apply();Show();};
            PlayerCore.Motion(speaker,1.1,.9);
            bar=new SeekBar {Width=barWidth,Seekable=true,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center};
            System.Windows.Automation.AutomationProperties.SetName(bar,"Volume");
            // Dragging sets the volume as it goes, and moving it always unmutes.
            bar.Preview=fraction=>{level=fraction;muted=false;Apply();Show();};
            bar.Commit=bar.Preview;
            Children.Add(speaker);
            Children.Add(bar);
            follow.Tick+=(s,e)=>{if(!bar.Dragging)Read();};
            IsVisibleChanged+=(s,e)=>{if(IsVisible){Read();follow.Start();}else follow.Stop();};
            Show();
        }
        // The app's volume can change elsewhere, so the control follows it while it is on screen. It is only usable once the app is making sound.
        void Read()
        {
            if(testMode)return;
            float read;bool mute;
            bool found=AppVolume.TryGet(Source(),out read,out mute);
            SetAvailable(found);
            if(!found)return;
            level=read;muted=mute;
            Show();
        }
        void Apply()
        {
            if(testMode)return;
            string source=Source();
            AppVolume.TrySet(source,(float)level);
            AppVolume.TryMute(source,muted);
        }
        void SetAvailable(bool on)
        {
            if(on==available)return;
            available=on;
            bar.Seekable=on;
            Opacity=on?1:.45;
            ToolTip=on?null:"Volume for the app that is playing. It works once that app is making sound.";
        }
        void Show()
        {
            bar.SetFraction(muted?0:level);
            string icon=muted||level<=.001?"Mute":level<.5?"VolumeLow":"Volume";
            if(icon==shownIcon)return;
            shownIcon=icon;
            var glyph=Design.Icon(icon,iconSize);
            glyph.Opacity=speaker.IsMouseOver?1:PlayerCore.IdleInk;
            speaker.Content=glyph;
        }
    }
}
