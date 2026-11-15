using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LavaBox.PackHelper
{
    public sealed class DemoRecorder
    {
        private sealed class Frame { public BitmapSource Bitmap; public double Time; public int Index; }
        private readonly BlockingCollection<Frame> frames=new BlockingCollection<Frame>(6);
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private readonly DispatcherTimer timer;
        private readonly Task writer;
        private readonly FrameworkElement surface;
        private int index;
        private DemoRecorder(FrameworkElement surface,string directory)
        {
            this.surface=surface;Directory.CreateDirectory(directory);
            writer=Task.Run(()=>
            {
                using(var timing=new StreamWriter(Path.Combine(directory,"frames.csv"),false))
                foreach(var frame in frames.GetConsumingEnumerable())
                {
                    string name=frame.Index.ToString("D6")+".png";
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame.Bitmap));
                    using(var file=File.Create(Path.Combine(directory,name)))encoder.Save(file);
                    timing.WriteLine(name+","+frame.Time.ToString("F6",CultureInfo.InvariantCulture));
                }
            });
            timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(50)};
            timer.Tick+=delegate{Capture();};timer.Start();
            Window.GetWindow(surface).Closed+=delegate{timer.Stop();frames.CompleteAdding();writer.Wait(5000);};
        }
        private void Capture()
        {
            int width=(int)Math.Ceiling(surface.ActualWidth),height=(int)Math.Ceiling(surface.ActualHeight);
            if(width<1||height<1||frames.Count>=5)return;
            var content=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);content.Render(surface);
            var visual=new DrawingVisual();
            using(var draw=visual.RenderOpen())
            {
                draw.DrawImage(content,new Rect(0,0,width,height));
                Point cursor=Mouse.GetPosition(surface);
                if(cursor.X>=0&&cursor.Y>=0&&cursor.X<width&&cursor.Y<height)
                {
                    if(Mouse.LeftButton==MouseButtonState.Pressed)draw.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromRgb(185,78,32)),3),cursor,18,18);
                    draw.PushTransform(new TranslateTransform(cursor.X,cursor.Y));
                    draw.DrawGeometry(Brushes.White,new Pen(new SolidColorBrush(Color.FromRgb(57,41,30)),1.5),Geometry.Parse("M0,0 L0,22 L6,17 L11,26 L15,24 L10,15 L20,15 Z"));draw.Pop();
                }
            }
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
            frames.TryAdd(new Frame{Bitmap=bitmap,Time=clock.Elapsed.TotalSeconds,Index=index++});
        }
        public static void Start(FrameworkElement surface,string directory){new DemoRecorder(surface,directory);}
    }
}
