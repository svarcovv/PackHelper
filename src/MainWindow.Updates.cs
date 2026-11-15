using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LavaBox.PackHelper
{
    public sealed partial class MainWindow
    {
        private async Task CheckUpdates()
        {
            latest=null;manualUpdate=false;
            SetBusy(true); Element<Border>("UpdatePanel").Visibility=Visibility.Visible;
            Element<TextBlock>("UpdateTitle").Text="Проверяем версии…";
            Element<TextBlock>("UpdateDetail").Text="Сравниваем выпуск GitHub и файл в выбранной сборке.";
            downloadUpdate.Visibility=Visibility.Collapsed;
            try
            {
                using(var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                using(var client=new UpdateClient())
                {
                    var result=await client.Check(release.packId,cancel.Token);
                    manualUpdate=result.ManualDownload;latest=result.Manifest;
                    if(result.NoPublishedRelease)
                    {
                        Element<TextBlock>("UpdateTitle").Text="Пока нет опубликованных обновлений";
                        Element<TextBlock>("UpdateDetail").Text="Встроенный пак "+release.packVersion+" доступен для установки.";
                    }
                    else if(result.ManualDownload)
                    {
                        Element<TextBlock>("UpdateTitle").Text="Свежий выпуск доступен на GitHub";
                        Element<TextBlock>("UpdateDetail").Text="Этот выпуск нужно скачать вручную.";
                        downloadUpdate.Content="Открыть выпуск";downloadUpdate.Visibility=Visibility.Visible;
                    }
                    else await PresentUpdate(result.Manifest);
                }
                await ShowCandidate();Message("Проверка завершена.",false);
            }
            catch(Exception error)
            {
                Element<TextBlock>("UpdateTitle").Text="Не удалось проверить обновления";
                Element<TextBlock>("UpdateDetail").Text="GitHub недоступен или не ответил вовремя. Встроенный пак "+release.packVersion+" можно установить офлайн.";
                Message(error is System.Net.Http.HttpRequestException || error is TaskCanceledException ? "Проверь подключение и попробуй ещё раз." : error.Message,true);
            }
            finally{SetBusy(false);}
        }

        private async Task PresentUpdate(UpdateManifest update)
        {
            var game=Selected;var state=await Task.Run(()=>CacheState.Read(game,update.packId,update.packSha1));
            if(game!=Selected)return;
            bool newer=UpdateManifest.VersionOf(update.appVersion)>UpdateManifest.VersionOf(release.appVersion);
            bool same=UpdateManifest.VersionOf(update.appVersion)==UpdateManifest.VersionOf(release.appVersion);
            bool packChanged=update.packSha1!=release.sha1;
            Element<Border>("UpdatePanel").Visibility=Visibility.Visible;
            Element<TextBlock>("UpdateTitle").Text=newer?"Доступен PackHelper "+update.appVersion:
                packChanged&&same?"Доступен новый ресурспак "+update.packVersion:"Установлена актуальная версия приложения";
            Element<TextBlock>("UpdateDetail").Text="PackHelper: "+release.appVersion+". Последний выпуск: "+update.appVersion+".\n"+
                "Пак в приложении: "+release.packVersion+". В последнем выпуске: "+update.packVersion+".\n"+
                (game==null?"Выбери сборку, чтобы проверить установленный пак.":state.Installed?"В сборке установлен пак из последнего выпуска.":"Пак из последнего выпуска в этой сборке ещё не установлен.");
            downloadUpdate.Content="Обновить приложение";
            downloadUpdate.Visibility=newer||(same&&packChanged)?Visibility.Visible:Visibility.Collapsed;
        }

        private async Task DownloadUpdate()
        {
            if(manualUpdate){OpenUrl(release.sourceUrl+"/releases/latest");return;}
            if(latest==null)return;
            SetBusy(true);progress.IsIndeterminate=false;progress.Value=0;
            try
            {
                Message("Скачиваем PackHelper "+latest.appVersion+" с ресурспаком "+latest.packVersion+"…",false);
                string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PackHelper","Updates");
                string downloaded;
                using(var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                using(var client=new UpdateClient())
                    downloaded=await client.Download(latest,release.packId,cache,new Progress<int>(p=>{progress.Value=p;}),cancel.Token);
                string current=Assembly.GetExecutingAssembly().Location;
                string oldHash=PackInstall.Hash(current,true);
                string arguments="--apply-update "+Process.GetCurrentProcess().Id+" "+SelfUpdate.Quote(current)+" "+oldHash+" "+latest.exeSha256+" "+SelfUpdate.Quote(Selected==null?"":Selected.GameDirectory);
                Process.Start(new ProcessStartInfo(downloaded,arguments){UseShellExecute=false,CreateNoWindow=true});
                quitting=true;Application.Current.Shutdown();
            }
            catch(Exception error){Message("Не удалось обновить приложение. "+error.Message+" Можно скачать выпуск с GitHub.",true);}
            finally{if(!quitting)SetBusy(false);}
        }
    }
}
