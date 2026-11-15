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
            int revision=++selectionRevision; var game=Selected;
            Element<TextBlock>("GamePath").Text=game==null?"Папка игры не выбрана":game.GameDirectory;
            Element<TextBlock>("GamePath").ToolTip=game==null?null:game.GameDirectory;
            Element<TextBox>("Destination").Text=game==null?"Выбери сборку, чтобы увидеть путь.":game.Destination(release);
            Element<TextBlock>("GameNote").Text=game==null?"Не нашлась? Выбери папку игры или её latest.log.":
                game.NeedsCacheEvidence?"OpSec меняет кэш. Один раз зайди на сервер, закрой игру и выбери свежий latest.log.":
                game.Isolated?"Кэш найден по логу. Заходи с тем же аккаунтом и по тому же адресу сервера.":
                "Выбери ту сборку, из которой играешь на сервере.";
            RefreshActions();
            var state=await Task.Run(()=>CacheState.Read(game,release.packId,release.sha1));
            if(revision!=selectionRevision)return;
            Element<TextBlock>("PackState").Text=state.Text; Element<TextBlock>("PackState").Foreground=Brush(state.Installed?"Success":"Muted");
            Element<Image>("StateIcon").Source=Icons.Read(state.Installed?"check":"info",Brush(state.Installed?"Success":"Muted"));
            if(latest!=null)await PresentUpdate(latest);
        }
        private async Task Scan()
        {
            SetBusy(true); Message("Ищем установленные сборки…",false);
            try
            {
                var found=demoFolder==null?await Task.Run(()=>GameDiscovery.Scan(release)):new List<GameCandidate>{GameDiscovery.Candidate(demoFolder,release)};
                games.ItemsSource=found; games.SelectedIndex=found.Count==1?0:-1;
                Message(found.Count==0?"Выбери папку своей сборки или её latest.log.":found.Count==1?"Сборка найдена. Проверь, что выбрана нужная.":"Найдено сборок: "+found.Count+". Выбери нужную из списка.",false);
            }
            catch(Exception error){Message("Поиск не завершён. Выбери папку вручную. "+error.Message,true);}
            finally{SetBusy(false);}
            await ShowCandidate();
        }
        private async void ChooseFolder()
        {
            using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Выбери папку игры с options.txt, logs или resourcepacks",ShowNewFolderButton=false})
                if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK) await ImportPath(picker.SelectedPath);
        }
        private async void ChooseLog()
        {
            var picker=new Microsoft.Win32.OpenFileDialog{Title="Свежий latest.log своей сборки",Filter="Логи Minecraft (*.log)|*.log",CheckFileExists=true};
            if(picker.ShowDialog(this)==true) await ImportPath(picker.FileName);
        }
        private async Task ImportPath(string path)
        {
            if(busy)return;
            try
            {
                GameCandidate game;
                if(Directory.Exists(path))
                {
                    if(!PackPaths.LooksLikeGame(path))throw new IOException("Нужна папка с options.txt, logs или resourcepacks.");
                    game=GameDiscovery.Candidate(path,release);
                }
                else
                {
                    game=GameDiscovery.ParseLog(GameDiscovery.ReadLogTail(path),release).LastOrDefault(c=>PackPaths.LooksLikeGame(c.GameDirectory));
                    if(game==null)
                    {
                        string parent=Path.GetDirectoryName(Path.GetDirectoryName(path));
                        if(Path.GetFileName(Path.GetDirectoryName(path)).Equals("logs",StringComparison.OrdinalIgnoreCase)&&PackPaths.LooksLikeGame(parent))game=GameDiscovery.Candidate(parent,release);
                        else throw new IOException("В логе нет подходящего пути на этом компьютере. Выбери папку игры.");
                    }
                }
                PackPaths.RejectLinks(game.CacheDirectory);
                var found=(games.ItemsSource as IEnumerable<GameCandidate> ?? new GameCandidate[0]).Where(c=>!c.GameDirectory.Equals(game.GameDirectory,StringComparison.OrdinalIgnoreCase)).ToList();
                found.Add(game);games.ItemsSource=found;games.SelectedItem=game;
                await ShowCandidate();Message("Сборка выбрана. Закрой Minecraft перед установкой.",false);
            }
            catch(Exception error){Message(error.Message,true);}
        }
        private async Task Install()
        {
            var game=Selected;if(game==null||closed.IsChecked!=true)return;
            SetBusy(true);Element<TextBlock>("InstallLabel").Text="Устанавливаем…";Message("Проверяем встроенный архив…",false);
            try
            {
                if(demoFolder==null)
                {
                    var running=await Task.Run(()=>GameDiscovery.RunningGameDirectories());
                    if(running.Any(p=>p.Equals(game.GameDirectory,StringComparison.OrdinalIgnoreCase)))throw new IOException("Minecraft ещё запущен. Полностью закрой игру и повтори установку.");
                }
                var result=await Task.Run(()=>PackInstall.Install(game,release,PackInstall.EmbeddedPack));
                await ShowCandidate();Message(result.AlreadyPresent?"Актуальный пак уже установлен. Можно заходить на сервер.":"Готово. Запусти Minecraft, зайди на lavabox.fun и разреши серверный пак.",false);
                status.Foreground=Brush("Success");
            }
            catch(Exception error){Message(error.Message,true);}
            finally{SetBusy(false);}
        }
    }
}
