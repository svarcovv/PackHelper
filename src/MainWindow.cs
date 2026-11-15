using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
        private readonly ComboBox games;
        private readonly CheckBox closed;
        private readonly Button install, checkUpdates, downloadUpdate, folder, log, rescan;
        private readonly TextBlock status;
        private readonly ProgressBar progress;
        private UpdateManifest latest;
        private bool busy, quitting, manualUpdate;
        private int selectionRevision;

        public MainWindow(PackRelease release, string demoFolder, string initialFolder, string recording)
        {
            this.release = release; this.demoFolder = demoFolder; this.initialFolder = initialFolder;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper.Window.xaml")) root = (Grid)XamlReader.Load(stream);
            Content = root; Title = "PackHelper " + release.appVersion; Width = 840; Height = Math.Min(830, SystemParameters.WorkArea.Height - 35);
            MinWidth = 720; MinHeight = Math.Min(650, Height); WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 14; Background = Brush("Canvas"); UseLayoutRounding = true;
            Icon = Icons.Read("package", Brush("Accent"));
            games = Element<ComboBox>("Games"); closed = Element<CheckBox>("GameClosed"); install = Element<Button>("InstallPack");
            checkUpdates = Element<Button>("CheckUpdates"); downloadUpdate = Element<Button>("DownloadUpdate");
            folder = Element<Button>("BrowseFolder"); log = Element<Button>("BrowseLog"); rescan = Element<Button>("Rescan");
            status = Element<TextBlock>("Status"); progress = Element<ProgressBar>("Progress");
            foreach (string pair in new[]{"HeaderIcon:package","PackIcon:package","FolderIcon:folder","LogIcon:file","RefreshIcon:refresh","GitHubIcon:github"})
            { string[] item = pair.Split(':'); Element<Image>(item[0]).Source = Icons.Read(item[1], Brush(item[0] == "PackIcon" || item[0] == "HeaderIcon" ? "Accent" : "Muted")); }
            Element<Image>("InstallIcon").Source = Icons.Read("download", Brushes.White);
            Element<TextBlock>("PackTitle").Text = "LavaResurs " + release.packVersion;
            Element<TextBlock>("PackDetail").Text = "Встроен в приложение · " + (release.size/1048576.0).ToString("0.0") + " МБ";
            Element<TextBlock>("AppVersion").Text = demoFolder == null ? "Версия " + release.appVersion : "Пример установки";
            games.SelectionChanged += async delegate { await ShowCandidate(); };
            closed.Checked += delegate { RefreshActions(); }; closed.Unchecked += delegate { RefreshActions(); };
            install.Click += async delegate { if (Selected != null && Selected.NeedsCacheEvidence) ChooseLog(); else await Install(); };
            rescan.Click += async delegate { await Scan(); }; folder.Click += delegate { ChooseFolder(); }; log.Click += delegate { ChooseLog(); };
            checkUpdates.Click += async delegate { await CheckUpdates(); }; downloadUpdate.Click += async delegate { await DownloadUpdate(); };
            Element<Button>("Source").Click += delegate { OpenUrl(release.sourceUrl); }; Element<Button>("Help").Click += delegate { OpenUrl(release.discordUrl); };
            AllowDrop = true; DragOver += delegate(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Drop += async delegate(object sender, DragEventArgs e) { var paths=e.Data.GetData(DataFormats.FileDrop) as string[]; if (!busy && paths != null && paths.Length==1) await ImportPath(paths[0]); };
            Loaded += async delegate { await Scan(); if (initialFolder != null) await ImportPath(initialFolder); if (recording != null && demoFolder != null) DemoRecorder.Start(root, recording); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (busy && !quitting) e.Cancel=true; };
            ConfigureMotion(); RefreshActions();
        }
        private T Element<T>(string name) where T: FrameworkElement { return (T)root.FindName(name); }
        private Brush Brush(string key) { return (Brush)root.FindResource(key); }
        private GameCandidate Selected { get { return games.SelectedItem as GameCandidate; } }
        private void Message(string text, bool error)
        {
            status.Text=text; status.Foreground=Brush(error ? "Danger" : "Muted");
            if (SystemParameters.ClientAreaAnimation) status.BeginAnimation(OpacityProperty,new DoubleAnimation(0.65,1,TimeSpan.FromMilliseconds(180)));
        }
        private void SetBusy(bool value)
        {
            busy=value; games.IsEnabled=closed.IsEnabled=folder.IsEnabled=log.IsEnabled=rescan.IsEnabled=checkUpdates.IsEnabled=downloadUpdate.IsEnabled=!value;
            progress.Visibility=value?Visibility.Visible:Visibility.Collapsed; progress.IsIndeterminate=value; RefreshActions();
        }
        private void RefreshActions()
        {
            install.IsEnabled=!busy && Selected != null && (closed.IsChecked==true || Selected.NeedsCacheEvidence);
            if (Selected != null && Selected.NeedsCacheEvidence) Element<TextBlock>("InstallLabel").Text="Выбрать свежий лог";
            else if (!busy) Element<TextBlock>("InstallLabel").Text="Установить ресурспак";
            System.Windows.Automation.AutomationProperties.SetName(install,Element<TextBlock>("InstallLabel").Text);
        }
        private void ConfigureMotion()
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            foreach (string name in new[]{"InstallPack","BrowseFolder","BrowseLog","CheckUpdates","DownloadUpdate","Rescan","Source","Help"})
            {
                var button=Element<Button>(name); button.RenderTransform=new ScaleTransform(1,1);
                button.PreviewMouseLeftButtonDown+=delegate { AnimateButton(button,0.96); };
                button.PreviewMouseLeftButtonUp+=delegate { AnimateButton(button,1); };
                button.MouseLeave+=delegate { AnimateButton(button,1); };
            }
        }
        private void AnimateButton(Button button,double scale)
        {
            var transform=button.RenderTransform as ScaleTransform; if(transform==null || !button.IsEnabled)return;
            var motion=new DoubleAnimation(scale,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
            transform.BeginAnimation(ScaleTransform.ScaleXProperty,motion); transform.BeginAnimation(ScaleTransform.ScaleYProperty,motion);
        }
        private void OpenUrl(string url)
        {
            try { Uri uri; if(Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.Scheme=="https") Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true}); }
            catch(Exception error){Message("Не удалось открыть ссылку: "+error.Message,true);}
        }
    }
}
