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
    public static class GameDiscovery
    {
        public static string ResolveInput(string input)
        {
            string value=Environment.ExpandEnvironmentVariables((input??"").Trim().Trim('"'));
            if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))throw new IOException("Вставь полный адрес папки из строки проводника.");
            string path=PackPaths.Normalize(value);
            if(File.Exists(path))
            {if(!Path.GetExtension(path).Equals(".log",StringComparison.OrdinalIgnoreCase))throw new IOException("Для поиска по логу выбери latest.log.");return path;}
            string leaf=Path.GetFileName(path);
            if(leaf.Equals("resourcepacks",StringComparison.OrdinalIgnoreCase)||leaf.Equals("logs",StringComparison.OrdinalIgnoreCase))
            {string parent=Path.GetDirectoryName(path);if(PackPaths.LooksLikeGame(parent))return parent;}
            if(!PackPaths.LooksLikeGame(path))throw new IOException("В этой папке не найдена сборка. Выбери папку с options.txt, logs или resourcepacks.");
            return path;
        }
        private static readonly Regex CacheLine = new Regex(@"Existing file (?<path>.+?) not found or had mismatched hash", RegexOptions.Compiled);
        private static readonly Regex GameArgument = new Regex("(?:^|\\s)--gameDir(?:=|\\s+)(?:\"(?<quoted>[^\"]+)\"|(?<plain>[^\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ReadLogTail(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bool truncated = stream.Length > 4194304;
                if (truncated) stream.Seek(-4194304, SeekOrigin.End);
                // Snapshot at most 4 MiB, even if another process keeps appending to the log.
                byte[] bytes=new byte[(int)Math.Min(stream.Length-stream.Position,4194304)];int used=0,n;
                while(used<bytes.Length&&(n=stream.Read(bytes,used,bytes.Length-used))>0)used+=n;
                using (var reader = new StreamReader(new MemoryStream(bytes,0,used), Encoding.UTF8, true))
                {
                    if (truncated) reader.ReadLine();
                    return reader.ReadToEnd();
                }
            }
        }

        public static GameCandidate Candidate(string root, PackRelease release)
        {
            root = PackPaths.Normalize(root);
            var candidate = new GameCandidate { GameDirectory = root, CacheDirectory = Path.Combine(root, "downloads"), Description = "Minecraft" };
            string log = Path.Combine(root, "logs", "latest.log");
            if (File.Exists(log))
            {
                try
                {
                    string text = ReadLogTail(log);
                    var observed = ParseLog(text, release).LastOrDefault(c => c.GameDirectory.Equals(root, StringComparison.OrdinalIgnoreCase));
                    if (observed != null) return observed;
                    candidate.NeedsCacheEvidence = text.IndexOf("aurick.opsec", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("[OpSec]", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (candidate.NeedsCacheEvidence) candidate.Description = "OpSec - нужен свежий лог входа";
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return candidate;
        }

        public static List<GameCandidate> ParseLog(string text, PackRelease release)
        {
            var result = new List<GameCandidate>();
            foreach (Match match in CacheLine.Matches(text))
            {
                try
                {
                    string path = PackPaths.Normalize(match.Groups["path"].Value);
                    string hash = Path.GetFileName(path);
                    string packDirectory = Path.GetDirectoryName(path);
                    if (!Regex.IsMatch(hash, "\\A[0-9a-fA-F]{40}\\z") ||
                        !Path.GetFileName(packDirectory).Equals(release.packId, StringComparison.OrdinalIgnoreCase)) continue;
                    int marker = path.LastIndexOf("\\downloads\\", StringComparison.OrdinalIgnoreCase);
                    if (marker < 3) continue;
                    string root = path.Substring(0, marker);
                    string cache = Path.GetDirectoryName(packDirectory);
                    string downloads = Path.Combine(root, "downloads");
                    if (!cache.Equals(downloads, StringComparison.OrdinalIgnoreCase) && !PackPaths.Inside(cache, downloads)) continue;
                    var candidate = new GameCandidate { GameDirectory = root, CacheDirectory = cache,
                        Isolated = !cache.Equals(downloads, StringComparison.OrdinalIgnoreCase),
                        Description = "Путь из лога LavaBox" };
                    candidate.Description = candidate.Isolated ? "Изолированный кэш из лога (OpSec)" : "Minecraft - подтверждено логом LavaBox";
                    result.Add(candidate);
                }
                catch (ArgumentException) { }
                catch (InvalidDataException) { }
                catch (IOException) { }
                catch (NotSupportedException) { }
            }
            return result;
        }

        private static IEnumerable<string> Children(string root)
        {
            try { return Directory.EnumerateDirectories(root).Take(120).Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0).ToArray(); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }

        public static List<string> RunningGameDirectories()
        {
            var roots = new List<string>();
            try
            {
                using (var search = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'"))
                {
                    search.Options.Timeout = TimeSpan.FromSeconds(3);
                    using (var processes = search.Get()) foreach (ManagementObject process in processes)
                    {
                        using (process)
                        {
                            string command = process["CommandLine"] as string ?? "";
                            Match match = GameArgument.Match(command);
                            if (match.Success)
                            {
                                string value = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
                                try { roots.Add(PackPaths.Normalize(value)); } catch (Exception) { }
                            }
                        }
                    }
                }
            }
            catch (ManagementException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Runtime.InteropServices.COMException) { }
            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<GameCandidate> Scan(PackRelease release)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string>(RunningGameDirectories());
            roots.Add(Path.Combine(roaming, ".minecraft"));
            roots.Add(Path.Combine(roaming, ".tlauncher", "legacy", "Minecraft", "game"));
            string[] launchers = { Path.Combine(roaming, "PrismLauncher", "instances"), Path.Combine(local, "PrismLauncher", "instances"),
                Path.Combine(roaming, "MultiMC", "instances"), Path.Combine(local, "MultiMC", "instances"),
                Path.Combine(roaming, "ATLauncher", "instances"), Path.Combine(roaming, "com.modrinth.theseus", "profiles"),
                Path.Combine(local, "com.modrinth.theseus", "profiles"), Path.Combine(roaming, "ModrinthApp", "profiles"),
                Path.Combine(local, "ModrinthApp", "profiles"), Path.Combine(home, "curseforge", "minecraft", "Instances"),
                Path.Combine(home, "Documents", "CurseForge", "Minecraft", "Instances") };
            foreach (string launcher in launchers) foreach (string instance in Children(launcher))
            {
                roots.Add(instance); roots.Add(Path.Combine(instance, ".minecraft")); roots.Add(Path.Combine(instance, "minecraft"));
            }
            foreach (string root in roots.ToArray())
            {
                foreach (string version in Children(Path.Combine(root, "versions")))
                    if (File.Exists(Path.Combine(version, "options.txt")) || Directory.Exists(Path.Combine(version, "logs"))) roots.Add(version);
                string profiles = Path.Combine(root, "launcher_profiles.json");
                try
                {
                    if (!File.Exists(profiles) || new FileInfo(profiles).Length > 1048576) continue;
                    var json = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(profiles)) as Dictionary<string, object>;
                    object items;
                    if (json == null || !json.TryGetValue("profiles", out items)) continue;
                    var entries = items as Dictionary<string, object>;
                    if (entries == null) continue;
                    foreach (var entry in entries.Values.Take(120))
                    {
                        var profile = entry as Dictionary<string, object>; object game;
                        if (profile != null && profile.TryGetValue("gameDir", out game) && game is string) roots.Add((string)game);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (InvalidDataException) { }
                catch (InvalidOperationException) { }
            }
            var result = new List<GameCandidate>();
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Take(250))
            {
                try
                {
                    if (!PackPaths.LooksLikeGame(root)) continue;
                    PackPaths.RejectLinks(root);
                    result.Add(Candidate(root, release));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
            }
            return result;
        }
    }

}
