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
    public sealed class InstallResult
    {
        public string Path;
        public bool AlreadyPresent;
        public string BackupPath;
    }

    public static class PackInstall
    {
        public static string Hash(string file, bool sha256)
        {
            using (var stream = new FileStream(PackPaths.Native(file), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (HashAlgorithm algorithm = sha256 ? (HashAlgorithm)SHA256.Create() : SHA1.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static InstallResult Install(GameCandidate candidate, PackRelease release, Func<Stream> openPack)
        {
            PackPaths.ValidateCandidate(candidate, release);
            string destination = candidate.Destination(release);
            string nativeDestination = PackPaths.Native(destination);
            if (File.Exists(nativeDestination) && new FileInfo(nativeDestination).Length == release.size &&
                Hash(destination, true) == release.sha256 && Hash(destination, false) == release.sha1)
                return new InstallResult { Path = destination, AlreadyPresent = true };
            string parent = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(PackPaths.Native(parent));
            PackPaths.RejectLinks(destination);
            string temporary = Path.Combine(parent, ".lava-install-" + Guid.NewGuid().ToString("N") + ".tmp");
            string nativeTemporary = PackPaths.Native(temporary);
            string backup = null;
            try
            {
                using (Stream input = openPack())
                using (var output = new FileStream(nativeTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (input == null) throw new InvalidDataException("В выпуске отсутствует архив ресурспака.");
                    byte[] buffer = new byte[65536]; long size = 0; int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        size += count;
                        if (size > release.size) throw new InvalidDataException("Размер встроенного архива не совпадает с выпуском.");
                        output.Write(buffer, 0, count);
                    }
                    if (size != release.size) throw new InvalidDataException("Встроенный архив неполный.");
                    output.Flush(true);
                }
                if (Hash(temporary, true) != release.sha256 || Hash(temporary, false) != release.sha1)
                    throw new InvalidDataException("Проверка архива не пройдена. Скачайте официальный выпуск заново.");
                PackPaths.RejectLinks(destination);
                if (File.Exists(nativeDestination))
                {
                    backup = Path.Combine(parent, "before-PackHelper-" + Guid.NewGuid().ToString("N"));
                    File.Replace(nativeTemporary, nativeDestination, PackPaths.Native(backup));
                }
                else File.Move(nativeTemporary, nativeDestination);
                return new InstallResult { Path = destination, AlreadyPresent = false, BackupPath = backup };
            }
            finally { if (File.Exists(nativeTemporary)) File.Delete(nativeTemporary); }
        }

        public static Stream EmbeddedPack()
        { return Assembly.GetExecutingAssembly().GetManifestResourceStream("LavaBox.Pack.zip"); }
    }}
