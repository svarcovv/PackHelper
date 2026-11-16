using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LavaBox.PackHelper
{
    public sealed partial class UpdateClient : IDisposable
    {
        private readonly HttpClient client;
        public UpdateClient() : this(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { }
        public UpdateClient(HttpMessageHandler handler)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackHelper/"+typeof(UpdateClient).Assembly.GetName().Version.ToString(3));
        }

        private async Task<string> Json(string url, CancellationToken cancel)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel))
            {
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 1048576) throw new InvalidDataException("Ответ обновлений слишком большой.");
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var buffer = new MemoryStream())
                {
                    byte[] chunk = new byte[8192]; int n;
                    while ((n = await stream.ReadAsync(chunk, 0, chunk.Length, cancel)) > 0)
                    {
                        if (buffer.Length + n > 1048576) throw new InvalidDataException("Ответ обновлений слишком большой.");
                        buffer.Write(chunk, 0, n);
                    }
                    return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                }
            }
        }

        public async Task<UpdateCheck> Check(string packId, CancellationToken cancel)
        {
            var serializer = new JavaScriptSerializer();
            string json = await Json("https://api.github.com/repos/svarcovv/PackHelper/releases/latest", cancel);
            if (json == null) return new UpdateCheck { NoPublishedRelease = true };
            var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
            object assets;
            if (release == null || !release.TryGetValue("assets", out assets) || !(assets is object[]))
                throw new InvalidDataException("Не удалось прочитать выпуск GitHub.");
            string manifestUrl = null;
            foreach (var item in (object[])assets)
            {
                var asset = item as Dictionary<string, object>; object name, url;
                if (asset != null && asset.TryGetValue("name", out name) && (string)name == "update.json" &&
                    asset.TryGetValue("browser_download_url", out url)) manifestUrl = url as string;
            }
            if (manifestUrl == null) return new UpdateCheck { ManualDownload = true };
            Uri parsed;
            if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out parsed) || parsed.Scheme != "https" ||
                parsed.Host != "github.com" || !parsed.AbsolutePath.StartsWith("/svarcovv/PackHelper/releases/download/", StringComparison.Ordinal) ||
                !parsed.AbsolutePath.EndsWith("/update.json", StringComparison.Ordinal) || !String.IsNullOrEmpty(parsed.Query))
                throw new InvalidDataException("Неверный адрес данных обновления.");
            string metadata = await Json(manifestUrl, cancel);
            if (metadata == null) throw new InvalidDataException("Файл update.json отсутствует в выпуске.");
            var manifest = serializer.Deserialize<UpdateManifest>(metadata);
            manifest.Validate(packId);
            return new UpdateCheck { Manifest = manifest };
        }

        public async Task<string> Download(UpdateManifest update, string packId, string root, IProgress<int> progress, CancellationToken cancel)
        {
            update.Validate(packId);
            string directory = Path.Combine(root, update.appVersion, update.exeSha256.Substring(0, 12));
            PackPaths.RejectLinks(directory); Directory.CreateDirectory(PackPaths.Native(directory));
            string file = Path.Combine(directory, update.exeName);
            if (File.Exists(PackPaths.Native(file)) && PackInstall.Hash(file, true) == update.exeSha256) return file;
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var response = await client.GetAsync(update.downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != update.exeSize)
                        throw new InvalidDataException("Размер обновления не совпадает с выпуском.");
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(PackPaths.Native(temp), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                    {
                        byte[] chunk = new byte[65536]; long total = 0; int n;
                        while ((n = await input.ReadAsync(chunk, 0, chunk.Length, cancel)) > 0)
                        {
                            total += n;
                            if (total > update.exeSize) throw new InvalidDataException("Обновление больше заявленного размера.");
                            await output.WriteAsync(chunk, 0, n, cancel);
                            if (progress != null) progress.Report((int)(total * 100 / update.exeSize));
                        }
                        if (total != update.exeSize) throw new InvalidDataException("Обновление скачалось не полностью.");
                        output.Flush(true);
                    }
                }
                if (PackInstall.Hash(temp, true) != update.exeSha256) throw new InvalidDataException("Проверка обновления не пройдена. Попробуй ещё раз.");
                PackPaths.RejectLinks(file);
                if (File.Exists(PackPaths.Native(file))) File.Replace(PackPaths.Native(temp), PackPaths.Native(file), null);
                else File.Move(PackPaths.Native(temp), PackPaths.Native(file));
                return file;
            }
            finally { if (File.Exists(PackPaths.Native(temp))) File.Delete(PackPaths.Native(temp)); }
        }
        public void Dispose() { client.Dispose(); }
    }
}
