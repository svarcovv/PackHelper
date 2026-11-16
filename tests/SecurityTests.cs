using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LavaBox.PackHelper;

static class SecurityTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
    sealed class FakeHttp:HttpMessageHandler
    {
        public Func<Uri,HttpResponseMessage> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {return Task.FromResult(Reply(request.RequestUri));}
    }
    public static int Run(string root,PackRelease pack)
    {
        Check(!TrustedUrls.IsUserLink("https://github.com/attacker/PackHelper")&&!TrustedUrls.IsUserLink("https://t.me/attacker")&&TrustedUrls.IsUserLink(TrustedUrls.Discord),"only exact official support and repository links accepted");
        bool rejected=false;try{PackPaths.Normalize(@"C:\game\downloads\file:payload");}catch(InvalidDataException){rejected=true;}
        Check(rejected,"NTFS alternate data streams rejected");
        var fakePack=new JavaScriptSerializer().Deserialize<PackRelease>(new JavaScriptSerializer().Serialize(pack));fakePack.developerUrl="https://t.me/attacker";
        rejected=false;try{fakePack.Validate();}catch(InvalidDataException){rejected=true;}
        Check(rejected,"embedded release cannot override official contact URLs");
        RunAsync(root,pack).GetAwaiter().GetResult();return count;
    }
    static async Task RunAsync(string root,PackRelease pack)
    {
        int calls=0;bool rejected=false;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>{calls++;var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new Uri("http://127.0.0.1/private");return response;}}))
        {try{await client.Check(pack.packId,CancellationToken.None);}catch(InvalidDataException){rejected=true;}}
        Check(rejected&&calls==1,"redirect to HTTP or a local service blocked before connecting");
        calls=0;rejected=false;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>{calls++;var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=uri;return response;}}))
        {try{await client.Check(pack.packId,CancellationToken.None);}catch(InvalidDataException){rejected=true;}}
        Check(rejected&&calls==6,"redirect loops bounded");
        rejected=false;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new string('x',1048577))}}))
        {try{await client.Check(pack.packId,CancellationToken.None);}catch(InvalidDataException){rejected=true;}}
        Check(rejected,"oversized metadata rejected");
        byte[] payload=new byte[2048];string fixture=Path.Combine(root,"security-update.exe");File.WriteAllBytes(fixture,payload);
        var update=new UpdateManifest{schemaVersion=1,appVersion="9.9.9",packVersion=pack.packVersion,packId=pack.packId,packSha1=pack.sha1,packSha256=pack.sha256,exeName="PackHelper-9.9.9.exe",exeSize=payload.Length,exeSha256=PackInstall.Hash(fixture,true),downloadUrl=TrustedUrls.Repository+"/releases/download/v9.9.9/PackHelper-9.9.9.exe"};
        UpdateTests.Sign(update);calls=0;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>{calls++;if(uri.Host=="github.com"){var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new Uri("https://release-assets.githubusercontent.com/asset?signature=test");return response;}return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(payload)};}}))
        {string file=await client.Download(update,pack.packId,Path.Combine(root,"cdn-update"),null,CancellationToken.None);Check(calls==2&&PackInstall.Hash(file,true)==update.exeSha256,"GitHub release CDN redirect remains functional with signed updates");}
        update.exeSha256=new string('b',64);calls=0;rejected=false;
        using(var client=new UpdateClient(new FakeHttp{Reply=uri=>{calls++;throw new Exception("must not connect");}}))
        {try{await client.Download(update,pack.packId,Path.Combine(root,"untrusted-update"),null,CancellationToken.None);}catch(InvalidDataException){rejected=true;}}
        Check(rejected&&calls==0,"untrusted update blocked before download");
        string cache=Path.Combine(root,"untrusted-status");new PublicStatusCache{Latest=update,Downloads=100}.Save(cache);
        Check(PublicStatusCache.Read(cache,pack.packId).Latest==null,"locally edited update cache cannot bypass signature checks");
    }
}
