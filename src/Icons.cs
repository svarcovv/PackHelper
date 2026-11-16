using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace LavaBox.PackHelper
{
    public static class Icons
    {
        private static double Number(XElement node, string name, double fallback)
        { var attr = node.Attribute(name); return attr == null ? fallback : Double.Parse(attr.Value, CultureInfo.InvariantCulture); }
        public static ImageSource Read(string name, Brush brush)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper.Icons." + name + ".svg"))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var doc = XDocument.Load(reader); var drawing = new DrawingGroup();
                var pen = new Pen(brush, Number(doc.Root,"stroke-width",2)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                foreach (var node in doc.Root.Elements())
                {
                    if ((string)node.Attribute("stroke") == "none" && (string)node.Attribute("fill") == "none") continue;
                    Geometry shape = null; string kind = node.Name.LocalName;
                    if (kind == "path") shape = Geometry.Parse((string)node.Attribute("d"));
                    if (kind == "circle") { double r = Number(node, "r", 0); shape = new EllipseGeometry(new Point(Number(node,"cx",0),Number(node,"cy",0)),r,r); }
                    if (kind == "rect") shape = new RectangleGeometry(new Rect(Number(node,"x",0),Number(node,"y",0),Number(node,"width",0),Number(node,"height",0)),Number(node,"rx",0),Number(node,"rx",0));
                    if (kind == "line") shape = new LineGeometry(new Point(Number(node,"x1",0),Number(node,"y1",0)),new Point(Number(node,"x2",0),Number(node,"y2",0)));
                    if (kind == "polyline" || kind == "polygon") shape = Geometry.Parse("M " + (string)node.Attribute("points") + (kind == "polygon" ? " Z" : ""));
                    if (shape != null) drawing.Children.Add(new GeometryDrawing(null, pen, shape));
                }
                drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));
                drawing.Freeze(); return new DrawingImage(drawing);
            }
        }
    }
}
