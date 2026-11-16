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
    public sealed class PackRelease
    {
        public string appVersion = "1.1.0";
        public string packVersion;
        public string packId;
        public string sha1;
        public string sha256;
        public long size;
        public string server = "lavabox.fun";
        public string sourceUrl = "https://github.com/svarcovv/PackHelper";
        public string discordUrl = "https://discord.gg/xMBKSnDvt";
        public string developerUrl = "https://t.me/AntiDoDmiGEI";

        public void Validate()
        {
            UpdateManifest.VersionOf(appVersion);
            UpdateManifest.VersionOf(packVersion);
            if (server != "lavabox.fun" || sourceUrl != TrustedUrls.Repository || discordUrl != TrustedUrls.Discord || developerUrl != TrustedUrls.Developer)
                throw new InvalidDataException("В выпуске изменены официальные адреса PackHelper.");
            Guid id;
            if (!Guid.TryParseExact(packId, "D", out id) || packId != id.ToString())
                throw new InvalidDataException("Неверный идентификатор ресурспака.");
            if (!Regex.IsMatch(sha1 ?? "", "\\A[a-f0-9]{40}\\z") ||
                !Regex.IsMatch(sha256 ?? "", "\\A[a-f0-9]{64}\\z") || size < 1 || size > 67108864)
                throw new InvalidDataException("Неверные контрольные суммы или размер ресурспака.");
            if (String.IsNullOrWhiteSpace(packVersion)) throw new InvalidDataException("Не указана версия пака.");
        }

        public static PackRelease Embedded()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LavaBox.Release.json"))
            {
                if (stream == null) throw new InvalidDataException("В выпуске отсутствуют данные ресурспака.");
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var release = new JavaScriptSerializer().Deserialize<PackRelease>(reader.ReadToEnd());
                    release.Validate();
                    return release;
                }
            }
        }
    }

}
