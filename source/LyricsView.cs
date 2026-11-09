using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TextBlock=Lucent.SmoothText;

namespace Lucent
{
    internal sealed class LyricLine
    {
        public double Time;
        public string Text;
    }

    internal sealed class LyricsResult
    {
        public List<LyricLine> Lines=new List<LyricLine>();
        // True when every line carries the time it is sung. Otherwise the text is shown as it is, without following the song.
        public bool Synced;
    }

    // Finds lyrics for the song that is playing. Nothing here runs until the lyrics view is switched on, because looking
    // a song up sends its title, artist and length to lrclib.net, a free public lyrics database.
    internal static class LyricsService
    {
        static readonly Dictionary<string,LyricsResult> cache=new Dictionary<string,LyricsResult>();
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;

        // Turns the text of a .lrc file ("[01:23.45] words") into lines in time order. Plain text becomes lines without times.
        internal static LyricsResult Parse(string synced,string plain)
        {
            var timed=new List<LyricLine>();
            if(!string.IsNullOrWhiteSpace(synced))
            {
                foreach(string raw in synced.Replace("\r","").Split('\n'))
                {
                    var stamps=Regex.Matches(raw,@"\[(\d+):(\d+(?:[.:]\d+)?)\]");
                    if(stamps.Count==0)continue;
                    string text=Regex.Replace(Regex.Replace(raw,@"\[[^\]]*\]",""),@"<\d+:\d+(?:[.:]\d+)?>","").Trim();
                    if(text.Length==0)continue;
                    foreach(Match stamp in stamps)
                    {
                        double minutes,seconds;
                        if(!double.TryParse(stamp.Groups[1].Value,NumberStyles.Float,Invariant,out minutes)||!double.TryParse(stamp.Groups[2].Value.Replace(':','.'),NumberStyles.Float,Invariant,out seconds))continue;
                        timed.Add(new LyricLine {Time=minutes*60+seconds,Text=text});
                    }
                }
                if(timed.Count>0)return new LyricsResult {Lines=timed.OrderBy(x=>x.Time).ToList(),Synced=true};
            }
            if(!string.IsNullOrWhiteSpace(plain))
            {
                var lines=plain.Replace("\r","").Split('\n').Select(x=>x.Trim()).ToList();
                while(lines.Count>0&&lines[0].Length==0)lines.RemoveAt(0);
                while(lines.Count>0&&lines[lines.Count-1].Length==0)lines.RemoveAt(lines.Count-1);
                if(lines.Count>0)return new LyricsResult {Lines=lines.Select(x=>new LyricLine {Time=-1,Text=x}).ToList(),Synced=false};
            }
            return null;
        }
        // "Song (feat. Someone) - 2011 Remaster" searches better as "Song".
        internal static string CleanTitle(string title)
        {
            string clean=Regex.Replace(title??"",@"\s*[\(\[][^\)\]]*[\)\]]","");
            clean=Regex.Replace(clean,@"\s+-\s+.*(remaster|version|edit|live|mix|from).*$","",RegexOptions.IgnoreCase).Trim();
            return clean.Length==0?(title??"").Trim():clean;
        }
        internal static string FirstArtist(string artist)
        {
            if(string.IsNullOrWhiteSpace(artist))return "";
            return Regex.Split(artist,@"\s*(?:,|&|;|\sfeat\.?\s|\sx\s|\swith\s)\s*",RegexOptions.IgnoreCase)[0].Trim();
        }
        internal static LyricsResult Find(string title,string artist,double duration)
        {
            if(string.IsNullOrWhiteSpace(title))return null;
            string key=((artist??"")+"|"+title).ToLowerInvariant();
            lock(cache){LyricsResult known;if(cache.TryGetValue(key,out known))return known;}
            LyricsResult found=null;
            try
            {
                found=Search("track_name="+Uri.EscapeDataString(CleanTitle(title))+(FirstArtist(artist).Length>0?"&artist_name="+Uri.EscapeDataString(FirstArtist(artist)):""),duration);
                if(found==null)found=Search("q="+Uri.EscapeDataString((FirstArtist(artist)+" "+CleanTitle(title)).Trim()),duration);
            }
            catch(Exception){return null;}
            lock(cache)cache[key]=found;
            return found;
        }
        static LyricsResult Search(string query,double duration)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://lrclib.net/api/search?"+query);
            request.UserAgent="Lucent (Windows desktop app)";
            request.Timeout=8000;
            request.ReadWriteTimeout=8000;
            string json;
            using(var response=request.GetResponse())
            using(var reader=new System.IO.StreamReader(response.GetResponseStream(),System.Text.Encoding.UTF8))json=reader.ReadToEnd();
            return Choose(json,duration);
        }
        // Of the answers, the one with timed lyrics whose length is closest to the song's; plain text only if nothing is timed.
        internal static LyricsResult Choose(string json,double duration)
        {
            var items=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.DeserializeObject(json) as object[];
            if(items==null)return null;
            LyricsResult best=null;double bestScore=double.MaxValue;
            foreach(var item in items.OfType<IDictionary<string,object>>())
            {
                object synced,plain,length;
                item.TryGetValue("syncedLyrics",out synced);item.TryGetValue("plainLyrics",out plain);item.TryGetValue("duration",out length);
                var result=Parse(synced as string,plain as string);
                if(result==null)continue;
                double gap=0;
                try{gap=duration>1&&length!=null?Math.Abs(Convert.ToDouble(length,Invariant)-duration):0;}catch(Exception){}
                if(duration>1&&gap>8)continue;
                double score=gap+(result.Synced?0:1000);
                if(score<bestScore){best=result;bestScore=score;}
            }
            return best;
        }
    }

    // The lyrics beside the player: the line being sung is bright, the others dim, and the lines glide so the current one stays in view.
    internal sealed class LyricsView:Grid
    {
        // The lines live on a canvas. In a grid cell the list would be clipped to the cell's height, and since the clip travels with
        // the list as it scrolls, only the first screenful of lines would ever be drawn.
        readonly Canvas stage=new Canvas();
        readonly StackPanel list=new StackPanel();
        readonly TranslateTransform scroll=new TranslateTransform();
        readonly TextBlock note=new TextBlock {FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromArgb(150,255,255,255))};
        readonly List<Border> items=new List<Border>();
        List<LyricLine> lines=new List<LyricLine>();
        bool synced;
        int active=-1;
        double manual;
        internal Action<double> Seek=seconds=>{};
        internal int ActiveIndex {get{return active;}}
        internal int LineCount {get{return lines.Count;}}
        internal bool Synced {get{return synced;}}
        internal string Note {get{return note.Visibility==Visibility.Visible?note.Text:null;}}
        internal double ScrollOffset {get{return scroll.Y;}}
        internal IList<Border> Items {get{return items;}}
        internal const double Dim=.38,UpNext=1;
        // The size of a line in pixels, and whether lines are centered. The big player keeps 30 and left; the mini player lets the user choose.
        double lineSize=30;bool centered;
        internal double LineSize {get{return lineSize;}}
        internal bool Centered {get{return centered;}}
        internal void SetStyle(double size,bool center)
        {
            lineSize=Math.Max(10,Math.Min(60,size));centered=center;
            note.HorizontalAlignment=center?HorizontalAlignment.Center:HorizontalAlignment.Left;note.TextAlignment=center?TextAlignment.Center:TextAlignment.Left;
            foreach(var item in items)
            {
                var label=(TextBlock)item.Child;label.FontSize=lineSize;label.TextAlignment=centered?TextAlignment.Center:TextAlignment.Left;
                item.Padding=new Thickness(centered?14:0,Math.Max(8,lineSize*.5),centered?14:28,Math.Max(8,lineSize*.5));
            }
            if(lines.Count==0)return;
            list.UpdateLayout();Follow(false);
        }

        public LyricsView()
        {
            Background=Brushes.Transparent;
            // The lines are drawn as they are, not kept as small pictures (see SmoothText): a picture of a line that slides by fractions of a pixel
            // is resampled every frame and goes soft, and large lyrics are where that shows.
            // (Each line is kept as a small picture, as text is everywhere in the app, unless the window says otherwise (the mini player does, because it scales).
            // A picture slides and fades at no cost; drawing every line again at each step made changing lines stutter while music played.)
            UseLayoutRounding=true;
            list.RenderTransform=scroll;
            stage.Children.Add(list);
            Children.Add(stage);
            Children.Add(note);
            // Lines fade out toward the top and bottom edges.
            // The fade is measured in pixels of what is visible (see SizeChanged below): a mask relative to the element's bounds is stretched over
            // the whole scrolling list, which left the first and last lines, and any line near the ends of a long list, dim.
            SizeChanged+=(s,e)=>
            {
                // The view's edge is a little loose to the sides, so a letter that reaches past the line's width (a wide last letter) is not cut off.
                Clip=new RectangleGeometry(new Rect(-24,0,ActualWidth+48,ActualHeight));
                // While the window that holds the lyrics is changing size (their view opening or closing), laying the lines out again at every step was what
                // made it stutter: they wait and are laid out once at the end (see Thaw). A view that has never been laid out is laid out at once.
                if(Frozen&&laidWidth>0){return;}
                Refit();
            };
        }
        // True while the lyrics should keep their layout (the window is being resized by an animation).
        internal bool Frozen {get;set;}
        double laidWidth,maskedHeight;
        void Refit()
        {
            if(ActualHeight>0&&Math.Abs(ActualHeight-maskedHeight)>.01)
            {
                maskedHeight=ActualHeight;
                var fade=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(0,0,0,0),0),new GradientStop(Color.FromArgb(255,0,0,0),.14),new GradientStop(Color.FromArgb(255,0,0,0),.8),new GradientStop(Color.FromArgb(0,0,0,0),1)},new Point(0,0),new Point(0,ActualHeight)){MappingMode=BrushMappingMode.Absolute};
                fade.Freeze();OpacityMask=fade;
            }
            // The list is as wide as the view. (This compares with the list's own width, not with the last width seen: after the view was hidden and shown again a stale
            // comparison left the list at the zero width it was given while hidden, and the lines could not be seen until the next song.)
            if(ActualWidth>0&&(double.IsNaN(list.Width)||Math.Abs(list.Width-ActualWidth)>.01)){laidWidth=ActualWidth;note.MaxWidth=Math.Max(100,ActualWidth);list.Width=ActualWidth;list.UpdateLayout();}
            Follow(false);
        }
        // The window has its final size: the lines are laid out once.
        internal void Thaw(){Frozen=false;Refit();}        internal void ShowMessage(string text)
        {
            Reset();
            note.Text=text;
            note.Visibility=Visibility.Visible;
        }
        internal void Clear()
        {
            Reset();
            note.Visibility=Visibility.Collapsed;
        }
        void Reset()
        {
            lines=new List<LyricLine>();items.Clear();list.Children.Clear();
            synced=false;active=-1;manual=0;
            scroll.BeginAnimation(TranslateTransform.YProperty,null);scroll.Y=0;
        }
        internal void SetLyrics(LyricsResult result)
        {
            Reset();
            if(result==null||result.Lines.Count==0){ShowMessage("No lyrics found for this song");return;}
            note.Visibility=Visibility.Collapsed;
            lines=result.Lines;synced=result.Synced;
            foreach(var line in lines)
            {
                var label=new TextBlock {Text=line.Text.Length==0?" ":line.Text,FontSize=lineSize,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,FontFamily=Design.DisplayFont,TextAlignment=centered?TextAlignment.Center:TextAlignment.Left};
                var item=new Border {Background=Brushes.Transparent,Padding=new Thickness(centered?14:0,Math.Max(8,lineSize*.5),centered?14:28,Math.Max(8,lineSize*.5)),Opacity=synced?Dim:.85,Child=label,Cursor=synced?Cursors.Hand:Cursors.Arrow};
                double time=line.Time;
                if(synced)
                {
                    // Pointing at a line that is not lit lifts it a little. The line being sung, and the first line before the song reaches it, stay fully lit.
                    item.MouseEnter+=(s,e)=>{if(!Lit(items.IndexOf(item)))Design.Animate(item,OpacityProperty,item.Opacity,.62,110);};
                    item.MouseLeave+=(s,e)=>{Design.Animate(item,OpacityProperty,item.Opacity,Lit(items.IndexOf(item))?1:Dim,200);};
                    item.MouseLeftButtonUp+=(s,e)=>{Seek(time);e.Handled=true;};
                }
                items.Add(item);list.Children.Add(item);
            }
            // Before the first line is sung (the start of a song, or after a seek back) the first line is the one coming up: it is shown near full strength.
            if(synced&&items.Count>0)items[0].Opacity=UpNext;
            // (While the view is hidden its width is 0: giving the list that width left every line squeezed to nothing when the view came back.)
            if(ActualWidth>0)list.Width=ActualWidth;
            UpdateLayout();
            Refit();
        }
        // Whether a line is shown fully lit: the one being sung, or, before the song reaches the first line, the first one.
        bool Lit(int index){return index>=0&&(index==active||(active<0&&index==0));}
        // The line being sung at this moment. A little early, so a line is lit as its first word starts.
        internal void Update(double seconds)
        {
            if(!synced||lines.Count==0)return;
            int index=-1,low=0,high=lines.Count-1;
            while(low<=high){int mid=(low+high)/2;if(lines[mid].Time<=seconds+.25){index=mid;low=mid+1;}else high=mid-1;}
            if(index==active)return;
            int before=active;
            active=index;
            manual=0;
            if(before>=0&&before<items.Count)Design.Animate(items[before],OpacityProperty,items[before].Opacity,Dim,260);
            if(active>=0)Design.Animate(items[active],OpacityProperty,items[active].Opacity,1,260);
            else if(items.Count>0)Design.Animate(items[0],OpacityProperty,items[0].Opacity,UpNext,260);
            Follow(true);
        }
        // Slides the lines so the active one rests about a third of the way down.
        // Lines far above or below what is on screen are not drawn at all (they keep their room, so nothing moves). Drawing every line of a long song
        // each frame while the lyrics slide made scrolling stutter.
        void Cull(double from,double to)
        {
            double margin=ActualHeight*.5,low=-Math.Max(from,to)-margin,high=ActualHeight-Math.Min(from,to)+margin,top=0;
            foreach(var item in items)
            {
                double bottom=top+item.ActualHeight;
                var wanted=bottom>=low&&top<=high?Visibility.Visible:Visibility.Hidden;
                if(item.Visibility!=wanted)item.Visibility=wanted;
                top=bottom;
            }
        }
        void Follow(bool animate,int milliseconds=520)
        {
            if(items.Count==0||ActualHeight<=0)return;
            double target;
            // The top and bottom of the view fade out, so the line in use rests where it is fully lit; until the first line, that place is for the first line.
            if(!synced)target=ActualHeight*.16;
            else
            {
                var item=items[Math.Max(0,active)];
                double top=item.TranslatePoint(new Point(),list).Y;
                target=ActualHeight*.34-(top+item.ActualHeight/2);
            }
            target+=manual;
            // Rest on a whole pixel, so the text lands on the pixel grid and is sharp once it has arrived.
            double pixel=VisualTreeHelper.GetDpi(this).DpiScaleY;target=Math.Round(target*pixel)/pixel;
            Cull(scroll.Y,target);
            if(!animate||!IsVisible){scroll.BeginAnimation(TranslateTransform.YProperty,null);scroll.Y=target;Cull(target,target);return;}
            var slide=new DoubleAnimation(target,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
            // When it has arrived, only the lines around the new place are kept.
            slide.Completed+=(s,e)=>Cull(target,target);
            scroll.BeginAnimation(TranslateTransform.YProperty,slide);
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if(items.Count==0)return;
            manual+=e.Delta*.6;
            if(!synced)manual=Math.Min(0,Math.Max(-Math.Max(0,list.ActualHeight-ActualHeight*.7),manual));
            Follow(true,170);
            e.Handled=true;
        }
    }
}
