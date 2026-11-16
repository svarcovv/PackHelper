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
                case "Log":title="Найдём кэш по логу";description="Этот способ нужен, если моды меняют место хранения серверного пака.";break;
                case "Prepare":title="Всё готово к установке";description="Проверь сборку и полностью закрой Minecraft перед продолжением.";break;
                case "Done":title="Можно заходить на сервер";description="Ресурспак готов для выбранной сборки.";break;
                case "Updates":title="Обновления";description="Актуальное приложение и ресурспак в одном месте.";break;
                case "Support":title="Поддержка и ошибки";description="Выбери удобный способ связаться и приложи сведения о программе.";break;
                default:title="Если что-то не получилось";description="Начни с подходящей ситуации. Устанавливать мод для PackHelper не нужно.";break;
            }
            Element<TextBlock>("PageTitle").Text=title;Element<TextBlock>("PageDescription").Text=description;
            bool steps=target=="Choose"||target=="Prepare"||target=="Done";Element<StackPanel>("Steps").Visibility=steps?Visibility.Visible:Visibility.Collapsed;
            foreach(string name in new[]{"Choose","Prepare","Done"})Element<TextBlock>("Step"+name).Foreground=Brush(name==target?"Accent":"Muted");
            string active=target=="Support"?"Support":target=="Updates"?"NavUpdates":target=="Help"?"NavHelp":"NavInstall";
            foreach(string name in new[]{"NavInstall","NavUpdates","NavHelp","Support"}){var b=Element<Button>(name);b.Background=Brush(name==active?"Surface":"Sidebar");b.Foreground=Brush(name==active?"Text":"Muted");}
            Element<Button>("Back").Visibility=target=="Choose"||target=="Updates"?Visibility.Collapsed:Visibility.Visible;
            Element<ScrollViewer>("PageScroll").ScrollToTop();Message("",false);RefreshActions();
            if(target=="Support")Element<TextBox>("Diagnostics").Text="PackHelper "+release.appVersion+"\nLavaResurs "+release.packVersion+"\nСборка: "+(Selected==null?"не выбрана":Selected.NeedsCacheEvidence?"нужен лог":Selected.Isolated?"изолированный кэш":"обычный кэш")+"\nПак: "+Element<TextBlock>("PackState").Text;
            // Keep keyboard navigation immediate; animate only occasional pointer transitions.
            var host=Element<Grid>("PageHost");host.BeginAnimation(OpacityProperty,null);host.RenderTransform=new TranslateTransform();
            if(animate&&SystemParameters.ClientAreaAnimation&&!keyboardNavigation)
            {host.BeginAnimation(OpacityProperty,new DoubleAnimation(0.55,1,TimeSpan.FromMilliseconds(180)));((TranslateTransform)host.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(7,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
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
