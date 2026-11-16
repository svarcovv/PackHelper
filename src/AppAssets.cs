using System;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace LavaBox.PackHelper
{
    public static class AppAssets
    {
        public static FontFamily Typeface { get { return new FontFamily("Segoe UI"); } }
        public static ImageSource Brand
        {
            get
            {
                var group=new DrawingGroup();
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper.App.svg"))
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                {
                    var doc=XDocument.Load(reader);
                    foreach(var node in doc.Root.Elements())
                    {
                        Geometry geometry=null;
                        if(node.Name.LocalName=="rect")geometry=new RectangleGeometry(new System.Windows.Rect(0,0,36,36),8,8);
                        if(node.Name.LocalName=="path")geometry=Geometry.Parse((string)node.Attribute("d"));
                        if(geometry!=null)group.Children.Add(new GeometryDrawing((Brush)new BrushConverter().ConvertFromString((string)node.Attribute("fill")),null,geometry));
                    }
                }
                group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new System.Windows.Rect(0,0,36,36))));
                group.Freeze();return new DrawingImage(group);
            }
        }
        public static ImageSource Icon
        {
            get { return Brand; }
        }
    }
}
