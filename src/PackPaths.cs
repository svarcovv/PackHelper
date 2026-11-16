using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LavaBox.PackHelper
{
    public static class PackPaths
    {
        public static string Native(string path)
        { return path.StartsWith("\\\\?\\", StringComparison.Ordinal) ? path : "\\\\?\\" + Path.GetFullPath(path); }

        public static string Normalize(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Regex.IsMatch(path, "\\A[A-Za-z]:[\\\\/]"))
                throw new InvalidDataException("Выберите локальную папку Minecraft на этом компьютере.");
            if(path.IndexOf(':',2)>=0 || path.IndexOfAny(new[]{'\0','\r','\n'})>=0)
                throw new InvalidDataException("Недопустимый путь к папке игры.");
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool Inside(string path, string parent)
        {
            return Normalize(path).StartsWith(Normalize(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public static bool LooksLikeGame(string path)
        {
            return Directory.Exists(path) && (File.Exists(Path.Combine(path, "options.txt")) ||
                Directory.Exists(Path.Combine(path, "logs")) || Directory.Exists(Path.Combine(path, "resourcepacks")) ||
                File.Exists(Path.Combine(path, "launcher_profiles.json")) ||
                (Directory.Exists(Path.Combine(path, "versions")) && Directory.Exists(Path.Combine(path, "assets"))));
        }

        public static void RejectLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(Native(current)) || Directory.Exists(Native(current))) &&
                    (File.GetAttributes(Native(current)) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Путь содержит ссылку или точку перенаправления. Выберите исходную папку сборки: " + current);
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }

        public static void ValidateCandidate(GameCandidate candidate, PackRelease release)
        {
            release.Validate();
            string root = Normalize(candidate.GameDirectory);
            string downloads = Path.Combine(root, "downloads");
            string cache = Normalize(candidate.CacheDirectory);
            if (!LooksLikeGame(root)) throw new IOException("Это не папка игры. Выберите папку с options.txt, logs или resourcepacks.");
            if (!cache.Equals(downloads, StringComparison.OrdinalIgnoreCase) && !Inside(cache, downloads))
                throw new IOException("Путь кэша выходит за пределы downloads выбранной сборки.");
            if (candidate.NeedsCacheEvidence)
                throw new IOException("В этой сборке обнаружен OpSec. Сначала один раз подключитесь к LavaBox, закройте игру и выберите её свежий latest.log.");
            string destination = candidate.Destination(release);
            if (!Inside(destination, downloads)) throw new IOException("Недопустимый путь установки.");
            RejectLinks(destination);
        }
    }

}
