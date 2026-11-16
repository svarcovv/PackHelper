using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LavaBox.PackHelper
{
    public sealed partial class MainWindow
    {
        private void Navigate(string target,bool animate=true)
        {
            if(busy)return;page=target;
            string[] names={"Choose","Locate","Launcher","Log","Prepare","Done","Updates","Help","Support"};
            foreach(string name in names)Element<StackPanel>(name+"Page").Visibility=name==target?Visibility.Visible:Visibility.Collapsed;
            if(target=="Choose"||target=="Prepare"||target=="Done")installPage=target;
            string title,description;
            switch(target)
            {
                case "Choose":title="Выбери сборку Minecraft";description="Установим серверный пак в ту сборку, из которой ты играешь.";break;
                case "Locate":title="Найдём папку игры";description="Проще всего открыть её из Minecraft. Искать по всему диску не нужно.";break;
                case "Launcher":title="Где лежит твоя сборка";description="Открой папку через лаунчер, скопируй её адрес и вернись назад.";break;
                case "Log":title="Выбери лог Minecraft";description="Нужен файл после попытки входа на сервер.";break;
                case "Prepare":title="Всё готово к установке";description="Проверь сборку и полностью закрой Minecraft перед продолжением.";break;
                case "Done":title="Можно заходить на сервер";description="Ресурспак готов для выбранной сборки.";break;
                case "Updates":title="Обновления";description="Актуальное приложение и ресурспак в одном месте.";break;
                case "Support":title="Поддержка и ошибки";description="Выбери удобный способ связаться и приложи сведения о программе.";break;
                default:title="Если что-то не получилось";description="Выбери проблему и выполни шаги ниже.";break;
            }
            Element<TextBlock>("PageTitle").Text=title;Element<TextBlock>("PageDescription").Text=description;
            bool steps=target=="Choose"||target=="Prepare"||target=="Done";Element<Grid>("Steps").Visibility=steps?Visibility.Visible:Visibility.Collapsed;
            int step=target=="Done"?3:target=="Prepare"?2:1;Element<Border>("StepFill").Width=52*step;
            System.Windows.Automation.AutomationProperties.SetName(Element<TextBlock>("PageTitle"),(steps?"Шаг "+step+" из 3. ":"")+title);
            string active=target=="Support"?"Support":target=="Updates"?"NavUpdates":target=="Help"?"NavHelp":"NavInstall";
            foreach(string name in new[]{"NavInstall","NavUpdates","NavHelp","Support"})
            {
                var b=Element<Button>(name);b.Background=Brush(name==active?"Tint":"Sidebar");b.Foreground=Brush(name==active?"Accent":"Muted");
                if(name.StartsWith("Nav",StringComparison.Ordinal))Element<Image>(name+"Icon").Source=Icons.Read(name=="NavInstall"?"download":name=="NavUpdates"?"refresh":"info",Brush(name==active?"Accent":"Muted"));
                if(name=="Support")Element<Image>("SupportIcon").Source=Icons.Read("headset",Brush(name==active?"Accent":"Muted"));
            }
            Element<Button>("Back").Visibility=target=="Choose"||target=="Updates"?Visibility.Collapsed:Visibility.Visible;
            Element<ScrollViewer>("PageScroll").ScrollToTop();Message("",false);RefreshActions();
            if(target=="Support")Element<TextBox>("Diagnostics").Text="PackHelper "+release.appVersion+"\nLavaResurs "+release.packVersion+"\nСборка: "+(Selected==null?"не выбрана":Selected.NeedsCacheEvidence?"нужен лог":Selected.Isolated?"изолированный кэш":"обычный кэш")+"\nПак: "+Element<TextBlock>("PackState").Text;
            // Keep keyboard navigation immediate; animate only occasional pointer transitions.
            var host=Element<Grid>("PageHost");host.BeginAnimation(OpacityProperty,null);host.RenderTransform=Transform.Identity;
            if(animate&&SystemParameters.ClientAreaAnimation&&!keyboardNavigation)
                host.BeginAnimation(OpacityProperty,new DoubleAnimation(0.82,1,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});
        }
        private void GoBack(){Navigate(page=="Launcher"||page=="Log"?"Locate":page=="Prepare"||page=="Locate"?"Choose":installPage);}
        private async Task Next()
        {
            if(page=="Choose")
            {if(Selected!=null&&Selected.NeedsCacheEvidence)Navigate("Log");else{Navigate("Prepare");await ShowCandidate();}}
            else if(page=="Prepare")await Install();else if(page=="Done")Close();
        }
    }
}
