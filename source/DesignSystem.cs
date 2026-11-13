using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Lucent
{
    // WPF glyph runs have no vertical antialiasing, which leaves stepped edges on
    // curves. Drawing each string's outline gives smooth edges at every size.
    internal sealed class SmoothText:FrameworkElement
    {
        const FrameworkPropertyMetadataOptions Inherited=FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender;
        public static readonly DependencyProperty TextProperty=DependencyProperty.Register("Text",typeof(string),typeof(SmoothText),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ForegroundProperty=TextElement.ForegroundProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontSizeProperty=TextElement.FontSizeProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(14d,Inherited));
        public static readonly DependencyProperty FontFamilyProperty=TextElement.FontFamilyProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily,Inherited));
        public static readonly DependencyProperty FontWeightProperty=TextElement.FontWeightProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(FontWeights.Normal,Inherited));
        public static readonly DependencyProperty FontStyleProperty=TextElement.FontStyleProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(FontStyles.Normal,Inherited));
        public static readonly DependencyProperty FontStretchProperty=TextElement.FontStretchProperty.AddOwner(typeof(SmoothText),new FrameworkPropertyMetadata(FontStretches.Normal,Inherited));
        static readonly DependencyProperty WrappingProperty=DependencyProperty.Register("TextWrapping",typeof(TextWrapping),typeof(SmoothText),new FrameworkPropertyMetadata(TextWrapping.NoWrap,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
        static readonly DependencyProperty TrimmingProperty=DependencyProperty.Register("TextTrimming",typeof(TextTrimming),typeof(SmoothText),new FrameworkPropertyMetadata(TextTrimming.None,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
        static readonly DependencyProperty AlignmentProperty=DependencyProperty.Register("TextAlignment",typeof(TextAlignment),typeof(SmoothText),new FrameworkPropertyMetadata(TextAlignment.Left,FrameworkPropertyMetadataOptions.AffectsRender));
        static readonly DependencyProperty LineHeightProperty=DependencyProperty.Register("LineHeight",typeof(double),typeof(SmoothText),new FrameworkPropertyMetadata(double.NaN,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
        // Outlines are slow to redraw, and every animation frame would redraw them all.
        // Rasterizing each element once keeps the smooth edges at no per-frame cost.
        static readonly BitmapCache Rasterized=NewCache();
        static BitmapCache NewCache(){var cache=new BitmapCache{SnapsToDevicePixels=true};cache.Freeze();return cache;}
        // The text is drawn into a child of its own, which keeps the picture. When the element only gets wider or narrower (a window being resized) and the words
        // and their line breaks stay the same, that child is not touched, so nothing is drawn again: redrawing every stretched label at each step of a resize was most of its cost.
        readonly DrawingVisual ink=new DrawingVisual();
        string inkKey;Brush inkBrush;
        public SmoothText()
        {
            ink.CacheMode=Rasterized;AddVisualChild(ink);
            // Text that is not on screen is not outlined (see OnRender); it is drawn when it shows.
            IsVisibleChanged+=(s,e)=>{if((bool)e.NewValue&&inkKey==null&&!string.IsNullOrEmpty(Text))InvalidateVisual();};
        }
        protected override int VisualChildrenCount {get{return 1;}}
        protected override Visual GetVisualChild(int index){return ink;}
        // Text is kept as a small picture so a page opens quickly, but a picture drawn at one size goes soft when it is scaled up. A window that
        // scales its contents (the mini player) turns this on for everything inside it, and the text is then drawn as it is, at any size.
        public static readonly DependencyProperty CrispProperty=DependencyProperty.RegisterAttached("Crisp",typeof(bool),typeof(SmoothText),
            new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits,(d,e)=>{var text=d as SmoothText;if(text!=null)text.ink.CacheMode=(bool)e.NewValue?null:Rasterized;}));
        public static bool GetCrisp(DependencyObject element){return (bool)element.GetValue(CrispProperty);}
        public static void SetCrisp(DependencyObject element,bool value){element.SetValue(CrispProperty,value);}
        public string Text {get{return (string)GetValue(TextProperty);}set{SetValue(TextProperty,value);}}
        public Brush Foreground {get{return (Brush)GetValue(ForegroundProperty);}set{SetValue(ForegroundProperty,value);}}
        public double FontSize {get{return (double)GetValue(FontSizeProperty);}set{SetValue(FontSizeProperty,value);}}
        public FontFamily FontFamily {get{return (FontFamily)GetValue(FontFamilyProperty);}set{SetValue(FontFamilyProperty,value);}}
        public FontWeight FontWeight {get{return (FontWeight)GetValue(FontWeightProperty);}set{SetValue(FontWeightProperty,value);}}
        public FontStyle FontStyle {get{return (FontStyle)GetValue(FontStyleProperty);}set{SetValue(FontStyleProperty,value);}}
        public FontStretch FontStretch {get{return (FontStretch)GetValue(FontStretchProperty);}set{SetValue(FontStretchProperty,value);}}
        public TextWrapping TextWrapping {get{return (TextWrapping)GetValue(WrappingProperty);}set{SetValue(WrappingProperty,value);}}
        public TextTrimming TextTrimming {get{return (TextTrimming)GetValue(TrimmingProperty);}set{SetValue(TrimmingProperty,value);}}
        public TextAlignment TextAlignment {get{return (TextAlignment)GetValue(AlignmentProperty);}set{SetValue(AlignmentProperty,value);}}
        public double LineHeight {get{return (double)GetValue(LineHeightProperty);}set{SetValue(LineHeightProperty,value);}}
        FormattedText Format(string text,double width)
        {
            var format=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection,new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),FontSize,Foreground??Brushes.White,null,TextFormattingMode.Ideal,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            if(!double.IsNaN(LineHeight))format.LineHeight=LineHeight;
            bool bounded=width>0&&!double.IsInfinity(width);
            if(bounded&&(TextWrapping!=TextWrapping.NoWrap||TextTrimming!=TextTrimming.None||TextAlignment!=TextAlignment.Left))format.MaxTextWidth=width;
            if(TextWrapping==TextWrapping.NoWrap&&TextTrimming!=TextTrimming.None){format.MaxLineCount=1;format.Trimming=TextTrimming;}
            format.TextAlignment=TextAlignment;return format;
        }
        // Shaping a string is the slow part of laying a page out, and the same strings are measured every time a page opens.
        static readonly System.Collections.Generic.Dictionary<string,Size> Measured=new System.Collections.Generic.Dictionary<string,Size>();
        string MeasureKey(string text,double width)
        {
            bool bounded=width>0&&!double.IsInfinity(width)&&(TextWrapping!=TextWrapping.NoWrap||TextTrimming!=TextTrimming.None||TextAlignment!=TextAlignment.Left);
            return string.Concat(text,"\u0001",bounded?Math.Round(width,2).ToString("R"):"-","\u0001",FontSize.ToString("R"),"\u0001",FontFamily.Source,"\u0001",FontWeight.ToOpenTypeWeight().ToString(),FontStyle.ToString(),FontStretch.ToString(),"\u0001",((int)TextWrapping).ToString(),((int)TextTrimming).ToString(),((int)TextAlignment).ToString(),"\u0001",LineHeight.ToString("R"),"\u0001",VisualTreeHelper.GetDpi(this).PixelsPerDip.ToString("R"));
        }
        protected override Size MeasureOverride(Size available)
        {
            string text=Text;bool empty=string.IsNullOrEmpty(text);
            string key=MeasureKey(empty?"":text,available.Width);Size size;
            if(Measured.TryGetValue(key,out size))return size;
            var format=Format(empty?"x":text,available.Width);
            size=new Size(empty?0:Math.Ceiling(format.WidthIncludingTrailingWhitespace),Math.Ceiling(format.Height));
            if(Measured.Count>8000)Measured.Clear();
            Measured[key]=size;
            return size;
        }
        // The outline of a piece of text is only worked out once for the same words, font, size and room: building it is the slow part of drawing text,
        // and the window being dragged wider asks for the same text again at every step (its element is a little wider each time, so it is redrawn).
        static readonly System.Collections.Generic.Dictionary<string,Geometry> Outlines=new System.Collections.Generic.Dictionary<string,Geometry>();
        protected override void OnRender(DrawingContext dc)
        {
            string text=Text;
            if(string.IsNullOrEmpty(text)){if(inkKey!=null){inkKey=null;inkBrush=null;using(ink.RenderOpen()){}}return;}
            // Slightly over the measured width, so a rounded layout cannot rewrap the last word.
            double width=TextWrapping==TextWrapping.NoWrap?RenderSize.Width:RenderSize.Width+1;
            string key=MeasureKey(text,width)+"\u0001"+(int)FlowDirection;Geometry outline;
            var brush=Foreground;
            if(key==inkKey&&ReferenceEquals(brush,inkBrush))return;
            // Hidden text (a lyric line far from the one being sung, a page not shown) is outlined only once it shows: a song's lyrics used to outline
            // every line at once when they arrived, which held the window up for a moment.
            if(!IsVisible){inkKey=null;return;}
            if(!Outlines.TryGetValue(key,out outline))
            {
                outline=Format(text,width).BuildGeometry(new Point());
                if(outline.CanFreeze)outline.Freeze();
                if(Outlines.Count>4000)Outlines.Clear();
                Outlines[key]=outline;
            }
            // Outlines alone read lighter than other apps' text on dark; a hairline restores the weight.
            using(var draw=ink.RenderOpen())draw.DrawGeometry(brush,new Pen(brush,.2),outline);
            inkKey=key;inkBrush=brush;
        }        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer(){return new TextPeer(this);}
        sealed class TextPeer:System.Windows.Automation.Peers.FrameworkElementAutomationPeer
        {
            public TextPeer(SmoothText owner):base(owner){}
            protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore()
            {
                return System.Windows.Automation.Peers.AutomationControlType.Text;
            }
            protected override string GetNameCore(){return ((SmoothText)Owner).Text;}
        }
    }
    // The "Recently played" panel of the app: it slides in from the right with the songs that played, newest first. The media apps do not tell Lucent
    // what is coming next, so this is the history of what has played, not a queue.
    internal sealed class HistoryPanel:Border
    {
        readonly StackPanel rows=new StackPanel {Margin=new Thickness(4,0,4,10)};
        readonly SmoothText sub=new SmoothText {FontSize=12,Foreground=new SolidColorBrush(Color.FromArgb(150,255,255,255))};
        readonly TranslateTransform move=new TranslateTransform(60,0);
        readonly System.Collections.Generic.Dictionary<string,BitmapSource> art=new System.Collections.Generic.Dictionary<string,BitmapSource>();
        readonly System.Windows.Threading.DispatcherTimer settle=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(340)};
        readonly Action changed;
        // The list is made the first time the panel opens (until then it is not part of the app's layout at all).
        Grid body;
        bool open;
        internal bool IsOpen {get{return open;}}
        // Raised when it opens or closes, so the button that shows it can light up.
        internal event Action<bool> OpenChanged;
        public HistoryPanel()
        {
            var title=new SmoothText {Text="Recently played",FontSize=16,FontWeight=FontWeights.Bold,Foreground=Brushes.White};
            var texts=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,0,0)};
            texts.Children.Add(title);texts.Children.Add(sub);
            var back=Icon(34);
            back.Content=new Viewbox {Width=16,Height=16,Child=new System.Windows.Shapes.Path {Data=Geometry.Parse("M14,5 L7,12 L14,19"),Stroke=Brushes.White,StrokeThickness=2.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=24,Height=24,Stretch=Stretch.Uniform}};
            System.Windows.Automation.AutomationProperties.SetName(back,"Close recently played");
            back.Click+=(s,e)=>Set(false);
            var clear=Icon(34);
            var trash=Design.Icon("Trash",18);trash.Opacity=.7;clear.Content=trash;
            clear.ToolTip="Clear the list";
            System.Windows.Automation.AutomationProperties.SetName(clear,"Clear recently played");
            clear.Click+=(s,e)=>PlayHistory.Clear();
            var header=new Grid {Margin=new Thickness(10,10,10,6)};
            header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            header.Children.Add(back);
            Grid.SetColumn(texts,1);header.Children.Add(texts);
            Grid.SetColumn(clear,2);header.Children.Add(clear);
            body=new Grid();
            body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            body.RowDefinitions.Add(new RowDefinition());
            body.Children.Add(header);
            Width=360;HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Stretch;
            CornerRadius=new CornerRadius(16);Background=new SolidColorBrush(Color.FromArgb(184,6,6,6));Design.SetStill(this,true);
            Visibility=Visibility.Collapsed;Opacity=0;RenderTransform=move;ClipToBounds=true;Child=body;
            settle.Tick+=(s,e)=>{settle.Stop();if(open)CacheMode=null;};
            changed=()=>{if(open)Dispatcher.BeginInvoke(new Action(Refresh));};
            Loaded+=(s,e)=>PlayHistory.Changed+=changed;
            Unloaded+=(s,e)=>PlayHistory.Changed-=changed;
        }
        static Button Icon(double size)
        {
            var button=new Button();PlayerCore.Frame(button,size);button.Height=size;
            PlayerCore.Motion(button,1.1,.9);
            return button;
        }
        BitmapSource Cover(HistoryEntry entry)
        {
            if(entry.Artwork==null)return null;
            BitmapSource found;
            if(art.TryGetValue(entry.Key,out found))return found;
            try
            {
                var image=new BitmapImage();
                using(var stream=new MemoryStream(entry.Artwork)){image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=96;image.StreamSource=stream;image.EndInit();}
                image.Freeze();
                if(art.Count>200)art.Clear();
                art[entry.Key]=image;
                return image;
            }
            catch(Exception){return null;}
        }
        void Refresh()
        {
            rows.Children.Clear();
            var list=PlayHistory.Newest();
            sub.Text=list.Count==1?"1 track":list.Count+" tracks";
            if(list.Count==0)
            {
                rows.Children.Add(new SmoothText {Text="Songs you play will show up here.",FontSize=14,Foreground=new SolidColorBrush(Color.FromArgb(150,255,255,255)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(14,10,14,0)});
                return;
            }
            for(int i=0;i<list.Count;i++)
            {
                var entry=list[i];
                var row=new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(56)});
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var picture=new Grid {Width=46,Height=46,HorizontalAlignment=HorizontalAlignment.Left,Background=new SolidColorBrush(Color.FromRgb(40,40,44)),Clip=new RectangleGeometry(new Rect(0,0,46,46),9,9)};
                var cover=Cover(entry);
                if(cover!=null)picture.Children.Add(new Image {Source=cover,Stretch=Stretch.UniformToFill});
                else picture.Children.Add(Design.Icon("Music",22));
                var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                words.Children.Add(new SmoothText {Text=entry.Title,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis});
                words.Children.Add(new SmoothText {Text=entry.Artist??"",FontSize=12,Foreground=new SolidColorBrush(Color.FromArgb(160,255,255,255)),TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)});
                Grid.SetColumn(words,1);
                row.Children.Add(picture);row.Children.Add(words);
                // The song playing now is the first row, and is picked out.
                rows.Children.Add(new Border {Padding=new Thickness(8,7,8,7),CornerRadius=new CornerRadius(10),Background=i==0?new SolidColorBrush(Color.FromArgb(28,255,255,255)):(Brush)Brushes.Transparent,Child=row});
            }
        }
        internal void Set(bool show)
        {
            if(show==open)return;
            open=show;
            // Slid as one picture, so it moves smoothly.
            CacheMode=new BitmapCache();
            var ease=new CubicEase {EasingMode=show?EasingMode.EaseOut:EasingMode.EaseIn};
            if(show)
            {
                if(rows.Parent==null)
                {
                    var scroll=new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
                    Grid.SetRow(scroll,1);body.Children.Add(scroll);
                }
                Refresh();
                Visibility=Visibility.Visible;
                Design.Animate(this,OpacityProperty,Opacity,1,220);
                move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(move.X,0,TimeSpan.FromMilliseconds(300)){EasingFunction=ease});
                settle.Stop();settle.Start();
            }
            else
            {
                var fade=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(180));
                fade.Completed+=(s,e)=>{if(!open){Visibility=Visibility.Collapsed;CacheMode=null;}};
                BeginAnimation(OpacityProperty,fade);
                move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(move.X,60,TimeSpan.FromMilliseconds(200)){EasingFunction=ease});
            }
            var raised=OpenChanged;if(raised!=null)raised(show);
        }
    }    internal static class Design
    {
        // AssemblyInfo.cs is the one place the version is written; the manifest only mirrors it.
        // Only for the pictures of the app (tests): a version to show in place of the real one.
        internal static string VersionOverride;
        public static string Version
        {
            get
            {
                if(!string.IsNullOrEmpty(VersionOverride))return VersionOverride;
                var named=(System.Reflection.AssemblyInformationalVersionAttribute[])typeof(Design).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false);
                return named.Length>0&&!string.IsNullOrEmpty(named[0].InformationalVersion)?named[0].InformationalVersion:Updates.Show(typeof(Design).Assembly.GetName().Version);
            }
        }
        // The snapshot (test build) number from AssemblyInfo.cs, or null in a build without one.
        public static string Snapshot
        {
            get
            {
                foreach(System.Reflection.AssemblyMetadataAttribute item in typeof(Design).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false))
                    if(item.Key=="Snapshot"&&!string.IsNullOrWhiteSpace(item.Value))return item.Value.Trim();
                return null;
            }
        }        public static readonly FontFamily Font=LoadFont();
        public static readonly FontFamily DisplayFont=LoadDisplayFont();
        // Large headings use Inter's display cut when it ships with the app, otherwise the regular cut.
        static FontFamily LoadDisplayFont()
        {
            string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts");
            return File.Exists(System.IO.Path.Combine(folder,"InterDisplay-Bold.ttf"))?new FontFamily(new Uri(folder+System.IO.Path.DirectorySeparatorChar),"./#Inter Display"):Font;
        }
        static FontFamily LoadFont()
        {
            string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts");
            return Directory.Exists(folder)?new FontFamily(new Uri(folder+System.IO.Path.DirectorySeparatorChar),"./#Inter"):new FontFamily("Segoe UI");
        }
        // The average color of a picture: what it is made of as a whole (a nearly black cover with a small colorful patch averages to nearly black).
        // Pressing a slider anywhere on its track puts the thumb there, and the same press goes on to drag it from that point (without this a press on the
        // track only moved the thumb, and dragging needed a second press on the thumb).
        static bool sliderJump;
        public static void EnableSliderJump()
        {
            if(sliderJump)return;
            sliderJump=true;
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Slider),UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((sender,e)=>
            {
                var slider=sender as System.Windows.Controls.Slider;
                if(slider==null||!slider.IsEnabled||e.Handled)return;
                var track=slider.Template==null?null:slider.Template.FindName("PART_Track",slider) as System.Windows.Controls.Primitives.Track;
                if(track==null||track.Thumb==null)return;
                // A press on the thumb drags it, as always.
                var source=e.OriginalSource as DependencyObject;
                while(source!=null&&!ReferenceEquals(source,track.Thumb)&&!ReferenceEquals(source,slider))source=source is Visual||source is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
                if(ReferenceEquals(source,track.Thumb))return;
                double value=track.ValueFromPoint(e.GetPosition(track));
                if(double.IsNaN(value)||double.IsInfinity(value))return;
                slider.Value=Math.Max(slider.Minimum,Math.Min(slider.Maximum,value));
                slider.UpdateLayout();
                e.Handled=true;
                // The thumb is now under the pointer: press it too, and the drag carries on from here.
                track.Thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice,e.Timestamp,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent,Source=track.Thumb});
            }));
        }
        public static Color Average(BitmapSource source)
        {
            if(source==null)return Color.FromRgb(45,45,48);
            double scale=16d/Math.Max(source.PixelWidth,source.PixelHeight);
            BitmapSource small=new FormatConvertedBitmap(new TransformedBitmap(source,new ScaleTransform(scale,scale)),PixelFormats.Bgra32,null,0);
            int stride=small.PixelWidth*4;var pixels=new byte[stride*small.PixelHeight];small.CopyPixels(pixels,stride,0);
            double r=0,g=0,b=0;int count=0;
            for(int i=0;i<pixels.Length;i+=4){if(pixels[i+3]<128)continue;b+=pixels[i];g+=pixels[i+1];r+=pixels[i+2];count++;}
            return count==0?Color.FromRgb(45,45,48):Color.FromRgb((byte)(r/count),(byte)(g/count),(byte)(b/count));
        }
        // The cover with its small patches of other colors pulled over to the color it has the most of (by area): a bright blue cover with a face on it gives
        // blue, not the brown of the face. The backdrops are made from this picture. Colors that cover a good share of it (a cover of two halves) stay as they are,
        // and a cover with hardly any color (black and white) is returned as it is.
        public static BitmapSource TowardDominant(BitmapSource source)
        {
            if(source==null)return null;
            try
            {
                var small=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
                int w=small.PixelWidth,h=small.PixelHeight,stride=w*4,count=w*h;
                var px=new byte[stride*h];small.CopyPixels(px,stride,0);
                const int Bins=36;
                var mass=new double[Bins];var hue=new double[count];var sat=new double[count];var val=new double[count];double total=0;
                for(int p=0;p<count;p++)
                {
                    double b=px[p*4]/255d,g=px[p*4+1]/255d,r=px[p*4+2]/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min,hh=0;
                    if(d>0){hh=max==r?((g-b)/d)%6:max==g?(b-r)/d+2:(r-g)/d+4;hh*=60;if(hh<0)hh+=360;}
                    hue[p]=hh;sat[p]=max<=0?0:d/max;val[p]=max;
                    if(sat[p]>.18&&max>.12&&px[p*4+3]>=128){double weight=sat[p]*(.25+Math.Min(.75,max));mass[(int)(hh/10)%Bins]+=weight;total+=weight;}
                }
                if(total<count*.02)return source;
                var smooth=new double[Bins];int peak=0;
                for(int k=0;k<Bins;k++){smooth[k]=mass[k]*.5+mass[(k+1)%Bins]*.25+mass[(k+Bins-1)%Bins]*.25;}
                for(int k=1;k<Bins;k++)if(smooth[k]>smooth[peak])peak=k;
                double sx=0,sy=0;
                for(int k=-2;k<=2;k++){int bin=(peak+k+Bins)%Bins;double angle=(bin*10+5)*Math.PI/180;sx+=mass[bin]*Math.Cos(angle);sy+=mass[bin]*Math.Sin(angle);}
                double dominant=(Math.Atan2(sy,sx)*180/Math.PI+360)%360;
                Func<double,double> smoothstep=t=>{t=t<0?0:t>1?1:t;return t*t*(3-2*t);};
                var output=new byte[px.Length];Array.Copy(px,output,px.Length);
                for(int p=0;p<count;p++)
                {
                    if(sat[p]<.08)continue;
                    double distance=Math.Abs(hue[p]-dominant);if(distance>180)distance=360-distance;
                    double far=smoothstep((distance-20)/30),share=smooth[(int)(hue[p]/10)%Bins]/Math.Max(1e-9,smooth[peak]),minority=1-smoothstep((share-.25)/.25),pull=far*minority;
                    if(pull<.001)continue;
                    double delta=((dominant-hue[p]+540)%360)-180,newHue=(hue[p]+delta*pull+360)%360;
                    double c=val[p]*sat[p],x=c*(1-Math.Abs((newHue/60)%2-1)),m=val[p]-c,r=0,g=0,b=0;
                    switch((int)(newHue/60)%6){case 0:r=c;g=x;break;case 1:r=x;g=c;break;case 2:g=c;b=x;break;case 3:g=x;b=c;break;case 4:r=x;b=c;break;default:r=c;b=x;break;}
                    output[p*4]=(byte)Math.Round((b+m)*255);output[p*4+1]=(byte)Math.Round((g+m)*255);output[p*4+2]=(byte)Math.Round((r+m)*255);
                }
                var result=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,output,stride);result.Freeze();return result;
            }
            catch(Exception){return source;}
        }
        public static Color Palette(BitmapSource source)
        {
            if(source==null)return Color.FromRgb(45,45,48);
            double scale=24d/Math.Max(source.PixelWidth,source.PixelHeight);
            BitmapSource small=new TransformedBitmap(source,new ScaleTransform(scale,scale));
            small=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);
            int stride=small.PixelWidth*4;var pixels=new byte[stride*small.PixelHeight];small.CopyPixels(pixels,stride,0);
            double[] weights=new double[16],red=new double[16],green=new double[16],blue=new double[16];
            for(int i=0;i<pixels.Length;i+=4)
            {
                double r=pixels[i+2]/255d,g=pixels[i+1]/255d,b=pixels[i]/255d;
                double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
                if(max<.10||min>.87||pixels[i+3]<128)continue;
                double hue=0;
                if(delta>0){hue=max==r?(g-b)/delta%6:max==g?(b-r)/delta+2:(r-g)/delta+4;hue=(hue*60+360)%360;}
                int bucket=(int)(hue/360*15);
                double weight=.12+delta*2;
                weights[bucket]+=weight;red[bucket]+=r*weight;green[bucket]+=g*weight;blue[bucket]+=b*weight;
            }
            int best=0;for(int i=1;i<weights.Length;i++)if(weights[i]>weights[best])best=i;
            if(weights[best]<.01)return Color.FromRgb(45,45,48);
            return Color.FromRgb((byte)(red[best]/weights[best]*255),(byte)(green[best]/weights[best]*255),(byte)(blue[best]/weights[best]*255));
        }
        public static Color Mix(Color color,double power,byte baseline)
        {
            return Color.FromRgb((byte)(baseline+(color.R-baseline)*power),(byte)(baseline+(color.G-baseline)*power),(byte)(baseline+(color.B-baseline)*power));
        }
        // The edge of a pane of glass, drawn as a few thin rings, each fainter than the one outside it, laid over the pane (it takes no room and no clicks): a crisp lit
        // outer line that falls off softly toward the inside, brightest along the top. One hard line of one pixel on a curve shows its steps; a soft falloff does not.
        // The rings are not snapped to whole pixels, so their curves are smoothed.
        public static FrameworkElement GlassEdge(double radius)
        {
            var edge=new Grid {IsHitTestVisible=false,UseLayoutRounding=false,SnapsToDevicePixels=false};
            double[] alpha={1,.42,.16};double inset=0;
            for(int ring=0;ring<alpha.Length;ring++)
            {
                double width=ring==0?1.3:1.1;
                var brush=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb((byte)(120*alpha[ring]),255,255,255),0),new GradientStop(Color.FromArgb((byte)(34*alpha[ring]),255,255,255),.5),new GradientStop(Color.FromArgb((byte)(20*alpha[ring]),255,255,255),1)},new Point(0,0),new Point(0,1));
                brush.Freeze();
                edge.Children.Add(new Border {BorderBrush=brush,BorderThickness=new Thickness(width),CornerRadius=new CornerRadius(Math.Max(1,radius-inset)),Margin=new Thickness(inset),UseLayoutRounding=false,SnapsToDevicePixels=false,IsHitTestVisible=false});
                inset+=width;
            }
            return edge;
        }
        // A pane's content with the glass edge laid over it.
        public static UIElement Glass(UIElement content,double radius)
        {
            var pane=new Grid();pane.Children.Add(content);pane.Children.Add(GlassEdge(radius));return pane;
        }
        // The thin lit edge of glass: brightest along the top. Sections have it, like the player bar.
        public static readonly Brush GlassRim=MakeGlassRim();
        static Brush MakeGlassRim()
        {
            var rim=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(96,255,255,255),0),new GradientStop(Color.FromArgb(26,255,255,255),.5),new GradientStop(Color.FromArgb(14,255,255,255),1)},new Point(0,0),new Point(0,1));
            rim.Freeze();return rim;
        }
        // Performance mode: nothing fades or glides, everything just changes.
        public static bool Instant;
        public static void Animate(DependencyObject target,DependencyProperty property,double from,double to,int milliseconds)
        {
            if(Instant)milliseconds=0;
            var animation=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
            var animatable=target as Animatable;if(animatable!=null)animatable.BeginAnimation(property,animation);
            else ((UIElement)target).BeginAnimation(property,animation);
        }
        // Every button animates, including ones with custom templates: small icon buttons
        // grow on hover, and all of them dip while pressed and spring back. Text buttons skip
        // the hover growth, which would blur their rasterized text, and rely on their glow.
        // Inside the players (and the buttons by the bottom right) nothing moves when the pointer is over a button: it only lights up. Set on a player, it holds for everything in it.
        public static readonly DependencyProperty StillProperty=DependencyProperty.RegisterAttached("Still",typeof(bool),typeof(Design),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetStill(DependencyObject element){return (bool)element.GetValue(StillProperty);}
        public static void SetStill(DependencyObject element,bool value){element.SetValue(StillProperty,value);}
        static readonly DependencyProperty MotionProperty=DependencyProperty.RegisterAttached("Motion",typeof(ScaleTransform),typeof(Design));
        static bool motionEnabled;
        // Two buttons that stand for one thing, such as the sidebar gear and its copy over the settings, move as one,
        // so a press that started on one carries on smoothly on the other.
        public static void ShareMotion(Button first,Button second)
        {
            var scale=new ScaleTransform(1,1);
            foreach(var button in new[]{first,second})
            {
                button.SetValue(MotionProperty,scale);
                button.RenderTransform=scale;
                button.RenderTransformOrigin=new Point(.5,.5);
            }
        }
        public static void EnableButtonMotion()
        {
            if(motionEnabled)return;motionEnabled=true;var type=typeof(ButtonBase);
            EventManager.RegisterClassHandler(type,UIElement.MouseEnterEvent,new MouseEventHandler((s,e)=>MoveButton(s,false,false)));
            EventManager.RegisterClassHandler(type,UIElement.MouseLeaveEvent,new MouseEventHandler((s,e)=>MoveButton(s,false,true)));
            EventManager.RegisterClassHandler(type,UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((s,e)=>MoveButton(s,true,false)));
            EventManager.RegisterClassHandler(type,UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler((s,e)=>MoveButton(s,false,!((UIElement)s).IsMouseOver,true)));
        }
        static void MoveButton(object sender,bool pressed,bool away,bool spring=false)
        {
            var button=(ButtonBase)sender;
            if(GetStill(button)||button is RepeatButton||button.TemplatedParent is ComboBox||(button is CheckBox&&button.ActualWidth>=64))return;
            var scale=(ScaleTransform)button.GetValue(MotionProperty);
            if(scale==null)
            {
                // A button with its own transform, such as the mini-player's, animates itself.
                if(!ReferenceEquals(button.RenderTransform,Transform.Identity))return;
                scale=new ScaleTransform(1,1);
                button.SetValue(MotionProperty,scale);
                button.RenderTransform=scale;
                button.RenderTransformOrigin=new Point(.5,.5);
            }
            // Icon buttons only brighten on hover, and a press pops them slightly larger.
            double width=Math.Max(16,button.ActualWidth);bool icon=width<64&&button.Content is Viewbox;
            double hover=width<64&&!icon?1+Math.Min(.12,4/width):1,target=pressed?(icon?1.08:1-Math.Min(.08,4/width)):away?1:hover;
            IEasingFunction ease=spring?(IEasingFunction)new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.6}:new CubicEase{EasingMode=EasingMode.EaseOut};
            var motion=new DoubleAnimation(target,TimeSpan.FromMilliseconds(spring?230:pressed?70:away?170:120)){EasingFunction=ease};
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,motion);scale.BeginAnimation(ScaleTransform.ScaleYProperty,motion);
        }
        // An item template using the smooth text element; a null path shows the item itself.
        public static DataTemplate TextTemplate(string path)
        {
            var label=new FrameworkElementFactory(typeof(SmoothText));
            label.SetBinding(SmoothText.TextProperty,path==null?new System.Windows.Data.Binding():new System.Windows.Data.Binding(path));
            return new DataTemplate{VisualTree=label};
        }
        // Solid sidebar glyphs: a filled shape with its details cut out reads as one
        // flat mark, where thin outlines look busy at this size.
        static Geometry Cut(string shape,string lines,double width)
        {
            return new CombinedGeometry(GeometryCombineMode.Exclude,Geometry.Parse(shape),Widen(lines,width));
        }
        static Geometry Widen(string lines,double width)
        {
            return Geometry.Parse(lines).GetWidenedPathGeometry(new Pen(Brushes.Black,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round});
        }
        public static Viewbox RailIcon(string name,double size=22)
        {
            Geometry data;
            switch(name)
            {
                case "Video":data=Cut("M12,1.8 C12,1.8 4.6,9.6 4.6,14.8 A7.4,7.4 0 0 0 19.4,14.8 C19.4,9.6 12,1.8 12,1.8 Z","M8.2,15.4 C8.5,17.6 9.9,19 12.2,19.4",1.8);
                break;
                case "Resolution":data=new CombinedGeometry(GeometryCombineMode.Union,Cut("M3,2.5 L21,2.5 Q23.5,2.5 23.5,5 L23.5,15.5 Q23.5,18 21,18 L3,18 Q.5,18 .5,15.5 L.5,5 Q.5,2.5 3,2.5 Z","M5,6.5 L8.5,6.5 M5,6.5 L5,10 M19,14 L15.5,14 M19,14 L19,10.5",1.7),Geometry.Parse("M10.2,18 L13.8,18 13.8,20 17,20 Q18,20 18,21 18,22 17,22 L7,22 Q6,22 6,21 6,20 7,20 L10.2,20 Z"));
                break;
                case "Home":data=Cut("M12,2.6 Q12.8,2.6 13.4,3.1 L21.6,10.2 Q22.4,10.9 21.6,11.6 Q21,12 20.5,11.6 L20.5,19.5 Q20.5,21.5 18.5,21.5 L5.5,21.5 Q3.5,21.5 3.5,19.5 L3.5,11.6 Q3,12 2.4,11.6 Q1.6,10.9 2.4,10.2 L10.6,3.1 Q11.2,2.6 12,2.6 Z","M12,16.6 L12,19.8",4.4);
                break;
                case "Profiles":data=Cut("M7,6.5 L17,6.5 Q21.4,6.5 22.4,11.6 L23.2,16.2 Q23.8,20 20.4,20 Q18.2,20 17.2,18 L16.4,16.6 L7.6,16.6 L6.8,18 Q5.8,20 3.6,20 Q0.2,20 0.8,16.2 L1.6,11.6 Q2.6,6.5 7,6.5 Z","M7.4,9.6 L7.4,13.4 M5.5,11.5 L9.3,11.5 M16.4,10.6 L16.5,10.7 M18.8,13 L18.9,13.1",1.9);
                break;
                case "Crosshair":data=new CombinedGeometry(GeometryCombineMode.Union,Widen("M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 Z",2.6),new CombinedGeometry(GeometryCombineMode.Union,Widen("M12,1 L12,6 M12,18 L12,23 M1,12 L6,12 M18,12 L23,12",2.4),new EllipseGeometry(new Point(12,12),1.7,1.7)));
                break;
                case "Visual tools":data=new CombinedGeometry(GeometryCombineMode.Union,new CombinedGeometry(GeometryCombineMode.Exclude,Geometry.Parse("M12,4.6 C6.4,4.6 2.4,8.8 0.8,12 C2.4,15.2 6.4,19.4 12,19.4 C17.6,19.4 21.6,15.2 23.2,12 C21.6,8.8 17.6,4.6 12,4.6 Z"),new EllipseGeometry(new Point(12,12),4.6,4.6)),Geometry.Parse("M13.4,7.9 L8.9,12.7 L11.6,12.7 L10.6,16.3 L15.1,11.3 L12.4,11.3 Z"));
                break;
                case "Backgrounds":data=new CombinedGeometry(GeometryCombineMode.Exclude,Cut("M5,2 L19,2 Q22,2 22,5 L22,19 Q22,22 19,22 L5,22 Q2,22 2,19 L2,5 Q2,2 5,2 Z","M4,18 L9,12.5 13,16.5 16,13.5 20.5,18",1.8),new EllipseGeometry(new Point(15.5,7.5),2,2));
                break;
                default:return Icon(name,size);
            }
            var path=new System.Windows.Shapes.Path{Data=data,Width=24,Height=24,Stretch=Stretch.None,Fill=Brushes.White};
            return new Viewbox{Width=size,Height=size,Child=path};
        }
        // Solid marks for the settings sidebar and About page.
        public static Viewbox SettingsIcon(string name,double size=16,Brush fill=null)
        {
            Geometry data;
            switch(name)
            {
                case "Appearance":data=Cut("M12,1.8 C6.2,1.8 1.6,6.2 1.6,11.8 C1.6,17.6 6.2,22.2 11.8,22.2 C13.5,22.2 14.6,21 14.6,19.6 C14.6,18.2 13.6,17.6 13.6,16.4 C13.6,15 14.7,14.1 16,14.1 L18.4,14.1 C20.8,14.1 22.4,12.3 22.4,10 C22.4,5.2 17.8,1.8 12,1.8 Z","M6.6,12 L6.61,12 M8.9,7.2 L8.91,7.2 M14.6,6.4 L14.61,6.4 M18,10 L18.01,10",3.3);
                break;
                case "Mini Player":case "Overlay":data=Geometry.Parse("M4,2.5 L14,2.5 Q16.5,2.5 16.5,5 L16.5,6 L9.5,6 Q6,6 6,9.5 L6,15.5 L4,15.5 Q1.5,15.5 1.5,13 L1.5,5 Q1.5,2.5 4,2.5 Z M10.5,8 L20,8 Q22.5,8 22.5,10.5 L22.5,19 Q22.5,21.5 20,21.5 L10.5,21.5 Q8,21.5 8,19 L8,10.5 Q8,8 10.5,8 Z");
                break;
                case "Shortcuts":data=Cut("M3.5,4.5 L20.5,4.5 Q23,4.5 23,7 L23,17 Q23,19.5 20.5,19.5 L3.5,19.5 Q1,19.5 1,17 L1,7 Q1,4.5 3.5,4.5 Z","M5.2,9 L5.21,9 M9,9 L9.01,9 M12.8,9 L12.81,9 M16.6,9 L16.61,9 M19.4,9 L19.41,9 M7,12.4 L7.01,12.4 M10.9,12.4 L10.91,12.4 M14.8,12.4 L14.81,12.4 M18.6,12.4 L18.61,12.4 M7.6,15.9 L16.4,15.9",2);
                break;
                case "General":data=new CombinedGeometry(GeometryCombineMode.Union,Widen("M2,7 L22,7 M2,17 L22,17",2.6),new CombinedGeometry(GeometryCombineMode.Union,new EllipseGeometry(new Point(8.5,7),4,4),new EllipseGeometry(new Point(15.5,17),4,4)));
                break;
                case "About":data=new CombinedGeometry(GeometryCombineMode.Exclude,Cut("M12,1.5 A10.5,10.5 0 1 0 12,22.5 A10.5,10.5 0 1 0 12,1.5 Z","M12,11.4 L12,17",2.6),new EllipseGeometry(new Point(12,7.3),1.6,1.6));
                break;
                case "Stats":data=Geometry.Parse("M3,21 L3,12 Q3,10.5 4.5,10.5 L6.5,10.5 Q8,10.5 8,12 L8,21 Z M10,21 L10,4 Q10,2.5 11.5,2.5 L13.5,2.5 Q15,2.5 15,4 L15,21 Z M17,21 L17,9 Q17,7.5 18.5,7.5 L20.5,7.5 Q22,7.5 22,9 L22,21 Z");
                break;
                case "Discord":case "Discord RPC":data=Geometry.Parse("M 20.317 4.3698 a 19.7913 19.7913 0 0 0 -4.8851 -1.5152 .0741 .0741 0 0 0 -.0785 .0371 c -.211 .3753 -.4447 .8648 -.6083 1.2495 -1.8447 -.2762 -3.68 -.2762 -5.4868 0 -.1636 -.3933 -.4058 -.8742 -.6177 -1.2495 a .077 .077 0 0 0 -.0785 -.037 19.7363 19.7363 0 0 0 -4.8852 1.515 .0699 .0699 0 0 0 -.0321 .0277 C .5334 9.0458 -.319 13.5799 .0992 18.0578 a .0824 .0824 0 0 0 .0312 .0561 c 2.0528 1.5076 4.0413 2.4228 5.9929 3.0294 a .0777 .0777 0 0 0 .0842 -.0276 c .4616 -.6304 .8731 -1.2952 1.226 -1.9942 a .076 .076 0 0 0 -.0416 -.1057 c -.6528 -.2476 -1.2743 -.5495 -1.8722 -.8923 a .077 .077 0 0 1 -.0076 -.1277 c .1258 -.0943 .2517 -.1923 .3718 -.2914 a .0743 .0743 0 0 1 .0776 -.0105 c 3.9278 1.7933 8.18 1.7933 12.0614 0 a .0739 .0739 0 0 1 .0785 .0095 c .1202 .099 .246 .1981 .3728 .2924 a .077 .077 0 0 1 -.0066 .1276 12.2986 12.2986 0 0 1 -1.873 .8914 .0766 .0766 0 0 0 -.0407 .1067 c .3604 .698 .7719 1.3628 1.225 1.9932 a .076 .076 0 0 0 .0842 .0286 c 1.961 -.6067 3.9495 -1.5219 6.0023 -3.0294 a .077 .077 0 0 0 .0313 -.0552 c .5004 -5.177 -.8382 -9.6739 -3.5485 -13.6604 a .061 .061 0 0 0 -.0312 -.0286 z M 8.02 15.3312 c -1.1825 0 -2.1569 -1.0857 -2.1569 -2.419 0 -1.3332 .9555 -2.4189 2.157 -2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332 -.9555 2.4189 -2.1569 2.4189 z m 7.9748 0 c -1.1825 0 -2.1569 -1.0857 -2.1569 -2.419 0 -1.3332 .9554 -2.4189 2.1569 -2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332 -.946 2.4189 -2.1568 2.4189 Z");
                break;
                case "GitHub":data=Geometry.Parse("M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12z");
                break;
                case "Support":data=Geometry.Parse("M12,21.5 C7.5,17.6 2,13.4 2,8.2 C2,4.6 4.6,2.5 7.4,2.5 C9.4,2.5 11,3.5 12,5.2 C13,3.5 14.6,2.5 16.6,2.5 C19.4,2.5 22,4.6 22,8.2 C22,13.4 16.5,17.6 12,21.5 Z");
                break;
                case "Folder":data=Geometry.Parse("M4,3.8 L9,3.8 Q10,3.8 10.7,4.5 L12.6,6.4 L20,6.4 Q22.5,6.4 22.5,8.9 L22.5,17.7 Q22.5,20.2 20,20.2 L4,20.2 Q1.5,20.2 1.5,17.7 L1.5,6.3 Q1.5,3.8 4,3.8 Z");
                break;
                case "Document":data=Cut("M6,1.8 L14,1.8 L20.5,8.3 L20.5,19.7 Q20.5,22.2 18,22.2 L6,22.2 Q3.5,22.2 3.5,19.7 L3.5,4.3 Q3.5,1.8 6,1.8 Z","M8,13 L16,13 M8,17 L16,17",1.9);
                break;
                case "Copy":data=Geometry.Parse("M9.5,2 L19.5,2 Q22,2 22,4.5 L22,14 Q22,16.5 19.5,16.5 L18.5,16.5 L18.5,9 Q18.5,5.5 15,5.5 L7,5.5 L7,4.5 Q7,2 9.5,2 Z M4.5,7.5 L14,7.5 Q16.5,7.5 16.5,10 L16.5,19.5 Q16.5,22 14,22 L4.5,22 Q2,22 2,19.5 L2,10 Q2,7.5 4.5,7.5 Z");
                break;
                default:return RailIcon(name,size);
            }
            var path=new System.Windows.Shapes.Path{Data=data,Width=24,Height=24,Stretch=Stretch.None,Fill=fill??Brushes.White};
            return new Viewbox{Width=size,Height=size,Child=path};
        }
        // Window controls: bold round-capped strokes on a fixed canvas, so all three share one size and weight.
        public static Viewbox ChromeIcon(string name,double size=15)
        {
            string data;
            switch(name)
            {
                case "Minimize":data="M5,12 L19,12";break;
                case "Collapse":data="M5,9 L12,16 19,9";break;
                case "PopOut":data="M11,6 L8,6 Q6,6 6,8 L6,16 Q6,18 8,18 L16,18 Q18,18 18,16 L18,13 M14,5 L19,5 L19,10 M19,5 L12,12";break;
                case "Dock":data="M11,6 L8,6 Q6,6 6,8 L6,16 Q6,18 8,18 L16,18 Q18,18 18,16 L18,13 M19,5 L12,12 M12,8 L12,12 L16,12";break;
                case "Maximize":data="M8,5 L16,5 Q19,5 19,8 L19,16 Q19,19 16,19 L8,19 Q5,19 5,16 L5,8 Q5,5 8,5 Z";break;
                case "Restore":data="M9.5,5 L16.5,5 Q19,5 19,7.5 L19,14.5 M7,8.5 L13,8.5 Q15.5,8.5 15.5,11 L15.5,16.5 Q15.5,19 13,19 L7,19 Q4.5,19 4.5,16.5 L4.5,11 Q4.5,8.5 7,8.5 Z";
                break;
                default:data="M6,6 L18,18 M18,6 L6,18";break;
            }
            var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Width=24,Height=24,Stretch=Stretch.None,Stroke=Brushes.White,StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
            return new Viewbox{Width=size,Height=size,Child=path};
        }
        public const double WindowInk=.8;
        // A window control: a bold glyph that brightens under the pointer.
        public static Button WindowButton(Style style,string name,Action action)
        {
            var icon=ChromeIcon(name);
            icon.Opacity=WindowInk;
            var button=new Button {Content=icon,Tag=name,Style=style,ToolTip=name,Width=28,Height=30,MinHeight=0,Padding=new Thickness(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Center,
                // A window button never takes the keyboard: with focus left on Close after a click, pressing Space pressed it and sent the app to the tray.
                Focusable=false,FocusVisualStyle=null};
            button.MouseEnter+=(s,e)=>Animate((UIElement)button.Content,UIElement.OpacityProperty,((UIElement)button.Content).Opacity,1,100);
            button.MouseLeave+=(s,e)=>Animate((UIElement)button.Content,UIElement.OpacityProperty,((UIElement)button.Content).Opacity,WindowInk,150);
            button.Click+=(s,e)=>action();
            System.Windows.Automation.AutomationProperties.SetName(button,name);
            return button;
        }
        // One set, drawn on a 24-unit grid: rounded 1.65 strokes, with a few solid marks where a filled shape reads better.
        public static Viewbox Icon(string name,double size=21)
        {
            bool solid=false,rounded=false;string data;
            switch(name)
            {
                case "Color":case "Video":data="M12,3 C12,3 5.5,10 5.5,14.5 A6.5,6.5 0 0 0 18.5,14.5 C18.5,10 12,3 12,3 Z M8.6,15 C8.8,16.8 10,18 11.8,18.3";break;
                case "Monitor":case "Resolution":data="M5,4.5 L19,4.5 Q21,4.5 21,6.5 L21,15.5 Q21,17.5 19,17.5 L5,17.5 Q3,17.5 3,15.5 L3,6.5 Q3,4.5 5,4.5 Z M8.5,21 L15.5,21 M12,17.5 L12,21";break;
                case "Profiles":data="M7.2,7 L16.8,7 Q20.6,7 21.6,11.6 L22.4,16 Q22.8,19.4 19.9,19.4 Q18,19.4 17,17.6 L16.2,16.2 L7.8,16.2 L7,17.6 Q6,19.4 4.1,19.4 Q1.2,19.4 1.6,16 L2.4,11.6 Q3.4,7 7.2,7 Z M7.6,9.8 L7.6,13.4 M5.8,11.6 L9.4,11.6 M16.3,10.4 L16.3,10.5 M18.6,12.7 L18.6,12.8";break;
                case "Crosshair":data="M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 M12,2 L12,8 M12,16 L12,22 M2,12 L8,12 M16,12 L22,12";break;
                case "Game Boost":data="M13.4,2.6 L5.2,13.4 L11,13.4 L10,21.4 L18.8,10.4 L13,10.4 Z";solid=true;rounded=true;break;
                case "Visual tools":data="M2,12 Q12,3.5 22,12 Q12,20.5 2,12 Z M13.2,8.7 L10.6,12.3 L13.4,12.1 L10.8,15.5";break;
                case "Search":data="M10.5,4 A6.5,6.5 0 1 0 10.5,17 A6.5,6.5 0 1 0 10.5,4 M15.3,15.3 L21,21";break;
                case "Backgrounds":data="M5,4 L19,4 Q21,4 21,6 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 L3,6 Q3,4 5,4 Z M3,16.5 L8.5,11 L13,15.5 L16,12.5 L21,17.5 M15.5,8.6 A1.4,1.4 0 1 0 15.5,8.7";break;
                case "Settings":data="F0 M10.36,4.89 L10.64,2.3 L13.36,2.3 L13.64,4.89 A7.3,7.3 0 0 1 15.87,5.81 L17.9,4.17 L19.83,6.1 L18.19,8.13 A7.3,7.3 0 0 1 19.11,10.36 L21.7,10.64 L21.7,13.36 L19.11,13.64 A7.3,7.3 0 0 1 18.19,15.87 L19.83,17.9 L17.9,19.83 L15.87,18.19 A7.3,7.3 0 0 1 13.64,19.11 L13.36,21.7 L10.64,21.7 L10.36,19.11 A7.3,7.3 0 0 1 8.13,18.19 L6.1,19.83 L4.17,17.9 L5.81,15.87 A7.3,7.3 0 0 1 4.89,13.64 L2.3,13.36 L2.3,10.64 L4.89,10.36 A7.3,7.3 0 0 1 5.81,8.13 L4.17,6.1 L6.1,4.17 L8.13,5.81 A7.3,7.3 0 0 1 10.36,4.89 Z M12,8.4 A3.6,3.6 0 1 0 12,15.6 A3.6,3.6 0 1 0 12,8.4 Z";solid=true;rounded=true;break;
                case "OpenPlayer":data="M5,8 L19,8 Q21,8 21,10 L21,14 Q21,16 19,16 L5,16 Q3,16 3,14 L3,10 Q3,8 5,8 Z M6.2,10.2 L8.8,10.2 L8.8,13.8 L6.2,13.8 Z M11.5,10.8 L18,10.8 M11.5,13.2 L15.5,13.2";break;
                case "BigPlayer":data="M8,2.8 L16,2.8 Q19,2.8 19,5.8 L19,18.2 Q19,21.2 16,21.2 L8,21.2 Q5,21.2 5,18.2 L5,5.8 Q5,2.8 8,2.8 Z M7.8,5.6 L16.2,5.6 L16.2,12 L7.8,12 Z M8,15 L13.5,15 M9.5,18.3 L9.52,18.3 M12,18.3 L12.02,18.3 M14.5,18.3 L14.52,18.3";break;
                case "Music":data="M9,18 L9,5.5 L20,3.5 L20,16 M9,9.5 L20,7.5 M6.4,15.3 A2.6,2.6 0 1 0 6.4,20.5 A2.6,2.6 0 1 0 6.4,15.3 M17.4,13.3 A2.6,2.6 0 1 0 17.4,18.5 A2.6,2.6 0 1 0 17.4,13.3";break;
                case "Play":data="M9.6,6.3 Q8,5.4 8,7.2 L8,16.8 Q8,18.6 9.6,17.7 L18.2,12.95 Q19.8,12 18.2,11.05 Z";solid=true;break;
                case "Pause":data="M7.6,6 L9.4,6 Q10.8,6 10.8,7.4 L10.8,16.6 Q10.8,18 9.4,18 L7.6,18 Q6.2,18 6.2,16.6 L6.2,7.4 Q6.2,6 7.6,6 Z M14.6,6 L16.4,6 Q17.8,6 17.8,7.4 L17.8,16.6 Q17.8,18 16.4,18 L14.6,18 Q13.2,18 13.2,16.6 L13.2,7.4 Q13.2,6 14.6,6 Z";solid=true;break;
                case "Previous":data="M4,5 L6,5 6,19 4,19 Z M19,5 Q20,4 20,6 L20,18 Q20,20 18,19 L8,13 Q7,12 8,11 Z";solid=true;break;
                case "Next":data="M18,5 L20,5 20,19 18,19 Z M5,5 Q4,4 4,6 L4,18 Q4,20 6,19 L16,13 Q17,12 16,11 Z";solid=true;break;
                case "Shuffle":data="M2,7 L5.5,7 C10,7 12,17 16.5,17 L21,17 M18,14 L21,17 L18,20 M2,17 L5.5,17 C7.2,17 8.4,16.2 9.4,15 M14.6,9 C15.4,7.9 16.4,7 18,7 L21,7 M18,4 L21,7 L18,10";break;
                case "Repeat":data="M8.5,16 L7,16 Q4,16 4,13 L4,9 Q4,6 7,6 L17,6 Q20,6 20,9 L20,13 Q20,16 17,16 L12,16 M14.5,13.5 L12,16 L14.5,18.5";break;
                case "RepeatOne":data="M8.5,16 L7,16 Q4,16 4,13 L4,9 Q4,6 7,6 L17,6 Q20,6 20,9 L20,13 Q20,16 17,16 L12,16 M14.5,13.5 L12,16 L14.5,18.5 M10.9,9.7 L12.4,8.7 L12.4,13.4";break;
                case "Queue":data="M3,6 L21,6 M3,12 L12.5,12 M3,18 L12.5,18 M16,12.5 L21.5,15.5 L16,18.5 Z";break;
                case "Volume":data="M1.35,8.84 L6.41,8.84 L12.73,3.15 L12.73,20.85 L6.41,15.16 L1.35,15.16 Z M16.53,8.21 Q19.31,12 16.53,15.79 M20.07,4.79 Q25.38,12 20.07,19.21";break;
                case "VolumeLow":data="M1.35,8.84 L6.41,8.84 L12.73,3.15 L12.73,20.85 L6.41,15.16 L1.35,15.16 Z M16.53,8.21 Q19.31,12 16.53,15.79";break;
                case "Mute":data="M1.35,8.84 L6.41,8.84 L12.73,3.15 L12.73,20.85 L6.41,15.16 L1.35,15.16 Z M17.37,9.54 L22.3,14.46 M22.3,9.54 L17.37,14.46";break;
                case "PlayerBar":data="M5,4 L19,4 Q21,4 21,6 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 L3,6 Q3,4 5,4 Z M3,13.8 L21,13.8 M6.6,16.9 L6.62,16.9 M10,16.9 L17.5,16.9";break;
                case "PlayerBarOn":data="M5,4 L19,4 Q21,4 21,6 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 L3,6 Q3,4 5,4 Z M3.9,15.4 L20.1,15.4 L20.1,18 Q20.1,19.1 19,19.1 L5,19.1 Q3.9,19.1 3.9,18 Z";break;
                case "Power":data="M12,3 L12,11 M7.3,6.4 A8,8 0 1 0 16.7,6.4";break;
                case "Lyrics":data="M5,4.5 L19,4.5 Q21,4.5 21,6.5 L21,14.5 Q21,16.5 19,16.5 L11,16.5 L7,20.5 L7,16.5 L5,16.5 Q3,16.5 3,14.5 L3,6.5 Q3,4.5 5,4.5 Z M9.7,8.5 L9.72,8.5 M9.4,8.8 Q9.6,10.6 8.3,11.5 M14.9,8.5 L14.92,8.5 M14.6,8.8 Q14.8,10.6 13.3,11.5";break;
                case "Home":data="M3,11 L12,3.2 L21,11 M5.5,9.2 L5.5,19.5 Q5.5,20.8 6.8,20.8 L10,20.8 L10,15 L14,15 L14,20.8 L17.2,20.8 Q18.5,20.8 18.5,19.5 L18.5,9.2";break;
                case "Chevron":data="M15,5 L8,12 L15,19";break;
                case "More":data="M4.8,12 a1.2,1.2 0 1,0 2.4,0 a1.2,1.2 0 1,0 -2.4,0 M10.8,12 a1.2,1.2 0 1,0 2.4,0 a1.2,1.2 0 1,0 -2.4,0 M16.8,12 a1.2,1.2 0 1,0 2.4,0 a1.2,1.2 0 1,0 -2.4,0";break;
                case "Pin":data="M9,4 L15,4 M10,4 L10,10 L7,14 L17,14 L14,10 L14,4 M12,14 L12,20";break;
                case "PopOut":data="M10,5 L6,5 Q4,5 4,7 L4,18 Q4,20 6,20 L17,20 Q19,20 19,18 L19,14 M14,4 L20,4 L20,10 M20,4 L11,13";break;
                case "Dock":data="M10,5 L6,5 Q4,5 4,7 L4,18 Q4,20 6,20 L17,20 Q19,20 19,18 L19,14 M20,4 L12,12 M12,7 L12,12 L17,12";break;
                case "Lock":data="M7,11 L7,8 Q7,3 12,3 Q17,3 17,8 L17,11 M6,11 L18,11 Q20,11 20,13 L20,19 Q20,21 18,21 L6,21 Q4,21 4,19 L4,13 Q4,11 6,11 Z M12,14.5 L12,17.5";break;
                case "Heart":data="M12,21 C8,17 2,13 2,8 C2,2 9,1 12,6 C15,1 22,2 22,8 C22,13 16,17 12,21 Z";break;
                case "Close":data="M6,6 L18,18 M18,6 L6,18";break;
                case "Minimize":data="M5,12 L19,12";break;
                case "Collapse":data="M5,9 L12,16 19,9";break;
                case "Maximize":data="M7,5 L17,5 Q19,5 19,7 L19,17 Q19,19 17,19 L7,19 Q5,19 5,17 L5,7 Q5,5 7,5 Z";break;
                case "Restore":data="M8,5 L19,5 19,16 M5,8 L16,8 16,19 5,19 Z";break;
                case "Compare":data="M4.5,5 L19.5,5 Q21,5 21,6.5 L21,17.5 Q21,19 19.5,19 L4.5,19 Q3,19 3,17.5 L3,6.5 Q3,5 4.5,5 Z M12,2.5 L12,21.5 M9,9.5 L6.5,12 L9,14.5 M15,9.5 L17.5,12 L15,14.5";break;
                case "Swap":data="M5,8 L19,8 M15,4 L19,8 L15,12 M19,16 L5,16 M9,12 L5,16 L9,20";break;
                case "Vibrance":data="M12,3 C6.5,3 3,7 3,11.5 C3,16 6.5,20 11,20 C12.5,20 13.2,19 13.2,18 C13.2,16.8 12.4,16.4 12.4,15.2 C12.4,14 13.4,13.2 14.6,13.2 L17,13.2 C19.5,13.2 21,11.6 21,9.4 C21,5.8 17,3 12,3 Z M7.5,11 L7.5,11.1 M10,7.5 L10,7.6 M14.5,7.5 L14.5,7.6";break;
                case "EyeCare":data="M2,14.5 Q12,6 22,14.5 Q12,23 2,14.5 Z M12,11.8 A2.7,2.7 0 1 0 12,17.2 A2.7,2.7 0 1 0 12,11.8 M12,3.2 L12,6 M5.6,5.6 L7.4,7.6 M18.4,5.6 L16.6,7.6";break;
                case "EyeBreak":data="M2.5,9.5 Q12,18 21.5,9.5 M6,13.8 L4.6,16.2 M10.2,15.6 L9.6,18.4 M13.8,15.6 L14.4,18.4 M18,13.8 L19.4,16.2";break;
                case "Eye":data="M2,12 Q12,3.5 22,12 Q12,20.5 2,12 Z M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9";break;
                case "Vision":data="M12,3.2 A5,5 0 1 0 12,13.2 A5,5 0 1 0 12,3.2 M8.2,9.8 A5,5 0 1 0 8.2,19.8 A5,5 0 1 0 8.2,9.8 M15.8,9.8 A5,5 0 1 0 15.8,19.8 A5,5 0 1 0 15.8,9.8";break;
                case "Sun":data="M12,7.5 A4.5,4.5 0 1 0 12,16.5 A4.5,4.5 0 1 0 12,7.5 M12,2 L12,4 M12,20 L12,22 M2,12 L4,12 M20,12 L22,12 M5,5 L6.4,6.4 M17.6,17.6 L19,19 M19,5 L17.6,6.4 M6.4,17.6 L5,19";break;
                case "Dim":data="M20,14.5 A8.5,8.5 0 1 1 9.5,4 A6.6,6.6 0 0 0 20,14.5 Z";break;
                case "Warning":data="M12,3.5 L22,20.5 L2,20.5 Z M12,10 L12,14.5 M12,17.4 L12,17.5";break;
                case "Keyboard":data="M3,6 L21,6 Q22,6 22,7 L22,17 Q22,18 21,18 L3,18 Q2,18 2,17 L2,7 Q2,6 3,6 Z M6,10 L6,10.1 M10,10 L10,10.1 M14,10 L14,10.1 M18,10 L18,10.1 M7.5,14.5 L16.5,14.5";break;
                case "Trash":data="M5,7 L6,21 18,21 19,7 M3,5 L21,5 M9,5 L9,2 15,2 15,5 M10,9 L10,17 M14,9 L14,17";break;
                default:data="M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,11 L12,16.5 M12,7.6 L12,7.7";break;
            }
            // The speaker with one wave is drawn left of the middle of its box (its shape ends at about 18 of 24); moved over, it sits in the middle of its button.
            var path=new System.Windows.Shapes.Path {RenderTransform=name=="VolumeLow"?new TranslateTransform(2.3,0):Transform.Identity,Data=Geometry.Parse(data),Width=24,Height=24,Stretch=name=="Pause"||name=="Play"||name=="Volume"||name=="VolumeLow"||name=="Mute"?Stretch.None:Stretch.Uniform,Stroke=solid&&!rounded?null:Brushes.White,Fill=solid?Brushes.White:null,StrokeThickness=1.65,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
            return new Viewbox{Width=size,Height=size,Child=path};
        }
        public static ResourceDictionary Resources(string fallback)
        {
            var result=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(fallback);
            var additions=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(ModernStyles);
            foreach(object key in additions.Keys)result[key]=additions[key];
            result["AdaptiveControlBrush"]=new SolidColorBrush(Color.FromRgb(32,32,33));
            result["AdaptiveSelectionBrush"]=new SolidColorBrush(Color.FromRgb(48,48,49));
            result["AdaptiveTrackBrush"]=new SolidColorBrush(Color.FromRgb(46,46,47));
            result["AdaptiveSettingBrush"]=new SolidColorBrush(Color.FromRgb(35,35,36));
            result["AdaptiveTooltipBrush"]=new SolidColorBrush(Color.FromRgb(24,24,26));
            var label=new FrameworkElementFactory(typeof(SmoothText));
            label.SetBinding(SmoothText.TextProperty,new System.Windows.Data.Binding());
            result[new DataTemplateKey(typeof(string))]=new DataTemplate(typeof(string)){VisualTree=label};
            return result;
        }
        public const string ModernStyles=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
        <Style TargetType='Button'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Foreground' Value='#EEEEEE'/><Setter Property='Background' Value='{DynamicResource AdaptiveControlBrush}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='MinHeight' Value='36'/><Setter Property='FontSize' Value='14'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
            <Grid><Border x:Name='Surface' Background='{TemplateBinding Background}' CornerRadius='10' Padding='{TemplateBinding Padding}' BorderThickness='{TemplateBinding BorderThickness}' BorderBrush='{TemplateBinding BorderBrush}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><Border x:Name='Glow' Background='#1CFFFFFF' CornerRadius='10' Opacity='0' IsHitTestVisible='False'/></Grid>
            <ControlTemplate.Triggers><EventTrigger RoutedEvent='MouseEnter'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Glow' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:.1'/></Storyboard></BeginStoryboard></EventTrigger><EventTrigger RoutedEvent='MouseLeave'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Glow' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:.16'/></Storyboard></BeginStoryboard></EventTrigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='#505050'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.35'/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='TabAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='Height' Value='28'/><Setter Property='MinHeight' Value='0'/><Setter Property='Padding' Value='10,0,12,0'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='face' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='RailAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='Width' Value='48'/><Setter Property='Height' Value='44'/><Setter Property='MinHeight' Value='0'/><Setter Property='Padding' Value='0'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='SettingsRow' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='Height' Value='32'/><Setter Property='MinHeight' Value='0'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='Transparent' Padding='10,0,10,0'><ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='PlainAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='MinHeight' Value='0'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='FolderExpander' TargetType='Expander'>
            <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Expander'><StackPanel>
                <ToggleButton FocusVisualStyle='{x:Null}' Cursor='Hand' IsChecked='{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent}}'>
                    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>
                        <Border x:Name='Surface' Background='Transparent' CornerRadius='14' Padding='22,17'>
                            <DockPanel>
                                <Path x:Name='Arrow' DockPanel.Dock='Right' Data='M0,0 L7,5 L0,10 Z' Fill='White' Opacity='.8' VerticalAlignment='Center' RenderTransformOrigin='.5,.5'><Path.RenderTransform><RotateTransform Angle='0'/></Path.RenderTransform></Path>
                                <ContentPresenter Content='{Binding Header,RelativeSource={RelativeSource AncestorType=Expander}}' TextElement.FontSize='20' TextElement.FontWeight='Bold' TextElement.Foreground='White' VerticalAlignment='Center'/>
                            </DockPanel>
                        </Border><ControlTemplate.Triggers>
                            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='RenderTransform'><Setter.Value><RotateTransform Angle='90'/></Setter.Value></Setter></Trigger>
                            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#14FFFFFF'/></Trigger>
                            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='Background' Value='#14FFFFFF'/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter x:Name='Body' Visibility='Collapsed' Margin='22,0,22,18'/>
            </StackPanel><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='Body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style x:Key='PrimaryAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Foreground' Value='#111111'/><Setter Property='Background' Value='#E9E9EB'/><Setter Property='FontWeight' Value='SemiBold'/></Style>
        <Style x:Key='TextAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='Padding' Value='0,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter x:Name='Label' Opacity='.82' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'><ContentPresenter.RenderTransform><TranslateTransform/></ContentPresenter.RenderTransform></ContentPresenter></Border><ControlTemplate.Triggers><EventTrigger RoutedEvent='MouseEnter'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Label' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:.1'/><DoubleAnimation Storyboard.TargetName='Label' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='2' Duration='0:0:.14'/></Storyboard></BeginStoryboard></EventTrigger><EventTrigger RoutedEvent='MouseLeave'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Label' Storyboard.TargetProperty='Opacity' To='.82' Duration='0:0:.16'/><DoubleAnimation Storyboard.TargetName='Label' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='0' Duration='0:0:.18'/></Storyboard></BeginStoryboard></EventTrigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='TrashAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#999999'/><Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='#FF6068'/></Trigger></Style.Triggers></Style>
        <Style TargetType='TextBox'><Setter Property='Foreground' Value='White'/><Setter Property='Background' Value='{DynamicResource AdaptiveControlBrush}'/><Setter Property='CaretBrush' Value='White'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='12,10'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='ChromeAction' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid Background='Transparent'><Border x:Name='Hover' Background='{DynamicResource AdaptiveSelectionBrush}' Opacity='0' CornerRadius='9' Margin='1,2'/><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='Tag' Value='Close'><Setter TargetName='Hover' Property='Background' Value='#EE1035'/></Trigger><EventTrigger RoutedEvent='MouseEnter'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Hover' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:.09'/></Storyboard></BeginStoryboard></EventTrigger><EventTrigger RoutedEvent='MouseLeave'><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Hover' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:.12'/></Storyboard></BeginStoryboard></EventTrigger><Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='.8'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='ToolTip'><Setter Property='Foreground' Value='#EEEEEE'/><Setter Property='FontSize' Value='12'/><Setter Property='Background' Value='{DynamicResource AdaptiveTooltipBrush}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='11,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToolTip'><Border Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='ScrollBar'><Setter Property='Width' Value='6'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Opacity='0' Command='ScrollBar.PageUpCommand'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#70595959' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Opacity='0' Command='ScrollBar.PageDownCommand'/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>
        <Style TargetType='Slider'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Height' Value='44'/><Setter Property='IsMoveToPointEnabled' Value='True'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid VerticalAlignment='Center' Height='30'><Border Height='7' Background='{DynamicResource AdaptiveTrackBrush}' CornerRadius='4'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Height='7' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border x:Name='Fill' Background='#EAEAEA' CornerRadius='4' Margin='0,0,-11,0'/><ControlTemplate.Triggers><Trigger Property='ActualWidth' Value='0'><Setter TargetName='Fill' Property='Visibility' Value='Hidden'/></Trigger></ControlTemplate.Triggers></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='24' Height='30'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Width='20' Height='20' Fill='White'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False' Opacity='0'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
        <Style x:Key='CandidateCheck' TargetType='CheckBox'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><Grid Width='18' Height='18'><Border x:Name='Box' Background='#141414' BorderBrush='#707074' BorderThickness='1' CornerRadius='5'/><Path x:Name='Tick' Visibility='Collapsed' Data='M4,9 L7,12 14,5' Stroke='Black' StrokeThickness='1.8' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Box' Property='Background' Value='White'/><Setter TargetName='Box' Property='BorderBrush' Value='White'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.3'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        </ResourceDictionary>";
    }
}
