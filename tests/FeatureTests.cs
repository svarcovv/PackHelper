using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LavaBox.PackHelper;

static class FeatureTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
    sealed class FakeHttp:HttpMessageHandler
    {
        public Func<Uri,string> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Reply(request.RequestUri),Encoding.UTF8,"application/json")});}
    }
    static string OldFile(string directory,string value)
    {Directory.CreateDirectory(directory);string temp=Path.Combine(directory,"fixture.tmp");File.WriteAllText(temp,value);string target=Path.Combine(directory,PackInstall.Hash(temp,false));if(File.Exists(target))File.Delete(target);File.Move(temp,target);return target;}
    public static int Run(string root,PackRelease pack)
    {
        var game=new GameCandidate{GameDirectory=root,CacheDirectory=Path.Combine(root,"downloads")};string target=game.Destination(pack),directory=Path.GetDirectoryName(target);
        string old=OldFile(directory,"old resource pack version");string foreign=OldFile(Path.Combine(game.CacheDirectory,Guid.NewGuid().ToString()),"other server pack");
        string notes=Path.Combine(directory,"notes.txt"),unknown=Path.Combine(directory,new string('c',40));File.WriteAllText(notes,"keep");File.WriteAllText(unknown,"unknown contents");
        File.WriteAllText(target,"broken new file");bool rejected=false;
        try{CacheCleanup.Prune(game,pack);}catch(InvalidDataException){rejected=true;}
        Check(rejected&&File.Exists(old),"old cache preserved when new file fails validation");
        var installed=PackInstall.Install(game,pack,()=>File.OpenRead(Path.Combine(root,"payload.zip")));
        Check(installed.Cleanup.RemovedFiles==1&&!File.Exists(old)&&PackInstall.Hash(target,true)==pack.sha256,"verified new installation prunes only old hashes in its UUID");
        Check(File.Exists(foreign)&&File.Exists(notes)&&File.Exists(unknown),"cleanup preserves other packs, notes and unverified files");
        Check(File.Exists(installed.BackupPath),"repair backup is retained during cleanup");
        DateTime modified=File.GetLastWriteTimeUtc(target);old=OldFile(directory,"another old version");
        installed=PackInstall.Install(game,pack,()=>{throw new Exception("must not rewrite current pack");});
        Check(installed.AlreadyPresent&&installed.Cleanup.RemovedFiles==1&&File.GetLastWriteTimeUtc(target)==modified,"cleanup works without rewriting an already current pack");
        rejected=false;try{CacheCleanup.Prune(new GameCandidate{GameDirectory=root,CacheDirectory=Path.GetDirectoryName(root)},pack);}catch(IOException){rejected=true;}
        Check(rejected&&File.Exists(foreign),"cleanup rejects a cache outside the game downloads");
        RunAsync(root,pack).GetAwaiter().GetResult();return count;
    }
    static string Release(bool draft,bool prerelease,object[] assets)
    {return new JavaScriptSerializer().Serialize(new{draft=draft,prerelease=prerelease,assets=assets});}
    static object Asset(string name,long downloads,string state="uploaded")
    {return new{name=name,download_count=downloads,state=state};}
    static async Task RunAsync(string root,PackRelease pack)
    {
        string json="["+Release(false,false,new[]{Asset("PackHelper-1.0.0.exe",4),Asset("update.json",100),Asset("PackHelper-1.1.0.exe",100,"new")})+","+
            Release(true,false,new[]{Asset("PackHelper-2.0.0.exe",100)})+","+Release(false,true,new[]{Asset("PackHelper-3.0.0.exe",100)})+","+
            Release(false,false,new[]{Asset("PackHelper-0.9.0.exe",6)})+"]";
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>json}))
        {var result=await client.Statistics(CancellationToken.None);Check(result.Count==10&&result.Complete,"statistics count only uploaded EXEs in stable public releases");}
        string one=Release(false,false,new[]{Asset("PackHelper-1.0.0.exe",1)});int calls=0;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>{calls++;return uri.Query.EndsWith("&page=1",StringComparison.Ordinal)?"["+String.Join(",",System.Linq.Enumerable.Repeat(one,30))+"]":"["+Release(false,false,new[]{Asset("PackHelper-2.0.0.exe",2)})+"]";}}))
        {var result=await client.Statistics(CancellationToken.None);Check(result.Count==32&&result.Complete&&calls==2,"statistics follow release pagination");}
        bool rejected=false;using(var client=new UpdateClient(new FakeHttp{Reply=uri=>"["+Release(false,false,new[]{Asset("PackHelper-1.0.0.exe",-1)})+"]"}))
        {try{await client.Statistics(CancellationToken.None);}catch(InvalidDataException){rejected=true;}}
        Check(rejected,"invalid download counts do not become displayed statistics");
        var latest=new UpdateManifest{schemaVersion=1,appVersion="9.1.0",packVersion=pack.packVersion,packId=pack.packId,packSha1=pack.sha1,packSha256=pack.sha256,exeName="PackHelper-9.1.0.exe",exeSize=2048,exeSha256=new string('a',64),downloadUrl="https://github.com/svarcovv/PackHelper/releases/download/v9.1.0/PackHelper-9.1.0.exe"};
        string cache=Path.Combine(root,"status-cache");var status=new PublicStatusCache{Downloads=10,DownloadsComplete=true,DownloadsCheckedUtc=DateTime.UtcNow,Latest=latest,ReleaseCheckedUtc=DateTime.UtcNow};status.Save(cache);
        var read=PublicStatusCache.Read(cache,pack.packId);Check(read.Downloads==10&&read.Latest.appVersion=="9.1.0","public counters and release metadata are available from offline cache");
        Check(PublicStatusCache.Read(cache,Guid.NewGuid().ToString()).Latest==null,"cached release for another pack is rejected");
        Check(!PublicStatusCache.Fresh(DateTime.UtcNow.AddMinutes(5))&&!PublicStatusCache.Fresh(DateTime.UtcNow.AddHours(-2))&&PublicStatusCache.Fresh(DateTime.UtcNow),"background refresh cache handles old and future timestamps");
        File.WriteAllText(Path.Combine(cache,"public-status.json"),"broken JSON");Check(PublicStatusCache.Read(cache,pack.packId).Downloads==null,"broken public status cache does not prevent startup");
    }
}
