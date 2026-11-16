using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LavaBox.PackHelper
{
    public sealed partial class MainWindow
    {
        private PublicStatusCache publicStatus=new PublicStatusCache();
        private readonly CancellationTokenSource onlineCancel=new CancellationTokenSource();
        private readonly SemaphoreSlim statusSave=new SemaphoreSlim(1,1);
        private string StatusDirectory{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PackHelper");}}
        private async Task LoadOnlineStatus()
        {
            publicStatus=await Task.Run(()=>PublicStatusCache.Read(StatusDirectory,release.packId));
            ShowDownloadCount();
            if(publicStatus.Latest!=null){latest=publicStatus.Latest;await PresentUpdate(latest);}
            await Task.WhenAll(RefreshReleaseStatus(),RefreshDownloadCount());
        }
        private async Task RefreshReleaseStatus()
        {
            if(PublicStatusCache.Fresh(publicStatus.ReleaseCheckedUtc))return;
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(onlineCancel.Token))
                using(var client=new UpdateClient())
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(7));var result=await client.Check(release.packId,timeout.Token);
                    if(result.Manifest!=null&&!checkingUpdates&&!busy)
                    {latest=result.Manifest;publicStatus.Latest=latest;publicStatus.ReleaseCheckedUtc=DateTime.UtcNow;await PresentUpdate(latest);await SavePublicStatus();}
                }
            }
            catch(Exception){/* A failed background check must not interrupt installation. */}
        }
        private async Task RefreshDownloadCount()
        {
            if(PublicStatusCache.Fresh(publicStatus.DownloadsCheckedUtc))return;
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(onlineCancel.Token))
                using(var client=new UpdateClient())
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(7));var result=await client.Statistics(timeout.Token);
                    publicStatus.Downloads=result.Count;publicStatus.DownloadsComplete=result.Complete;publicStatus.DownloadsCheckedUtc=DateTime.UtcNow;
                    ShowDownloadCount();await SavePublicStatus();
                }
            }
            catch(Exception){ShowDownloadCount();}
        }
        private async Task SavePublicStatus()
        {
            await statusSave.WaitAsync();
            try
            {
                var snapshot=new PublicStatusCache{Latest=publicStatus.Latest,ReleaseCheckedUtc=publicStatus.ReleaseCheckedUtc,Downloads=publicStatus.Downloads,DownloadsComplete=publicStatus.DownloadsComplete,DownloadsCheckedUtc=publicStatus.DownloadsCheckedUtc};
                await Task.Run(()=>snapshot.Save(StatusDirectory));
            }
            finally{statusSave.Release();}
        }
        private void ShowDownloadCount()
        {
            var text=Element<TextBlock>("DownloadCount");
            text.Text=(publicStatus.Downloads.HasValue?(publicStatus.DownloadsComplete?"":"≥ ")+publicStatus.Downloads.Value.ToString("N0",CultureInfo.CurrentCulture):"Нет данных");
            System.Windows.Automation.AutomationProperties.SetName(text,"Скачиваний: "+text.Text);
            Element<Border>("DownloadStats").ToolTip=new TextBlock { MaxWidth=240, TextWrapping=TextWrapping.Wrap, Foreground=Brush("Text"), Text="Скачивания приложения с GitHub"+
                (publicStatus.Downloads.HasValue?"\nОбновлено: "+publicStatus.DownloadsCheckedUtc.ToLocalTime().ToString("g",CultureInfo.CurrentCulture):"\nОбновлено: нет данных") };
        }
    }
}
