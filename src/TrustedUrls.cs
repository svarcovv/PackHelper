using System;

namespace LavaBox.PackHelper
{
    public static class TrustedUrls
    {
        public const string Repository = "https://github.com/svarcovv/PackHelper";
        public const string Discord = "https://discord.gg/xMBKSnDvt";
        public const string Developer = "https://t.me/AntiDoDmiGEI";
        public static bool IsUserLink(string value)
        {
            return value == Repository || value == Repository + "/releases/latest" ||
                value == Repository + "/issues/new?template=bug_report.yml" ||
                value == Discord || value == Developer;
        }
        public static bool IsRequest(Uri uri, bool redirected, bool releaseDownload)
        {
            if (uri == null || uri.Scheme != "https" || !uri.IsDefaultPort ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
            if (releaseDownload && uri.Host == "github.com")
                return uri.AbsolutePath.StartsWith("/svarcovv/PackHelper/releases/download/", StringComparison.Ordinal);
            if (!releaseDownload && uri.Host == "api.github.com")
                return uri.AbsolutePath == "/repos/svarcovv/PackHelper/releases" || uri.AbsolutePath == "/repos/svarcovv/PackHelper/releases/latest";
            return redirected && releaseDownload &&
                (uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
        }
    }
}
