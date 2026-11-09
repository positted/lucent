using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBlock=Lucent.SmoothText;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lucent
{
    // The right side of the Color page: one-click looks, and gentle reminders to rest your eyes.
    internal sealed partial class Shell
    {
        sealed class ColorPreset
        {
            public string Name,Detail;
            public double Vibrance,Red,Dim;
        }
        static readonly ColorPreset[] ColorPresets=
        {
            new ColorPreset {Name="Neutral",Detail="No changes",Vibrance=100,Red=0,Dim=0},
            new ColorPreset {Name="Vivid",Detail="Richer colors",Vibrance=170,Red=0,Dim=0},
            new ColorPreset {Name="Warm evening",Detail="Softer, warmer light",Vibrance=105,Red=32,Dim=0},
            new ColorPreset {Name="Night",Detail="Dim and warm",Vibrance=100,Red=62,Dim=35},
            new ColorPreset {Name="Reading",Detail="Easy on the eyes",Vibrance=100,Red=45,Dim=12}
        };
        FrameworkElement PresetsCard()
        {
            var card=VideoCard("Quick looks","Vibrance");
            var note=Text("Sets Vibrance, Red Filter and Screen Dim in one click. Adjust them after if you like.",12);
            note.Foreground=Muted;
            card.Children.Add(note);
            var chips=new WrapPanel {Margin=new Thickness(0,4,0,0)};
            foreach(var preset in ColorPresets)
            {
                var chosen=preset;
                var chip=Action(preset.Name,()=>ApplyColorPreset(chosen));
                chip.Padding=new Thickness(12,9,12,9);chip.FontSize=13;chip.MinHeight=0;chip.Margin=new Thickness(0,0,7,7);
                chip.ToolTip=preset.Detail;
                chip.Tag=preset.Name;
                chips.Children.Add(chip);
            }
            card.Children.Add(chips);
            return card;
        }
        // Moving the sliders is what applies a look, so a preset goes through them like the user would.
        void ApplyColorPreset(ColorPreset preset)
        {
            Slider slider;
            if(sliders.TryGetValue("Vibrance",out slider))slider.Value=preset.Vibrance;
            if(sliders.TryGetValue("Red Filter",out slider))slider.Value=preset.Red;
            if(sliders.TryGetValue("Screen Dim",out slider))slider.Value=preset.Dim;
            SetStatus(preset.Name+" look applied");
        }

        // Break reminders. One minute at a time, so changing the interval never needs a new timer.
        readonly DispatcherTimer breakTimer=new DispatcherTimer {Interval=TimeSpan.FromMinutes(1)};
        int breakElapsed;
        TextBlock breakNext;
        readonly List<Button> breakChoices=new List<Button>();
        FrameworkElement BreakCard()
        {
            breakChoices.Clear();
            var card=VideoCard("Eye breaks","EyeBreak");
            var note=Text("A reminder to look at something far away for 20 seconds, which rests your eyes. It stays quiet while a game profile is in use.",12);
            note.Foreground=Muted;
            card.Children.Add(note);
            card.Children.Add(Toggle("Remind me to take breaks",preferences.BreakReminders,on=>
            {
                preferences.BreakReminders=on;
                breakElapsed=0;
                ShowBreakCountdown();
                SaveSoon();
            }));
            var row=new WrapPanel();
            foreach(int minutes in new[]{20,30,45,60})
            {
                int chosen=minutes;
                var chip=Action(minutes+" min",()=>
                {
                    preferences.BreakMinutes=chosen;
                    breakElapsed=0;
                    PaintBreakChoices();
                    ShowBreakCountdown();
                    SaveSoon();
                });
                chip.Tag=minutes;chip.Padding=new Thickness(11,8,11,8);chip.FontSize=12;chip.MinHeight=0;chip.Margin=new Thickness(0,0,6,6);
                breakChoices.Add(chip);
                row.Children.Add(chip);
            }
            card.Children.Add(row);
            breakNext=Text("",12);
            breakNext.Foreground=Muted;
            card.Children.Add(breakNext);
            // Shows the reminder right away, with its sound, so it is easy to see what it will look like.
            var test=Action("Test the reminder",ShowBreakNotice);
            test.Margin=new Thickness(0,12,0,0);
            card.Children.Add(test);
            PaintBreakChoices();
            ShowBreakCountdown();
            breakTimer.Tick-=OnBreakTick;breakTimer.Tick+=OnBreakTick;
            if(!TestMode&&!breakTimer.IsEnabled)breakTimer.Start();
            return card;
        }
        void PaintBreakChoices()
        {
            foreach(var chip in breakChoices)chip.Background=(int)chip.Tag==preferences.BreakMinutes?selectionSurface:optionSurface;
        }
        void ShowBreakCountdown()
        {
            if(breakNext==null)return;
            int left=Math.Max(1,preferences.BreakMinutes-breakElapsed);
            breakNext.Text=preferences.BreakReminders?"Next reminder in "+left+" min":"Reminders are off";
        }
        void OnBreakTick(object sender,EventArgs e){BreakTick();}
        // One minute has passed. Returns whether a reminder was shown, which the tests use.
        // The reminder itself, also what the Test button shows.
        internal void ShowBreakNotice()
        {
            ShowNotice("Time for an eye break","Look at something far away for 20 seconds. Your eyes will thank you.",6);
            if(!TestMode)NoticeChime.Play();
        }
        internal bool BreakTick()
        {
            if(!preferences.BreakReminders||closing)return false;
            // Time in a game does not count and never interrupts it.
            if(activeProfile!="Desktop"){breakElapsed=0;ShowBreakCountdown();return false;}
            breakElapsed++;
            if(breakElapsed<preferences.BreakMinutes){ShowBreakCountdown();return false;}
            breakElapsed=0;
            ShowBreakCountdown();
            ShowBreakNotice();
            if(!TestMode&&tray!=null)
            {
                try{tray.ShowBalloonTip(6000,"Lucent","Time for a short eye break. Look at something far away for 20 seconds.",System.Windows.Forms.ToolTipIcon.Info);}
                catch(Exception){}
            }
            return true;
        }
    }

    // A soft two-note chime for reminders that should be noticed. It plays the app''s own sound, quietly, and never anything the PC is doing.
    internal static class NoticeChime
    {
        static readonly System.IO.MemoryStream stream=Create();
        static readonly System.Media.SoundPlayer player=new System.Media.SoundPlayer(stream);
        internal static void Play(){try{player.Play();}catch(Exception){}}
        static System.IO.MemoryStream Create()
        {
            const int rate=44100,count=rate*7/10;
            var data=new System.IO.MemoryStream();var w=new System.IO.BinaryWriter(data);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
            for(int i=0;i<count;i++)
            {
                double t=i/(double)rate;
                double tone=Note(t,659.25,.5)+.9*Note(t-.16,987.77,.55)+.35*Note(t-.16,1975.5,.3);
                w.Write((short)(tone*3300));
            }
            data.Position=0;return data;
        }
        static double Note(double time,double frequency,double duration)
        {
            if(time<0||time>=duration)return 0;
            double envelope=Math.Min(1,time/.01)*Math.Pow(1-time/duration,2.2);
            return envelope*Math.Sin(2*Math.PI*frequency*time);
        }
    }}
