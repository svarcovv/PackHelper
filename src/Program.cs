using System;
using System.IO;
using System.Reflection;
using System.Windows;

[assembly: System.Reflection.AssemblyTitle("PackHelper")]
[assembly: System.Reflection.AssemblyProduct("PackHelper")]
[assembly: System.Reflection.AssemblyDescription("Офлайн-установка серверного ресурспака LavaBox")]
[assembly: System.Reflection.AssemblyCompany("svarcovv")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]

namespace LavaBox.PackHelper
{
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            try
            {
                if (args.Length > 0 && args[0] == "--apply-update") { SelfUpdate.ApplyAndRestart(args, Assembly.GetExecutingAssembly().Location); return; }
                string demo = null, folder = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--demo-folder" && i+1 < args.Length) demo = PackPaths.Normalize(args[++i]);
                    else if (args[i] == "--game-folder" && i+1 < args.Length) folder = PackPaths.Normalize(args[++i]);
                }
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.Run(new MainWindow(PackRelease.Embedded(), demo, folder));
            }
            catch (Exception error) { MessageBox.Show(error.Message, "PackHelper", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

}
