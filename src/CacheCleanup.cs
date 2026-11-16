using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LavaBox.PackHelper
{
    public sealed class CleanupResult
    {
        public int RemovedFiles;
        public long RemovedBytes;
        public bool Incomplete;
    }
    public static class CacheCleanup
    {
        public static CleanupResult Prune(GameCandidate game,PackRelease release)
        {
            PackPaths.ValidateCandidate(game,release);
            string target=game.Destination(release),native=PackPaths.Native(target);
            if(!File.Exists(native)||new FileInfo(native).Length!=release.size||PackInstall.Hash(target,false)!=release.sha1||PackInstall.Hash(target,true)!=release.sha256)
                throw new InvalidDataException("Новый файл не прошёл проверку. Старый кэш сохранён.");
            string directory=Path.GetDirectoryName(target);var result=new CleanupResult();
            // Only this pack's UUID in the selected cache. Never recurse into other packs.
            try
            {
                int inspected=0;
                foreach(string entry in Directory.EnumerateFiles(PackPaths.Native(directory)))
                {
                    if(++inspected>256){result.Incomplete=true;break;}
                    string name=Path.GetFileName(entry);
                    if(!Regex.IsMatch(name,"\\A[0-9a-fA-F]{40}\\z")||name.Equals(release.sha1,StringComparison.OrdinalIgnoreCase))continue;
                    string file=Path.Combine(directory,name);
                    try
                    {
                        PackPaths.RejectLinks(file);long size=new FileInfo(PackPaths.Native(file)).Length;
                        if(size>67108864||!PackInstall.Hash(file,false).Equals(name,StringComparison.OrdinalIgnoreCase))continue;
                        PackPaths.RejectLinks(file);File.Delete(PackPaths.Native(file));result.RemovedFiles++;result.RemovedBytes+=size;
                    }
                    catch(IOException){result.Incomplete=true;}
                    catch(UnauthorizedAccessException){result.Incomplete=true;}
                }
            }
            catch(IOException){result.Incomplete=true;}
            catch(UnauthorizedAccessException){result.Incomplete=true;}
            return result;
        }
    }
}
