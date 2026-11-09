using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ResolutionSwitcher
{
    internal sealed class ModePickerList : Control
    {
        private readonly object[] items;
        private readonly Func<object,bool> favorite;
        private readonly Action<object> toggleFavorite;
        private readonly Action<object> choose;
        private readonly Action dismiss;
        private readonly Timer scrollTimer=new Timer {Interval=8};
        private readonly System.Diagnostics.Stopwatch scrollClock=System.Diagnostics.Stopwatch.StartNew();
        private float scroll, target;
        private int hovered=-1,selected;
        private const int Row=36,Inset=7;
        public ModePickerList(IEnumerable<object> items,object selected,Func<object,bool> favorite,Action<object> toggleFavorite,Action<object> choose,Action dismiss)
        {
            this.items=items.ToArray();this.selected=Array.IndexOf(this.items,selected);this.favorite=favorite;this.toggleFavorite=toggleFavorite;this.choose=choose;this.dismiss=dismiss;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            BackColor=Color.FromArgb(24,24,24);Font=new Font(Synthwave.FontFamily,10f);TabStop=true;AccessibleName="Choose a display mode";
            scrollTimer.Tick+=(s,e)=>{double elapsed=Math.Min(.05,scrollClock.Elapsed.TotalSeconds);scrollClock.Restart();scroll+=(target-scroll)*(float)(1-Math.Exp(-elapsed/.035));if(Math.Abs(target-scroll)<.1f){scroll=target;scrollTimer.Stop();}Invalidate();};
        }
        public void RevealSelection() {if(selected>=0)scroll=target=Math.Max(0,Math.Min(selected*Row-(Height-Row)/2,items.Length*Row+Inset*2-Height));Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath outer=RoundedPanel.RoundedPath(new Rectangle(0,0,Width-1,Height-1),10))
            using(Pen edge=new Pen(Color.FromArgb(66,66,66)))g.DrawPath(edge,outer);
            GraphicsState state=g.Save();g.SetClip(new Rectangle(4,4,Width-8,Height-8));
            for(int i=Math.Max(0,(int)(scroll/Row));i<items.Length;i++)
            {
                int y=(int)(Inset+i*Row-scroll);if(y>=Height)break;
                if(i==hovered || i==selected)
                using(GraphicsPath row=RoundedPanel.RoundedPath(new Rectangle(6,y,Width-19,Row-2),7))
                using(SolidBrush fill=new SolidBrush(Color.FromArgb(i==hovered?51:37,i==hovered?51:37,i==hovered?51:37)))g.FillPath(fill,row);
                bool canFavorite=toggleFavorite!=null && items[i] is ResolutionChoice;
                Synthwave.DrawText(g,Convert.ToString(items[i]),Font,new Rectangle(17,y,Width-(canFavorite?65:40),Row-2),Color.FromArgb(236,236,236),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                if(canFavorite)
                using(Font star=new Font("Segoe UI Symbol",12f))Synthwave.DrawText(g,favorite!=null&&favorite(items[i])?"★":"☆",star,new Rectangle(Width-47,y,28,Row-2),Color.FromArgb(195,195,195),TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
                else if(i==selected)using(Pen check=new Pen(Color.FromArgb(210,210,210),1.5f))g.DrawLines(check,new[]{new Point(Width-30,y+17),new Point(Width-26,y+21),new Point(Width-19,y+13)});
            }
            g.Restore(state);
            if(items.Length*Row+Inset*2>Height)
            {
                float track=Height-14,thumb=Math.Max(24,track*Height/(items.Length*Row+Inset*2f));
                float top=7+(track-thumb)*scroll/Math.Max(1,items.Length*Row+Inset*2-Height);
                using(Pen pen=new Pen(Color.FromArgb(104,104,104),3)) {pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,Width-6,top,Width-6,top+thumb);}
            }
        }
        private int RowAt(int y) {int index=(int)Math.Floor((y-Inset+scroll)/Row);return index>=0&&index<items.Length?index:-1;}
        protected override void OnMouseMove(MouseEventArgs e) {int next=RowAt(e.Y);if(next!=hovered){hovered=next;Invalidate();}base.OnMouseMove(e);}
        protected override void OnMouseLeave(EventArgs e) {hovered=-1;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Left)return;int index=RowAt(e.Y);if(index<0)return;
            if(e.X>Width-50&&toggleFavorite!=null&&items[index] is ResolutionChoice){toggleFavorite(items[index]);Invalidate();return;}
            choose(items[index]);dismiss();
        }
        protected override void OnMouseWheel(MouseEventArgs e) {target=Math.Max(0,Math.Min(items.Length*Row+Inset*2-Height,target-e.Delta/120f*Row*3));if(!scrollTimer.Enabled)scrollClock.Restart();scrollTimer.Start();base.OnMouseWheel(e);}
        protected override bool IsInputKey(Keys keyData) {return true;}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress=true;
            if(e.KeyCode==Keys.Escape){dismiss();return;}
            if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){if(selected>=0){choose(items[selected]);dismiss();}return;}
            if(e.KeyCode==Keys.Up)selected=Math.Max(0,selected-1);else if(e.KeyCode==Keys.Down)selected=Math.Min(items.Length-1,selected+1);else if(e.KeyCode==Keys.Home)selected=0;else if(e.KeyCode==Keys.End)selected=items.Length-1;else return;
            target=Math.Max(0,Math.Min(items.Length*Row+Inset*2-Height,selected*Row-(Height-Row)/2));if(!scrollTimer.Enabled)scrollClock.Restart();scrollTimer.Start();Invalidate();
        }
        protected override void Dispose(bool disposing) {if(disposing)scrollTimer.Dispose();base.Dispose(disposing);}
    }
    internal sealed class ModePickerPopup : ToolStripDropDown
    {
        public ModePickerPopup() {AutoClose=true;AutoSize=false;Padding=Padding.Empty;BackColor=Color.FromArgb(24,24,24);DropShadowEnabled=true;}
        protected override void OnPaintBackground(PaintEventArgs e) {e.Graphics.Clear(BackColor);}
        protected override void OnPaint(PaintEventArgs e) { }
    }
}
