using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LavaBox.PackHelper
{
    public static class AppAssets
    {
        public static FontFamily Typeface
        {get{return new FontFamily(new Uri("pack://application:,,,/"),"/"+Assembly.GetExecutingAssembly().GetName().Name+";component/assets/fonts/#Manrope");}}
        public static ImageSource Icon
        {
            get
            {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper.App.ico"))
                {var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return decoder.Frames.OrderByDescending(f=>f.PixelWidth).First();}
            }
        }
    }
}
