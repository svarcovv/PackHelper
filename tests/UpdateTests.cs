using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LavaBox.PackHelper;

static class UpdateTests
{
    sealed class FakeHttp : HttpMessageHandler
    {
        public Func<HttpRequestMessage,HttpResponseMessage> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { token.ThrowIfCancellationRequested();return Task.FromResult(Reply(request)); }
    }
    static int count;
    static readonly RSACryptoServiceProvider TestSigner = new RSACryptoServiceProvider(2048);
    public static void Sign(UpdateManifest update)
    {
        TestSigner.PersistKeyInCsp=false;
        UpdateTrust.TestPublicKey=TestSigner.ToXmlString(false);
        update.signature=Convert.ToBase64String(TestSigner.SignData(UpdateTrust.Payload(update),CryptoConfig.MapNameToOID("SHA256")));
    }
    static void Check(bool pass,string name){if(!pass)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
    static void Reject(Action action,string name)
    { bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,name); }
    static HttpResponseMessage Json(string text){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")};}
    public static int Run(string root,PackRelease pack)
    { RunAsync(root,pack).GetAwaiter().GetResult();return count; }
    static async Task RunAsync(string root,PackRelease pack)
    {
        byte[] payload=Encoding.UTF8.GetBytes(new string('P',2048));
        string source=Path.Combine(root,"new.exe");File.WriteAllBytes(source,payload);
        var update=new UpdateManifest{schemaVersion=1,appVersion="1.2.0",packVersion="1.2.3",packId=pack.packId,
            packSha1=pack.sha1,packSha256=pack.sha256,exeName="PackHelper-1.2.0.exe",exeSize=payload.Length,
            exeSha256=PackInstall.Hash(source,true),downloadUrl="https://github.com/svarcovv/PackHelper/releases/download/v1.2.0/PackHelper-1.2.0.exe"};
        Sign(update);
        update.Validate(pack.packId);Check(UpdateManifest.VersionOf("1.10.0")>UpdateManifest.VersionOf("1.2.0"),"versions compared numerically");
        string goodUrl=update.downloadUrl;update.downloadUrl="https://example.com/payload.exe";
        Reject(()=>update.Validate(pack.packId),"non-official executable URL rejected");update.downloadUrl=goodUrl;
        Reject(()=>update.Validate(Guid.NewGuid().ToString()),"update for another resource pack rejected");
        UpdateTrust.Verify(update,pack.packId);Check(true,"signed release accepted");
        string signedHash=update.exeSha256; update.exeSha256=new string('b',64);
        Reject(()=>UpdateTrust.Verify(update,pack.packId),"metadata tampering rejected even with matching replacement hash");update.exeSha256=signedHash;
        string savedSignature=update.signature; update.signature=null;
        Reject(()=>UpdateTrust.Verify(update,pack.packId),"unsigned release blocked");update.signature=savedSignature;
        using(var impostor=new RSACryptoServiceProvider(2048))
        {impostor.PersistKeyInCsp=false;update.signature=Convert.ToBase64String(impostor.SignData(UpdateTrust.Payload(update),CryptoConfig.MapNameToOID("SHA256")));}
        Reject(()=>UpdateTrust.Verify(update,pack.packId),"signature from untrusted publisher blocked");update.signature=savedSignature;
        string metadata=new JavaScriptSerializer().Serialize(update);
        string asset="https://github.com/svarcovv/PackHelper/releases/download/v1.2.0/update.json";
        using(var client=new UpdateClient(new FakeHttp{Reply=request=>request.RequestUri.Host=="api.github.com"?Json("{\"assets\":[{\"name\":\"update.json\",\"browser_download_url\":\""+asset+"\"}]}"):Json(metadata)}))
        {var result=await client.Check(pack.packId,CancellationToken.None);Check(result.Manifest.appVersion=="1.2.0","latest release metadata loaded and validated");}
        using(var client=new UpdateClient(new FakeHttp{Reply=request=>new HttpResponseMessage(HttpStatusCode.NotFound)}))
        {Check((await client.Check(pack.packId,CancellationToken.None)).NoPublishedRelease,"unpublished release handled without breaking offline installation");}
        using(var client=new UpdateClient(new FakeHttp{Reply=request=>Json("{\"assets\":[]}")}))
        {Check((await client.Check(pack.packId,CancellationToken.None)).ManualDownload,"older release without updater gets manual fallback");}
        string cache=Path.Combine(root,"updates");
        using(var client=new UpdateClient(new FakeHttp{Reply=request=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(payload)}}))
        {
            string downloaded=await client.Download(update,pack.packId,cache,null,CancellationToken.None);
            Check(PackInstall.Hash(downloaded,true)==update.exeSha256,"downloaded executable hash checked");
            string again=await client.Download(update,pack.packId,cache,null,CancellationToken.None);
            Check(downloaded==again,"verified update cache reused");
        }
        bool bad=false;
        using(var client=new UpdateClient(new FakeHttp{Reply=request=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[payload.Length])}}))
        {try{await client.Download(update,pack.packId,Path.Combine(root,"bad-updates"),null,CancellationToken.None);}catch(InvalidDataException){bad=true;}}
        Check(bad,"corrupted update rejected");
        Check(Directory.GetFiles(Path.Combine(root,"bad-updates"),"*.partial",SearchOption.AllDirectories).Length==0,"partial update removed after failure");
        string target=Path.Combine(root,"PackHelper.exe");File.WriteAllText(target,"old executable");string old=PackInstall.Hash(target,true);
        SelfUpdate.Replace(source,target,old,update.exeSha256);
        Check(PackInstall.Hash(target,true)==update.exeSha256&&File.ReadAllText(target+".previous")=="old executable","self-update replaces executable atomically and preserves previous file");
        File.WriteAllText(target,"unexpected user change");
        Reject(()=>SelfUpdate.Replace(source,target,update.exeSha256,update.exeSha256),"changed target not overwritten by updater");
        Check(File.ReadAllText(target)=="unexpected user change","failed replacement preserves current executable");
        Check(SelfUpdate.Quote(@"C:\Folder with spaces\")=="\"C:\\Folder with spaces\\\\\"","Windows update arguments quote trailing backslashes");
        Check(!CacheState.Read(null,pack.packId,pack.sha1).Installed,"installed-pack check requires a selected profile");
    }
}
