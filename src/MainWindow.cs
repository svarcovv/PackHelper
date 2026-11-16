using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LavaBox.PackHelper
{
    public sealed partial class MainWindow : Window
    {
        private readonly PackRelease release;
        private readonly string demoFolder, initialFolder;
        private readonly Grid root;
        private readonly ListBox games;
        private readonly CheckBox closed;
        private readonly Button install, checkUpdates, downloadUpdate, folder, log, rescan;
        private readonly TextBlock status;
        private readonly ProgressBar progress;
        private UpdateManifest latest;
        private bool busy, quitting, manualUpdate, keyboardNavigation;
        private int selectionRevision;
        private string page="Choose", installPage="Choose";
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        public MainWindow(PackRelease release,string demoFolder,string initialFolder,string recording)
        {
            this.release=release;this.demoFolder=demoFolder;this.initialFolder=initialFolder;
            using(var theme=Resource("Theme.xaml")) Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme));
            // Application resources are needed while loading the separately embedded view.
            if(Application.Current!=null) Application.Current.Resources.MergedDictionaries.Add(Resources);
            using(var view=Resource("Window.xaml"))root=(Grid)XamlReader.Load(view);
            Content=root;Title="PackHelper "+release.appVersion;Width=960;Height=Math.Min(750,SystemParameters.WorkArea.Height-30);
            MinWidth=800;MinHeight=Math.Min(600,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
            FontFamily=AppAssets.Typeface;FontSize=14;Background=Brush("Canvas");Foreground=Brush("Text");UseLayoutRounding=true;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);Icon=AppAssets.Icon;
            Element<Image>("Brand").Source=AppAssets.Icon;
            games=Element<ListBox>("Games");closed=Element<CheckBox>("GameClosed");install=Element<Button>("InstallPack");
            checkUpdates=Element<Button>("CheckUpdates");downloadUpdate=Element<Button>("DownloadUpdate");folder=Element<Button>("BrowseFolder");log=Element<Button>("BrowseLog");rescan=Element<Button>("Rescan");
            status=Element<TextBlock>("Status");progress=Element<ProgressBar>("Progress");
            Element<TextBlock>("PackTitle").Text="LavaResurs "+release.packVersion;
            Element<TextBlock>("PackDetail").Text="Встроен в приложение · "+(release.size/1048576.0).ToString("0.0")+" МБ";
            Element<TextBlock>("LocalVersions").Text="PackHelper "+release.appVersion+" · LavaResurs "+release.packVersion;
            Element<TextBlock>("AppVersion").Text="Версия "+release.appVersion;
            Element<Image>("DoneIcon").Source=Icons.Read("check",Brush("Success"));
            games.SelectionChanged+=async delegate{await ShowCandidate();};closed.Checked+=delegate{RefreshActions();};closed.Unchecked+=delegate{RefreshActions();};
            install.Click+=async delegate{await Next();};rescan.Click+=async delegate{await Scan();};folder.Click+=delegate{ChooseFolder();};log.Click+=delegate{ChooseLog();};
            checkUpdates.Click+=async delegate{await CheckUpdates();};downloadUpdate.Click+=async delegate{await DownloadUpdate();};
            Bind("NavInstall",()=>Navigate(installPage));Bind("NavUpdates",()=>Navigate("Updates"));Bind("NavHelp",()=>Navigate("Help"));Bind("Support",()=>Navigate("Support"));
            Bind("FindFolder",()=>Navigate("Locate"));Bind("LauncherHelp",()=>Navigate("Launcher"));Bind("LogGuide",()=>Navigate("Log"));Bind("HelpLog",()=>Navigate("Log"));Bind("HelpLocate",()=>Navigate("Locate"));Bind("DoneHelp",()=>Navigate("Help"));
            Bind("Back",GoBack);Bind("Source",()=>OpenUrl(release.sourceUrl));Bind("Discord",()=>OpenUrl(release.discordUrl));Bind("ReportBug",()=>OpenUrl(release.sourceUrl+"/issues/new?template=bug_report.yml"));
            Bind("CopyServer",()=>Copy("lavabox.fun","Адрес сервера скопирован."));Bind("CopyDiagnostics",()=>Copy(Element<TextBox>("Diagnostics").Text,"Сведения скопированы."));
            Element<Button>("UsePath").Click+=async delegate{await ImportPath(Element<TextBox>("FolderInput").Text);};
            Element<TextBox>("FolderInput").KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&!busy){e.Handled=true;await ImportPath(Element<TextBox>("FolderInput").Text);}};
            AllowDrop=true;DragOver+=delegate(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
            Drop+=async delegate(object sender,DragEventArgs e){var paths=e.Data.GetData(DataFormats.FileDrop) as string[];if(!busy&&paths!=null&&paths.Length==1)await ImportPath(paths[0]);};
            SourceInitialized+=delegate{try{int dark=1;DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
            Loaded+=async delegate{await Scan();if(initialFolder!=null)await ImportPath(initialFolder);};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy&&!quitting)e.Cancel=true;};
            PreviewKeyDown+=delegate{keyboardNavigation=true;};PreviewMouseDown+=delegate{keyboardNavigation=false;};
            foreach(string icon in new[]{"NavInstall:download","NavUpdates:refresh","NavHelp:info"}){var pair=icon.Split(':');Element<Image>(pair[0]+"Icon").Source=Icons.Read(pair[1],Brush("Accent"));}
            ConfigureMotion();Navigate("Choose",false);
        }
        private static System.IO.Stream Resource(string name){return Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper."+name);}
        private T Element<T>(string name) where T:FrameworkElement{return (T)root.FindName(name);}
        private Brush Brush(string key){return (Brush)Resources[key];}
        private GameCandidate Selected{get{return games.SelectedItem as GameCandidate;}}
        private void Bind(string name,Action action){Element<Button>(name).Click+=delegate{if(!busy)action();};}
        private void Message(string text,bool error){status.Text=text;status.Visibility=String.IsNullOrEmpty(text)?Visibility.Collapsed:Visibility.Visible;status.Foreground=Brush(error?"Danger":"Muted");}
        private void Copy(string text,string confirmation){try{Clipboard.SetText(text);Message(confirmation,false);}catch(ExternalException){Message("Буфер обмена занят. Попробуй ещё раз.",true);}}
        private void SetBusy(bool value)
        {
            busy=value;games.IsEnabled=closed.IsEnabled=folder.IsEnabled=log.IsEnabled=rescan.IsEnabled=checkUpdates.IsEnabled=downloadUpdate.IsEnabled=!value;
            foreach(string name in new[]{"NavInstall","NavUpdates","NavHelp","Support","Back","FindFolder","UsePath","LauncherHelp","LogGuide","BrowseLog"})Element<Button>(name).IsEnabled=!value;
            progress.Visibility=value?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=value;RefreshActions();
        }
        private void RefreshActions()
        {
            install.Visibility=page=="Choose"||page=="Prepare"||page=="Done"?Visibility.Visible:Visibility.Collapsed;
            install.IsEnabled=!busy&&(page=="Choose"?Selected!=null:page=="Prepare"?Selected!=null&&closed.IsChecked==true:true);
            Element<TextBlock>("InstallLabel").Text=busy&&page=="Prepare"?"Устанавливаем…":page=="Prepare"?"Установить пак":page=="Done"?"Закрыть":"Продолжить";
            System.Windows.Automation.AutomationProperties.SetName(install,Element<TextBlock>("InstallLabel").Text);
        }
        private void ConfigureMotion()
        {
            if(!SystemParameters.ClientAreaAnimation)return;
            foreach(var button in FindButtons(root))
            {
                button.RenderTransform=new ScaleTransform(1,1);
                button.PreviewMouseLeftButtonDown+=delegate{AnimateButton(button,0.98);};button.PreviewMouseLeftButtonUp+=delegate{AnimateButton(button,1);};button.MouseLeave+=delegate{AnimateButton(button,1);};
            }
        }
        private static System.Collections.Generic.IEnumerable<Button> FindButtons(DependencyObject parent)
        {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);var button=child as Button;if(button!=null)yield return button;foreach(var nested in FindButtons(child))yield return nested;}}
        private void AnimateButton(Button button,double scale)
        {var transform=button.RenderTransform as ScaleTransform;if(transform==null||!button.IsEnabled)return;var motion=new DoubleAnimation(scale,TimeSpan.FromMilliseconds(110)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};transform.BeginAnimation(ScaleTransform.ScaleXProperty,motion);transform.BeginAnimation(ScaleTransform.ScaleYProperty,motion);}
        private void OpenUrl(string url){try{Uri uri;if(Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https")Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(Exception){Message("Не удалось открыть ссылку. Попробуй открыть её в браузере.",true);}}
    }
}
