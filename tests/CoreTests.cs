using System;
using System.IO;
using System.Linq;
using System.Text;
using LavaBox.PackHelper;

static class CoreTests
{
    static int count;
    static string root;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
    static void Throws(Action action, string name)
    {
        bool caught = false;
        try { action(); } catch (IOException) { caught = true; } catch (InvalidDataException) { caught = true; } catch (ArgumentException) { caught = true; }
        Check(caught, name);
    }
    static void Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
        AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        root = Path.Combine(Path.GetFullPath(args[0]), "packhelper-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "options.txt"), "test");
        byte[] payload = Encoding.UTF8.GetBytes("PackHelper test archive contents");
        string original = Path.Combine(root, "payload.zip"); File.WriteAllBytes(original, payload);
        var release = new PackRelease { packVersion = "1.2.3", packId = "4e7502b2-1883-44d9-a2b4-03cfd5884e13",
            sha1 = PackInstall.Hash(original, false), sha256 = PackInstall.Hash(original, true), size = payload.Length };
        release.Validate();
        var candidate = GameDiscovery.Candidate(root, release);
        string target = candidate.Destination(release);
        string oldHash = new string('b', 40);
        string log = "[00:00:00] [Download/WARN]: Existing file " + Path.Combine(root, "downloads", release.packId, oldHash) + " not found or had mismatched hash";
        var parsed = GameDiscovery.ParseLog(log, release).Single();
        Check(parsed.GameDirectory == root && parsed.Destination(release) == target, "old version log resolves current version destination");
        string portableRoot = @"C:\Downloads\Minecraft Portable";
        string portableLog = "Existing file " + Path.Combine(portableRoot, "downloads", release.packId, oldHash) + " not found or had mismatched hash";
        Check(GameDiscovery.ParseLog(portableLog, release).Single().GameDirectory == portableRoot, "game inside Downloads uses its own downloads directory");
        string scope = Path.Combine(root, "downloads", Guid.NewGuid().ToString(), "a8ddce9c0e7c191d");
        var scoped = GameDiscovery.ParseLog("Existing file " + Path.Combine(scope, release.packId, oldHash) + " not found or had mismatched hash", release).Single();
        Check(scoped.Isolated && scoped.CacheDirectory == scope, "isolated cache keeps account and server buckets");
        Check(GameDiscovery.ParseLog(log.Replace(release.packId, Guid.NewGuid().ToString()), release).Count == 0, "unrelated pack ignored");
        Check(GameDiscovery.ParseLog(log.Replace("downloads\\", "downloads\\..\\outside\\"), release).Count == 0, "path traversal in log ignored");
        Directory.CreateDirectory(Path.Combine(root, "resourcepacks"));
        string personal = Path.Combine(root, "resourcepacks", "personal.zip"); File.WriteAllText(personal, "keep");
        var result = PackInstall.Install(candidate, release, () => new MemoryStream(payload));
        Check(!result.AlreadyPresent && PackInstall.Hash(target, true) == release.sha256, "standard cache installed and verified");
        Check(File.ReadAllText(personal) == "keep" && File.ReadAllText(Path.Combine(root, "options.txt")) == "test", "personal packs and game settings preserved");
        DateTime modified = File.GetLastWriteTimeUtc(target);
        result = PackInstall.Install(candidate, release, () => { throw new Exception("must not reopen payload"); });
        Check(result.AlreadyPresent && File.GetLastWriteTimeUtc(target) == modified, "idempotent installation does not rewrite cache");
        File.WriteAllText(target, "corrupt");
        result = PackInstall.Install(candidate, release, () => new MemoryStream(payload));
        Check(File.ReadAllText(result.BackupPath) == "corrupt" && PackInstall.Hash(target, true) == release.sha256, "corrupt cache backed up before atomic replacement");
        result = PackInstall.Install(scoped, release, () => new MemoryStream(payload));
        Check(File.Exists(PackPaths.Native(result.Path)) && result.Path.StartsWith(scope), "isolated cache installation");
        File.WriteAllText(PackPaths.Native(result.Path), "broken scoped cache");
        result = PackInstall.Install(scoped, release, () => new MemoryStream(payload));
        Check(File.ReadAllText(PackPaths.Native(result.BackupPath)) == "broken scoped cache" && PackInstall.Hash(result.Path, true) == release.sha256,
            "long isolated cache path supports atomic repair");
        var outside = new GameCandidate { GameDirectory = root, CacheDirectory = Path.GetDirectoryName(root) };
        Throws(() => PackInstall.Install(outside, release, () => new MemoryStream(payload)), "outside-cache write rejected");
        var unknown = new GameCandidate { GameDirectory = root, CacheDirectory = Path.Combine(root, "downloads"), NeedsCacheEvidence = true };
        Throws(() => PackPaths.ValidateCandidate(unknown, release), "unknown OpSec cache requires evidence");
        var wrong = new GameCandidate { GameDirectory = root, CacheDirectory = Path.Combine(root, "downloads", "another") };
        Throws(() => PackInstall.Install(wrong, release, () => new MemoryStream(new byte[payload.Length])), "wrong embedded hash rejected");
        Check(!File.Exists(wrong.Destination(release)) && Directory.GetFiles(Path.GetDirectoryName(wrong.Destination(release)), "*.tmp").Length == 0, "failed install leaves no final file or temporary payload");
        Throws(() => PackInstall.Install(wrong, release, () => new MemoryStream(new byte[1])), "truncated payload rejected");
        string logs = Path.Combine(root, "logs"); Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "latest.log"), "[OpSec] Loaded config");
        Check(GameDiscovery.Candidate(root, release).NeedsCacheEvidence, "OpSec without target cache evidence blocks guessing");
        File.WriteAllText(Path.Combine(logs, "latest.log"), "[OpSec] Loaded config\n" + log);
        Check(!GameDiscovery.Candidate(root, release).NeedsCacheEvidence, "explicit log path takes priority over mod detection");
        count += UpdateTests.Run(root,release);
        Console.WriteLine("All " + count + " checks passed. Fixture: " + root);
    }
}
