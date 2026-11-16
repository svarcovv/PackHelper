using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using LavaBox.PackHelper;

class UiCheck
{
    static MainWindow window;
    static Grid root;
    static string output;
    static int checks;
    static T E<T>(string name) where T:FrameworkElement{return (T)root.FindName(name);}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void CheckButtonSpacing(DependencyObject parent)
    {
        var button=parent as Button;
        if(button!=null&&button.IsVisible&&!String.IsNullOrEmpty(button.Name))
            Check(button.Padding.Left>=10&&button.Padding.Right>=10,button.Name+" has horizontal space for hover and focus");
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)CheckButtonSpacing(VisualTreeHelper.GetChild(parent,i));
    }
    static void Hover(Button button,bool value)
    {
        var key=(DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        button.SetValue(key,value);window.UpdateLayout();
    }
    static void Navigate(string page){typeof(MainWindow).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{page,false,true});window.UpdateLayout();}
    static void Capture(FrameworkElement element,string name,double scale=1)
    {
        element.UpdateLayout();var bmp=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bmp.Render(element);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(Path.Combine(output,name+".png")))png.Save(stream);
    }
    [STAThread] static int Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
        AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        output=args[1];Directory.CreateDirectory(output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        window=new MainWindow(PackRelease.Embedded(),args[0],null);root=(Grid)window.Content;window.Show();
        window.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                await Task.Delay(700);
                foreach(int width in new[]{800,960})
                {
                    window.Width=width;window.Height=width==800?600:750;
                    foreach(string page in new[]{"Choose","Locate","Launcher","Log","Prepare","Done","Updates","Help","Support"})
                    {
                        Navigate(page);await Task.Delay(30);Capture(root,page+"-"+width);
                        CheckButtonSpacing(root);
                        if(width==800)
                        {
                            string name=page=="Choose"?"FindFolder":page=="Locate"?"LauncherHelp":page=="Help"?"HelpSupport":null;
                            if(name!=null){E<Button>(name).BringIntoView();window.UpdateLayout();Hover(E<Button>(name),true);Capture(root,page+"-hover");Hover(E<Button>(name),false);}
                        }
                        Check(E<ScrollViewer>("PageScroll").ActualHeight>100,page+" has usable scroll area at "+width);
                        Check(E<TextBlock>("PageTitle").ActualHeight>=32,page+" title is visible at "+width);
                    }
                }
                Navigate("Prepare");E<CheckBox>("GameClosed").IsChecked=false;
                Check(!E<Button>("InstallPack").IsEnabled,"installation requires game-closed confirmation");
                E<CheckBox>("GameClosed").IsChecked=true;
                Check(E<Button>("InstallPack").IsEnabled,"confirmation enables installation");
                Check(E<CheckBox>("GameClosed").ActualWidth>200&&E<CheckBox>("GameClosed").ActualHeight>=44,"checkbox includes its label in the hit target");
                E<Button>("InstallPack").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(1200);
                Console.WriteLine("Install status: "+E<TextBlock>("Status").Text);
                Check(E<StackPanel>("DonePage").Visibility==Visibility.Visible,"installation reaches the success screen");
                Check(File.Exists(Path.Combine(args[0],"downloads",PackRelease.Embedded().packId,PackRelease.Embedded().sha1)),"real bundled pack installed in fixture");
                Capture(root,"Installed");
                Navigate("Help");Navigate("Log");E<Button>("Back").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(E<StackPanel>("HelpPage").Visibility==Visibility.Visible,"Back returns to the actual previous page");
                Navigate("Updates");
                var foreground=E<Button>("CheckUpdates").Foreground as SolidColorBrush;
                Check(foreground!=null&&foreground.Color==Color.FromRgb(45,23,12),"primary button uses dark text on peach");
                var stats=E<Border>("DownloadStats");var tip=new ToolTip{Content=stats.ToolTip,PlacementTarget=stats};tip.SetResourceReference(FrameworkElement.StyleProperty,typeof(ToolTip));tip.IsOpen=true;await Task.Delay(100);
                var tooltipContent=(TextBlock)tip.Content;
                Check(tooltipContent.Text=="Скачивания приложения с GitHub\nОбновлено: нет данных","statistics tooltip contains only its source and update time");
                Check(tip.ActualWidth<=300&&tip.ActualHeight>=40,"statistics tooltip is compact");
                Check(((SolidColorBrush)tip.Background).Color==Color.FromRgb(27,20,16),"statistics tooltip has the dark theme background");Capture(tip,"Tooltip");tip.IsOpen=false;
                Check(E<TextBlock>("GameClosedLabel").Text=="Minecraft полностью закрыт.","game confirmation has one concise label");
                foreach(string name in new[]{"Rescan","FindFolder","LauncherHelp"})Check(E<Button>(name).Style==window.Resources["Subtle"],name+" is visible as a secondary action");
                foreach(string name in new[]{"NavInstall","NavUpdates","NavHelp","Support"})
                {
                    var focus=E<Button>(name).FocusVisualStyle;
                    var template=(ControlTemplate)((Setter)focus.Setters[0]).Value;
                    var border=(Border)template.LoadContent();
                    Check(border.BorderThickness.Left==1&&border.BorderThickness.Bottom==1,name+" uses an even focus outline without an underline");
                }
                Console.WriteLine("All "+checks+" UI checks passed.");window.Close();app.Shutdown(0);
            }
            catch(Exception error){Console.Error.WriteLine(error);window.Close();app.Shutdown(1);}
        }));return app.Run();
    }
}
