using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Lucent
{
    // Everything that describes a crosshair, so any look can be drawn, saved and shared as a short code.
    public class CrosshairSettings
    {
        public string Name="Gap cross";
        public bool Top=true,Right=true,Bottom=true,Left=true;
        public int Length=7,Thickness=2,Gap=4;
        public bool Dot=false,SquareDot=false;
        public int DotSize=3;
        public bool Ring=false;
        public int RingRadius=10,RingThickness=2;
        public int Rotation=0,Opacity=100,OffsetX=0,OffsetY=0;
        public string FillColor="#FFFFFF",OutlineColor="#000000";
        public bool Outline=true;
        public int OutlineThickness=1;
        public CrosshairSettings Copy(){return (CrosshairSettings)MemberwiseClone();}
        public bool HasArms {get{return Length>0&&(Top||Right||Bottom||Left);}}
        static int Limit(int value,int min,int max){return Math.Max(min,Math.Min(max,value));}
        // Settings arrive from disk and from pasted codes, so every value is pulled back into range.
        public void Clamp()
        {
            if(string.IsNullOrWhiteSpace(Name))Name="Custom";
            Name=Name.Replace(';',' ').Replace('|',' ').Trim();
            if(Name.Length>24)Name=Name.Substring(0,24);
            Length=Limit(Length,0,40);Thickness=Limit(Thickness,1,10);Gap=Limit(Gap,0,30);
            DotSize=Limit(DotSize,1,12);RingRadius=Limit(RingRadius,3,40);RingThickness=Limit(RingThickness,1,6);
            // The ring was removed. Old saved looks and codes that had one simply load without it.
            Ring=false;
            Rotation=Limit(Rotation,0,45);Opacity=Limit(Opacity,20,100);OffsetX=Limit(OffsetX,-80,80);OffsetY=Limit(OffsetY,-80,80);
            OutlineThickness=Limit(OutlineThickness,1,3);
            FillColor=CrosshairColors.Format(CrosshairColors.Parse(FillColor,System.Drawing.Color.White));
            OutlineColor=CrosshairColors.Format(CrosshairColors.Parse(OutlineColor,System.Drawing.Color.Black));
        }
    }
    public static class CrosshairColors
    {
        // Colors are stored as #RRGGBB; older files wrote WPF's #AARRGGBB.
        public static System.Drawing.Color Parse(string value,System.Drawing.Color fallback)
        {
            if(string.IsNullOrEmpty(value))return fallback;
            string hex=value.Trim().TrimStart('#');int rgb;
            if(hex.Length==8)hex=hex.Substring(2);
            if(hex.Length!=6||!int.TryParse(hex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))return fallback;
            return System.Drawing.Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255);
        }
        public static string Format(System.Drawing.Color color){return "#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");}
    }
    // The ready-made styles. Choosing one changes the shape and keeps the color, outline, opacity and offset.
    public static class CrosshairPresets
    {
        public static readonly string[] Names={"Cross","Gap cross","T","X","Dot","Square"};
        public static CrosshairSettings Create(string name)
        {
            var s=new CrosshairSettings {Name=name};
            switch(name)
            {
                case "Cross":s.Length=8;s.Thickness=2;s.Gap=0;break;
                case "T":s.Top=false;s.Length=8;s.Thickness=2;s.Gap=2;break;
                case "X":s.Rotation=45;s.Length=7;s.Thickness=2;s.Gap=3;break;
                case "Dot":s.Top=s.Right=s.Bottom=s.Left=false;s.Dot=true;s.DotSize=4;break;
                case "Square":s.Top=s.Right=s.Bottom=s.Left=false;s.Dot=true;s.SquareDot=true;s.DotSize=6;break;
                default:s.Name="Gap cross";s.Length=7;s.Thickness=2;s.Gap=4;break;
            }
            return s;
        }
        public static void Apply(CrosshairSettings target,string name)
        {
            var style=Create(name);
            target.Name=style.Name;
            target.Top=style.Top;target.Right=style.Right;target.Bottom=style.Bottom;target.Left=style.Left;
            target.Length=style.Length;target.Thickness=style.Thickness;target.Gap=style.Gap;
            target.Dot=style.Dot;target.SquareDot=style.SquareDot;target.DotSize=style.DotSize;
            target.Ring=style.Ring;target.RingRadius=style.RingRadius;target.RingThickness=style.RingThickness;
            target.Rotation=style.Rotation;
        }
        // Earlier versions had a shape name and one size. The old "positted" style is gone and becomes the plain gap cross.
        public static CrosshairSettings FromLegacy(string shape,double scale,string color,bool outline,string outlineColor)
        {
            double size=double.IsNaN(scale)||double.IsInfinity(scale)?.6:Math.Max(.1,Math.Min(4,scale));
            CrosshairSettings s;
            switch(shape)
            {
                case "Classic":
                    s=Create("Cross");
                    s.Length=Math.Max(1,(int)Math.Round(size*6));s.Thickness=Math.Max(1,2*(int)Math.Round(size*.4)+1);
                    break;
                case "Dot":
                    s=Create("Dot");
                    s.DotSize=Math.Max(2,(int)Math.Round(Math.Max(1.5,size*2+.5)*2));
                    break;
                case "Square":
                    s=Create("Square");
                    s.DotSize=Math.Max(1,(int)Math.Round(size*2))*2+1;
                    break;
                default:
                    s=Create("Gap cross");
                    break;
            }
            s.FillColor=CrosshairColors.Format(CrosshairColors.Parse(color,System.Drawing.Color.White));
            s.OutlineColor=CrosshairColors.Format(CrosshairColors.Parse(outlineColor,System.Drawing.Color.Black));
            s.Outline=outline;
            s.Clamp();
            return s;
        }
    }
    // A crosshair as one line of text, so it can be copied, sent to a friend and pasted back in.
    public static class CrosshairCode
    {
        const string Prefix="LCX1-";
        public static string Encode(CrosshairSettings s)
        {
            Func<bool,string> flag=value=>value?"1":"0";
            string body=string.Join(";",new[]{s.Name,flag(s.Top)+flag(s.Right)+flag(s.Bottom)+flag(s.Left),s.Length.ToString(CultureInfo.InvariantCulture),s.Thickness.ToString(CultureInfo.InvariantCulture),s.Gap.ToString(CultureInfo.InvariantCulture),
                flag(s.Dot)+flag(s.SquareDot),s.DotSize.ToString(CultureInfo.InvariantCulture),flag(s.Ring),s.RingRadius.ToString(CultureInfo.InvariantCulture),s.RingThickness.ToString(CultureInfo.InvariantCulture),
                s.Rotation.ToString(CultureInfo.InvariantCulture),s.Opacity.ToString(CultureInfo.InvariantCulture),s.OffsetX.ToString(CultureInfo.InvariantCulture),s.OffsetY.ToString(CultureInfo.InvariantCulture),
                s.FillColor,flag(s.Outline),s.OutlineColor,s.OutlineThickness.ToString(CultureInfo.InvariantCulture)});
            return Prefix+Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
        }
        // Null when the text is not a crosshair code.
        public static CrosshairSettings Decode(string code)
        {
            if(string.IsNullOrWhiteSpace(code))return null;
            code=code.Trim();
            if(!code.StartsWith(Prefix,StringComparison.Ordinal))return null;
            try
            {
                var parts=Encoding.UTF8.GetString(Convert.FromBase64String(code.Substring(Prefix.Length))).Split(';');
                if(parts.Length!=18||parts[1].Length!=4||parts[5].Length!=2)return null;
                var ints=new int[18];
                foreach(int index in new[]{2,3,4,6,8,9,10,11,12,13,17})if(!int.TryParse(parts[index],NumberStyles.Integer,CultureInfo.InvariantCulture,out ints[index]))return null;
                Func<char,bool> on=c=>c=='1';
                var s=new CrosshairSettings
                {
                    Name=parts[0],Top=on(parts[1][0]),Right=on(parts[1][1]),Bottom=on(parts[1][2]),Left=on(parts[1][3]),
                    Length=ints[2],Thickness=ints[3],Gap=ints[4],Dot=on(parts[5][0]),SquareDot=on(parts[5][1]),DotSize=ints[6],
                    Ring=parts[7]=="1",RingRadius=ints[8],RingThickness=ints[9],Rotation=ints[10],Opacity=ints[11],OffsetX=ints[12],OffsetY=ints[13],
                    FillColor=parts[14],Outline=parts[15]=="1",OutlineColor=parts[16],OutlineThickness=ints[17]
                };
                s.Clamp();
                return s;
            }
            catch(FormatException){return null;}
        }
    }
    // Draws a crosshair pixel for pixel: no blending, so it stays sharp and never mixes with the window's transparent color.
    public static class CrosshairRenderer
    {
        // Pixels from the center to the edge of the drawing, outline included.
        public static int Extent(CrosshairSettings s)
        {
            int reach=(s.Thickness+1)/2;
            if(s.HasArms)reach=Math.Max(reach,s.Gap+s.Length);
            if(s.Dot)reach=Math.Max(reach,(DotPixels(s)+1)/2);
            return reach+(s.Outline?s.OutlineThickness:0)+1;
        }
        // The whole crosshair shares one center. An even thickness or dot has its middle on the line between two pixels, an odd one in the
        // middle of a pixel, so mixing them would leave the dot or the arms half a pixel off. The dot follows the arms; with no arms it sets the center.
        public static bool EvenCenter(CrosshairSettings s)
        {
            return s.HasArms?s.Thickness%2==0:s.Dot&&s.DotSize%2==0;
        }
        // The dot as it is drawn: one pixel larger when that is what it takes to sit on the same center as the arms.
        public static int DotPixels(CrosshairSettings s)
        {
            return (s.DotSize%2==0)==EvenCenter(s)?s.DotSize:s.DotSize+1;
        }
        static bool InBand(int value,int thickness){int low=-(thickness/2);return value>=low&&value<=low+thickness-1;}
        static bool InRun(int value,int gap,int length,bool positive,int shift){return positive?value>=gap&&value<=gap+length-1:value<=-gap-shift&&value>=-(gap+length-1)-shift;}
        // The pixels of the crosshair itself, as offsets from the center pixel.
        public static bool[,] Fill(CrosshairSettings s,int extent)
        {
            int size=extent*2+1;var mask=new bool[size,size];
            double angle=s.Rotation*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle);
            bool even=EvenCenter(s);
            // The middle of the drawing, in pixel offsets: on a pixel for odd sizes, between two for even sizes.
            double middle=even?-.5:0;
            int dotSize=DotPixels(s);
            double dotRadius=dotSize/2d;
            // An even arm is half a pixel off the middle pixel, so its far side starts one pixel earlier to stay symmetric.
            int shift=even?1:0;
            for(int y=-extent;y<=extent;y++)
            for(int x=-extent;x<=extent;x++)
            {
                bool on=false;
                if(s.HasArms)
                {
                    // Sample the unrotated shape, so turned arms have no holes.
                    double px=x-middle,py=y-middle;
                    int u=s.Rotation==0?x:(int)Math.Round(px*cos+py*sin+middle),v=s.Rotation==0?y:(int)Math.Round(-px*sin+py*cos+middle);
                    on=(s.Right&&InRun(u,s.Gap,s.Length,true,0)&&InBand(v,s.Thickness))||(s.Left&&InRun(u,s.Gap,s.Length,false,shift)&&InBand(v,s.Thickness))
                        ||(s.Bottom&&InRun(v,s.Gap,s.Length,true,0)&&InBand(u,s.Thickness))||(s.Top&&InRun(v,s.Gap,s.Length,false,shift)&&InBand(u,s.Thickness));
                }
                if(!on&&s.Dot)
                {
                    double dx=x-middle,dy=y-middle;
                    on=s.SquareDot?Math.Abs(dx)<=dotRadius-.5+1e-9&&Math.Abs(dy)<=dotRadius-.5+1e-9:dx*dx+dy*dy<=dotRadius*dotRadius+(dotSize<=2?.25:0);
                }
                mask[x+extent,y+extent]=on;
            }
            return mask;
        }
        public static void Draw(Graphics g,Rectangle bounds,CrosshairSettings s)
        {
            int extent=Extent(s),size=extent*2+1;
            var fill=Fill(s,extent);
            var state=g.Save();
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Default;
            // The middle of the screen is the line between two pixels on an even-sized display and a pixel on an odd one. An even crosshair
            // sits on the line, an odd one on the pixel to its upper left, so each is as close to the true middle as pixels allow.
            bool even=EvenCenter(s);
            int cx=bounds.Left+(even?bounds.Width/2:(bounds.Width-1)/2)+s.OffsetX,cy=bounds.Top+(even?bounds.Height/2:(bounds.Height-1)/2)+s.OffsetY;
            using(var fillBrush=new SolidBrush(CrosshairColors.Parse(s.FillColor,System.Drawing.Color.White)))
            using(var edgeBrush=new SolidBrush(CrosshairColors.Parse(s.OutlineColor,System.Drawing.Color.Black)))
            {
                int reach=s.Outline?s.OutlineThickness:0;
                for(int y=0;y<size;y++)
                for(int x=0;x<size;x++)
                {
                    if(fill[x,y]){g.FillRectangle(fillBrush,cx+x-extent,cy+y-extent,1,1);continue;}
                    if(reach==0)continue;
                    // An outline pixel is any empty pixel within reach of the crosshair.
                    bool near=false;
                    for(int dy=-reach;dy<=reach&&!near;dy++)
                    for(int dx=-reach;dx<=reach&&!near;dx++)
                    {
                        int nx=x+dx,ny=y+dy;
                        if(nx>=0&&ny>=0&&nx<size&&ny<size&&fill[nx,ny])near=true;
                    }
                    if(near)g.FillRectangle(edgeBrush,cx+x-extent,cy+y-extent,1,1);
                }
            }
            g.Restore(state);
        }
    }
}
