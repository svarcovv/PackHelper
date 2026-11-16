using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LavaBox.PackHelper
{
    public sealed partial class MainWindow
    {
        private async Task ShowCandidate()
        {
            int revision=++selectionRevision;var game=Selected;closed.IsChecked=false;
            Element<TextBox>("GamePath").Text=game==null?"":game.GameDirectory;
            Element<TextBlock>("SelectedName").Text=game==null?"Сборка не выбрана":game.DisplayName;
            Element<TextBlock>("GameNote").Text=game==null?"Выбери сборку на первом шаге.":game.NeedsCacheEvidence?"Нужен свежий latest.log после входа на сервер.":game.Isolated?"Кэш определён по логу. Используй тот же аккаунт и адрес сервера.":"Личные ресурспаки и настройки игры сохранятся.";
            RefreshActions();var state=await Task.Run(()=>CacheState.Read(game,release.packId,release.sha1));if(revision!=selectionRevision)return;
            Element<TextBlock>("PackState").Text=state.Text;Element<TextBlock>("PackState").Foreground=Brush(state.Installed?"Success":"Muted");
            Element<Image>("StateIcon").Source=Icons.Read(state.Installed?"check":"info",Brush(state.Installed?"Success":"Muted"));
            if(latest!=null)await PresentUpdate(latest);
        }
        private async Task Scan()
        {
            if(scanning||busy)return;var selected=Selected;scanning=true;rescan.IsEnabled=false;RefreshActions();
            progress.IsIndeterminate=true;progress.Visibility=Visibility.Visible;Message("Ищем сборки…",false);
            try
            {
                var found=await Task.Run(()=>demoFolder==null?GameDiscovery.Scan(release):new List<GameCandidate>{GameDiscovery.Candidate(demoFolder,release)});
                var keep=Selected??selected;
                if(keep!=null&&!found.Any(c=>c.GameDirectory.Equals(keep.GameDirectory,StringComparison.OrdinalIgnoreCase))&&PackPaths.LooksLikeGame(keep.GameDirectory))found.Add(keep);
                var previous=(games.ItemsSource as IEnumerable<GameCandidate>??new GameCandidate[0]).ToArray();
                bool same=previous.Length==found.Count&&previous.Zip(found,(a,b)=>a.GameDirectory.Equals(b.GameDirectory,StringComparison.OrdinalIgnoreCase)&&a.CacheDirectory.Equals(b.CacheDirectory,StringComparison.OrdinalIgnoreCase)&&a.NeedsCacheEvidence==b.NeedsCacheEvidence).All(equal=>equal);
                if(!same)
                {games.ItemsSource=found;games.SelectedItem=keep==null?null:found.FirstOrDefault(c=>c.GameDirectory.Equals(keep.GameDirectory,StringComparison.OrdinalIgnoreCase));if(games.SelectedItem==null&&found.Count==1)games.SelectedIndex=0;}
                Element<TextBlock>("FoundCount").Text=found.Count==0?"Сборки не найдены":"Найдено сборок: "+found.Count;
                Element<TextBlock>("NoGames").Visibility=found.Count==0?Visibility.Visible:Visibility.Collapsed;Message("",false);
            }
            catch(Exception){Message("Не удалось завершить поиск. Выбери папку вручную.",true);}
            finally{scanning=false;rescan.IsEnabled=!busy;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;RefreshActions();}await ShowCandidate();
        }
        private async void ChooseFolder()
        {using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Папка игры, resourcepacks или logs",ShowNewFolderButton=false,SelectedPath=Selected==null?Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData):Selected.GameDirectory})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)await ImportPath(picker.SelectedPath);}
        private async void ChooseLog()
        {string initial=Selected==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft","logs"):Path.Combine(Selected.GameDirectory,"logs");if(!Directory.Exists(initial))initial=Selected==null?Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData):Selected.GameDirectory;var picker=new Microsoft.Win32.OpenFileDialog{Title="Выбери latest.log своей сборки",Filter="Логи Minecraft (*.log)|*.log",FileName="latest.log",CheckFileExists=true,InitialDirectory=initial};if(picker.ShowDialog(this)==true)await ImportPath(picker.FileName);}
        private async Task ImportPath(string value)
        {
            if(busy)return;
            try
            {
                string path=GameDiscovery.ResolveInput(value);GameCandidate game;
                if(Directory.Exists(path))game=GameDiscovery.Candidate(path,release);
                else
                {
                    game=GameDiscovery.ParseLog(GameDiscovery.ReadLogTail(path),release).LastOrDefault(c=>PackPaths.LooksLikeGame(c.GameDirectory));
                    if(game==null){string parent=Path.GetDirectoryName(Path.GetDirectoryName(path));if(Path.GetFileName(Path.GetDirectoryName(path)).Equals("logs",StringComparison.OrdinalIgnoreCase)&&PackPaths.LooksLikeGame(parent))game=GameDiscovery.Candidate(parent,release);else throw new IOException("В логе нет пути к сборке на этом компьютере. Выбери папку игры.");}
                }
                PackPaths.RejectLinks(game.CacheDirectory);
                var found=(games.ItemsSource as IEnumerable<GameCandidate>??new GameCandidate[0]).Where(c=>!c.GameDirectory.Equals(game.GameDirectory,StringComparison.OrdinalIgnoreCase)).ToList();found.Add(game);games.ItemsSource=found;games.SelectedItem=game;
                await ShowCandidate();Navigate(game.NeedsCacheEvidence?"Log":"Prepare");
                if(game.NeedsCacheEvidence)Message("Мод меняет кэш. Выбери свежий лог после попытки входа.",false);
            }
            catch(Exception error){Message(error is UnauthorizedAccessException?"Нет доступа к папке. Выбери папку своей сборки.":error.Message,true);}
        }
        private async Task Install()
        {
            var game=Selected;if(game==null||closed.IsChecked!=true)return;
            SetBusy(true);Message("Проверяем и устанавливаем архив…",false);bool success=false;
            try
            {
                if(demoFolder==null){var running=await Task.Run(()=>GameDiscovery.RunningGameDirectories());if(running.Any(p=>p.Equals(game.GameDirectory,StringComparison.OrdinalIgnoreCase)))throw new IOException("Minecraft ещё запущен. Полностью закрой игру и повтори установку.");}
                var result=await Task.Run(()=>PackInstall.Install(game,release,PackInstall.EmbeddedPack));await ShowCandidate();success=true;
                var cleanup=Element<TextBlock>("CacheCleanupNote");cleanup.Text=result.Cleanup.RemovedFiles>0?"Убрано старых файлов пака: "+result.Cleanup.RemovedFiles+" ("+(result.Cleanup.RemovedBytes/1048576.0).ToString("0.0")+" МБ).":"";
                if(result.Cleanup.Incomplete)cleanup.Text+=" Часть старых файлов занята. Новый пак установлен.";
                cleanup.Visibility=String.IsNullOrEmpty(cleanup.Text)?Visibility.Collapsed:Visibility.Visible;
            }
            catch(Exception error){Message("Установка не завершена. "+error.Message,true);}
            finally{SetBusy(false);}if(success)Navigate("Done");
        }
    }
}
