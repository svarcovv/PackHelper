using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LavaBox.PackHelper
{
    // Keep thin edges on physical pixels when Windows uses a fractional scale.
    public sealed class CrispBorder : Border
    {
        private static double Snap(double value,double scale)
        {return Math.Round(value*scale,MidpointRounding.AwayFromZero)/scale;}
        private static double Edge(double value,double scale)
        {return value<=0?0:Math.Max(1,Math.Round(value*scale,MidpointRounding.AwayFromZero))/scale;}
        protected override void OnRender(DrawingContext draw)
        {
            var c=CornerRadius;
            if(c.TopLeft!=c.TopRight||c.TopLeft!=c.BottomLeft||c.TopLeft!=c.BottomRight){base.OnRender(draw);return;}
            var dpi=VisualTreeHelper.GetDpi(this);
            double width=Snap(ActualWidth,dpi.DpiScaleX),height=Snap(ActualHeight,dpi.DpiScaleY);
            if(width<=0||height<=0)return;
            double rx=Math.Min(Snap(c.TopLeft,dpi.DpiScaleX),width/2),ry=Math.Min(Snap(c.TopLeft,dpi.DpiScaleY),height/2);
            var outer=new Rect(0,0,width,height);
            if(Background!=null)draw.DrawRoundedRectangle(Background,null,outer,rx,ry);
            if(BorderBrush==null)return;
            double l=Edge(BorderThickness.Left,dpi.DpiScaleX),r=Edge(BorderThickness.Right,dpi.DpiScaleX);
            double t=Edge(BorderThickness.Top,dpi.DpiScaleY),b=Edge(BorderThickness.Bottom,dpi.DpiScaleY);
            if(l+r+t+b==0)return;
            var ring=new GeometryGroup{FillRule=FillRule.EvenOdd};
            ring.Children.Add(new RectangleGeometry(outer,rx,ry));
            if(width>l+r&&height>t+b)
                ring.Children.Add(new RectangleGeometry(new Rect(l,t,width-l-r,height-t-b),Math.Max(0,rx-Math.Max(l,r)),Math.Max(0,ry-Math.Max(t,b))));
            ring.Freeze();draw.DrawGeometry(BorderBrush,null,ring);
        }
        protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi)
        {base.OnDpiChanged(oldDpi,newDpi);InvalidateVisual();}
    }
}
