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
        private bool busy, quitting, manualUpdate, keyboardNavigation, checkingUpdates, scanning;
        private int selectionRevision;
        private string page="Choose", installPage="Choose";
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        public MainWindow(PackRelease release,string demoFolder,string initialFolder,string recording)
        {
            this.release=release;this.demoFolder=demoFolder;this.initialFolder=initialFolder;
            Resources.MergedDictionaries.Add((ResourceDictionary)LoadXaml("Theme.xaml"));
            // Application resources are needed while loading the separately embedded view.
            if(Application.Current!=null) Application.Current.Resources.MergedDictionaries.Add(Resources);
            root=(Grid)LoadXaml("Window.xaml");
            Content=root;Title="PackHelper "+release.appVersion;Width=960;Height=Math.Min(750,SystemParameters.WorkArea.Height-30);
            MinWidth=800;MinHeight=Math.Min(600,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
            FontFamily=AppAssets.Typeface;FontSize=14;Background=Brush("Canvas");Foreground=Brush("Text");UseLayoutRounding=true;
            SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this,TextRenderingMode.Auto);TextOptions.SetTextHintingMode(this,TextHintingMode.Fixed);RenderOptions.SetClearTypeHint(this,ClearTypeHint.Enabled);Icon=AppAssets.Icon;
            Element<Image>("Brand").Source=AppAssets.Brand;
            Element<Image>("DownloadStatsIcon").Source=Icons.Read("download",Brush("Muted"));
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
            Bind("Telegram",()=>OpenUrl(release.developerUrl));
            Bind("HelpChoose",()=>Navigate("Choose"));Bind("HelpUpdates",()=>Navigate("Updates"));Bind("HelpSupport",()=>Navigate("Support"));
            Bind("CopyServer",()=>Copy("lavabox.fun","Адрес сервера скопирован."));Bind("CopyDiagnostics",()=>Copy(Element<TextBox>("Diagnostics").Text,"Сведения скопированы."));
            Element<Button>("UsePath").Click+=async delegate{await ImportPath(Element<TextBox>("FolderInput").Text);};
            Element<TextBox>("FolderInput").KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&!busy){e.Handled=true;await ImportPath(Element<TextBox>("FolderInput").Text);}};
            AllowDrop=true;DragOver+=delegate(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
            Drop+=async delegate(object sender,DragEventArgs e){var paths=e.Data.GetData(DataFormats.FileDrop) as string[];if(!busy&&paths!=null&&paths.Length==1)await ImportPath(paths[0]);};
            SourceInitialized+=delegate{try{int dark=1;DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
            Loaded+=async delegate{await Scan();if(initialFolder!=null)await ImportPath(initialFolder);await LoadOnlineStatus();};
            Closed+=delegate{onlineCancel.Cancel();};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy&&!quitting)e.Cancel=true;};
            PreviewKeyDown+=delegate{keyboardNavigation=true;};PreviewMouseDown+=delegate{keyboardNavigation=false;};
            foreach(string icon in new[]{"NavInstall:download","NavUpdates:refresh","NavHelp:info"}){var pair=icon.Split(':');Element<Image>(pair[0]+"Icon").Source=Icons.Read(pair[1],Brush("Accent"));}
            Element<Image>("SupportIcon").Source=Icons.Read("headset",Brush("Muted"));
            Element<Image>("SourceIcon").Source=Icons.Read("github",Brush("Muted"));
            Element<Image>("TelegramIcon").Source=Icons.Read("telegram",Brushes.White);
            Element<Image>("DiscordIcon").Source=Icons.Read("discord",Brushes.White);
            Navigate("Choose",false);
        }
        private static System.IO.Stream Resource(string name){return Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper."+name);}
        private static object LoadXaml(string name)
        {
            var context=new ParserContext{XamlTypeMapper=new XamlTypeMapper(new string[0])};
            context.XamlTypeMapper.AddMappingProcessingInstruction("urn:packhelper:controls","LavaBox.PackHelper",Assembly.GetExecutingAssembly().GetName().Name);
            using(var stream=Resource(name))return XamlReader.Load(stream,context);
        }
        private T Element<T>(string name) where T:FrameworkElement{return (T)root.FindName(name);}
        private Brush Brush(string key){return (Brush)Resources[key];}
        private GameCandidate Selected{get{return games.SelectedItem as GameCandidate;}}
        private void Bind(string name,Action action){Element<Button>(name).Click+=delegate{if(!busy)action();};}
        private void Message(string text,bool error){status.Text=text;status.Visibility=String.IsNullOrEmpty(text)?Visibility.Collapsed:Visibility.Visible;status.Foreground=Brush(error?"Danger":"Muted");}
        private void Copy(string text,string confirmation){try{Clipboard.SetText(text);Message(confirmation,false);}catch(ExternalException){Message("Буфер обмена занят. Попробуй ещё раз.",true);}}
        private void SetBusy(bool value)
        {
            busy=value;games.IsEnabled=closed.IsEnabled=folder.IsEnabled=log.IsEnabled=rescan.IsEnabled=checkUpdates.IsEnabled=downloadUpdate.IsEnabled=!value;
            checkUpdates.IsEnabled=!value&&!checkingUpdates;rescan.IsEnabled=!value&&!scanning;
            foreach(string name in new[]{"NavInstall","NavUpdates","NavHelp","Support","Back","FindFolder","UsePath","LauncherHelp","LogGuide","BrowseLog"})Element<Button>(name).IsEnabled=!value;
            progress.Visibility=value?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=value;RefreshActions();
        }
        private void RefreshActions()
        {
            install.Visibility=page=="Choose"||page=="Prepare"||page=="Done"?Visibility.Visible:Visibility.Collapsed;
            install.IsEnabled=!busy&&!scanning&&(page=="Choose"?Selected!=null:page=="Prepare"?Selected!=null&&closed.IsChecked==true:true);
            Element<TextBlock>("InstallLabel").Text=busy&&page=="Prepare"?"Устанавливаем…":page=="Prepare"?"Установить пак":page=="Done"?"Закрыть":"Продолжить";
            System.Windows.Automation.AutomationProperties.SetName(install,Element<TextBlock>("InstallLabel").Text);
        }
        private void OpenUrl(string url){try{Uri uri;if(Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https")Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(Exception){Message("Не удалось открыть ссылку. Попробуй открыть её в браузере.",true);}}
    }
}
