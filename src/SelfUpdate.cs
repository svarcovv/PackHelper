using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace LavaBox.PackHelper
{
    public static class SelfUpdate
    {
        public static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') result.Append('\\', slashes * 2 + 1).Append('"');
                else result.Append('\\', slashes).Append(ch);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        public static void Replace(string source, string target, string previousHash, string newHash)
        {
            source = PackPaths.Normalize(source); target = PackPaths.Normalize(target);
            if (source.Equals(target, StringComparison.OrdinalIgnoreCase) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                !Regex.IsMatch(previousHash ?? "", "\\A[a-f0-9]{64}\\z") || !Regex.IsMatch(newHash ?? "", "\\A[a-f0-9]{64}\\z"))
                throw new InvalidDataException("Неверные параметры обновления.");
            PackPaths.RejectLinks(source); PackPaths.RejectLinks(target);
            if (PackInstall.Hash(source, true) != newHash || PackInstall.Hash(target, true) != previousHash)
                throw new InvalidDataException("Файл приложения изменился. Скачай свежий выпуск с GitHub.");
            string temporary = target + ".update-" + Guid.NewGuid().ToString("N");
            string backup = target + ".previous";
            PackPaths.RejectLinks(backup);
            try
            {
                File.Copy(PackPaths.Native(source), PackPaths.Native(temporary));
                if (PackInstall.Hash(temporary, true) != newHash) throw new InvalidDataException("Ошибка копирования обновления.");
                File.Replace(PackPaths.Native(temporary), PackPaths.Native(target), PackPaths.Native(backup));
            }
            finally { if (File.Exists(PackPaths.Native(temporary))) File.Delete(PackPaths.Native(temporary)); }
        }

        public static void ApplyAndRestart(string[] args, string executable)
        {
            if (args.Length != 6) throw new InvalidDataException("Неверные параметры обновления.");
            int parentId;
            if (!Int32.TryParse(args[1], out parentId) || parentId == Process.GetCurrentProcess().Id)
                throw new InvalidDataException("Не удалось дождаться закрытия приложения.");
            try { using (var parent = Process.GetProcessById(parentId)) if (!parent.WaitForExit(20000)) throw new IOException("Закрой старое окно PackHelper и повтори обновление."); }
            catch (ArgumentException) { }
            Exception last = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try { Replace(executable, args[2], args[3], args[4]); last = null; break; }
                catch (IOException error) { last = error; Thread.Sleep(500); }
            }
            if (last != null) throw last;
            string parameters = args[5].Length == 0 ? "--updated" : "--game-folder " + Quote(args[5]) + " --updated";
            Process.Start(new ProcessStartInfo(args[2], parameters) { UseShellExecute = false });
        }
    }
}
