using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;

namespace PackHelperBuilder
{
    static class ReleaseWindow
    {
        [STAThread] static int Main(string[] args)
        {
            try
            {
                if(args.Length==6&&args[0]=="--build"){ReleaseBuild.Run(args[1],args[2],args[3],args[4],args[5]);return 0;}
                if(args.Length!=1)throw new Exception("Запусти release.cmd из папки исходников PackHelper.");
                string project=Path.GetFullPath(args[0]);
                string markup=@"<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Title='Новый выпуск PackHelper' Width='590' Height='420' ResizeMode='NoResize' WindowStartupLocation='CenterScreen' Background='#202226' FontFamily='Segoe UI' FontSize='14' Foreground='#F0F0F1'><StackPanel Margin='25'>
<TextBlock Text='Собрать новый выпуск' FontSize='23' FontWeight='SemiBold' Foreground='#F0F0F1' Margin='0,0,0,20'/>
<TextBlock Text='Точный ZIP ресурспака'/><DockPanel Margin='0,7,0,18'><Button x:Name='Choose' Content='Выбрать' DockPanel.Dock='Right' Padding='13,6' Margin='8,0,0,0'/><TextBox x:Name='Pack' IsReadOnly='True' Padding='7'/></DockPanel>
<Grid Margin='0,0,0,22'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions><StackPanel Margin='0,0,10,0'><TextBlock Text='Версия приложения'/><TextBox x:Name='App' Text='1.4.3' Padding='8' Margin='0,7,0,0'/></StackPanel><StackPanel Grid.Column='1' Margin='10,0,0,0'><TextBlock Text='Версия ресурспака'/><TextBox x:Name='Version' Padding='8' Margin='0,7,0,0'/></StackPanel></Grid>
<Button x:Name='Build' Content='Собрать выпуск' Background='#F29B65' Foreground='#241A14' Padding='13' BorderThickness='0'/><TextBlock x:Name='Status' Text='Готовые файлы появятся в папке dist.' TextWrapping='Wrap' Foreground='#ACB0B8' Margin='0,15,0,0'/>
</StackPanel></Window>";
                Window window;using(var reader=XmlReader.Create(new StringReader(markup)))window=(Window)XamlReader.Load(reader);
                var pack=(TextBox)window.FindName("Pack");var app=(TextBox)window.FindName("App");var version=(TextBox)window.FindName("Version");
                string bundled=Path.Combine(project,"resourcepack");if(Directory.Exists(bundled)){var archives=Directory.GetFiles(bundled,"*.zip");if(archives.Length==1){pack.Text=archives[0];var match=System.Text.RegularExpressions.Regex.Match(Path.GetFileName(pack.Text),@"\d+\.\d+\.\d+");if(match.Success)version.Text=match.Value;}}
                var status=(TextBlock)window.FindName("Status");var build=(Button)window.FindName("Build");bool busy=false;
                ((Button)window.FindName("Choose")).Click+=delegate{var picker=new Microsoft.Win32.OpenFileDialog{Filter="Ресурспак (*.zip)|*.zip"};if(picker.ShowDialog(window)==true){pack.Text=picker.FileName;var match=System.Text.RegularExpressions.Regex.Match(picker.SafeFileName,@"\d+\.\d+\.\d+");if(match.Success)version.Text=match.Value;}};
                build.Click+=async delegate
                {
                    busy=true;build.IsEnabled=false;status.Text="Собираем выпуск…";
                    try{string path=pack.Text,av=app.Text,pv=version.Text;string output=await Task.Run(()=>ReleaseBuild.Run(project,path,av,pv,Path.Combine(project,"dist")));status.Text="Готово. Приложи EXE, update.json, pack-release.json и SHA256SUMS.txt к выпуску с тегом v"+av+".";Process.Start(new ProcessStartInfo("explorer.exe","\""+output+"\""){UseShellExecute=true});}
                    catch(Exception error){status.Text=error.Message;}finally{busy=false;build.IsEnabled=true;}
                };
                window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy)e.Cancel=true;};
                new Application().Run(window);return 0;
            }
            catch(Exception error){if(args.Length>0&&args[0]=="--build")Console.Error.WriteLine(error.Message);else MessageBox.Show(error.Message,"PackHelper");return 1;}
        }
    }
}
