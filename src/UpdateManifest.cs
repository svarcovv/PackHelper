using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LavaBox.PackHelper
{
    public sealed class UpdateManifest
    {
        public int schemaVersion;
        public string appVersion, packVersion, packId, packSha1, packSha256;
        public string exeName, exeSha256, downloadUrl;
        public long exeSize;

        public static Version VersionOf(string value)
        {
            if (!Regex.IsMatch(value ?? "", "\\A[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}\\z"))
                throw new InvalidDataException("Неверный номер версии выпуска.");
            return new Version(value);
        }

        public void Validate(string expectedPackId)
        {
            VersionOf(appVersion); VersionOf(packVersion);
            if (schemaVersion != 1 || packId != expectedPackId ||
                !Regex.IsMatch(packSha1 ?? "", "\\A[a-f0-9]{40}\\z") ||
                !Regex.IsMatch(packSha256 ?? "", "\\A[a-f0-9]{64}\\z") ||
                !Regex.IsMatch(exeSha256 ?? "", "\\A[a-f0-9]{64}\\z") ||
                exeSize < 1024 || exeSize > 134217728 || exeName != "PackHelper-" + appVersion + ".exe")
                throw new InvalidDataException("Данные обновления повреждены или относятся к другому паку.");
            Uri uri;
            string expected = "https://github.com/svarcovv/PackHelper/releases/download/v" + appVersion + "/" + exeName;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out uri) || uri.AbsoluteUri != expected)
                throw new InvalidDataException("Обновление должно находиться в официальном выпуске PackHelper.");
        }
    }

    public sealed class UpdateCheck
    {
        public UpdateManifest Manifest;
        public bool NoPublishedRelease;
        public bool ManualDownload;
    }

    public sealed class CacheState
    {
        public string Text;
        public bool Installed;
        public static CacheState Read(GameCandidate game, string id, string hash)
        {
            if (game == null) return new CacheState { Text = "Сначала выбери сборку" };
            if (game.NeedsCacheEvidence) return new CacheState { Text = "Нужен свежий latest.log" };
            string path = Path.Combine(game.CacheDirectory, id, hash);
            try
            {
                if (!File.Exists(PackPaths.Native(path))) return new CacheState { Text = "Пак ещё не установлен" };
                bool valid = PackInstall.Hash(path, false) == hash;
                return new CacheState { Installed = valid, Text = valid ? "Установлен в этой сборке" : "Нужно восстановить файл" };
            }
            catch (IOException) { return new CacheState { Text = "Закрой игру и проверь файл снова" }; }
            catch (UnauthorizedAccessException) { return new CacheState { Text = "Нет доступа к кэшу сборки" }; }
        }
    }
}
