using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("LavaBox PackHelper")]
[assembly: System.Reflection.AssemblyDescription("Офлайн-установка серверного ресурспака LavaBox")]
[assembly: System.Reflection.AssemblyCompany("svarcovv")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace LavaBox.PackHelper
{
    static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string demo = null;
                if (args.Length == 2 && args[0] == "--demo-folder") demo = PackPaths.Normalize(args[1]);
                Application.Run(new MainForm(PackRelease.Embedded(), demo));
            }
            catch (Exception error) { MessageBox.Show(error.Message, "PackHelper - не удалось открыть выпуск", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

}
