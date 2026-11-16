using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PackHelperBuilder
{
    static class ReleaseBuild
    {
        static string Hash(string file,string algorithm)
        {using(var input=File.OpenRead(file))using(var hash=HashAlgorithm.Create(algorithm))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
        static string Q(string value){return "\""+value.Replace("\"","\\\"")+"\"";}
        public static string Run(string root,string pack,string appVersion,string packVersion,string output)
        {
            if(!Regex.IsMatch(appVersion,"\\A[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}\\z")||!Regex.IsMatch(packVersion,"\\A[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}\\z"))throw new Exception("Укажи версии в формате 1.2.3.");
            pack=Path.GetFullPath(pack);root=Path.GetFullPath(root);output=Path.GetFullPath(output);
            if(!File.Exists(pack)||new FileInfo(pack).Length>67108864)throw new Exception("Выбери ZIP ресурспака размером до 64 МБ.");
            var json=new JavaScriptSerializer();
            using(var zip=ZipFile.OpenRead(pack))
            {
                var entry=zip.GetEntry("pack.mcmeta");if(entry==null||entry.Length>1048576)throw new Exception("В корне ZIP нужен pack.mcmeta.");
                using(var reader=new StreamReader(entry.Open()))
                {var metadata=json.DeserializeObject(reader.ReadToEnd()) as Dictionary<string,object>;if(metadata==null||!metadata.ContainsKey("pack")||!(metadata["pack"] is Dictionary<string,object>))throw new Exception("Неверный pack.mcmeta.");}
            }
            Directory.CreateDirectory(output);string build=Path.Combine(output,".build");Directory.CreateDirectory(build);
            string id="4e7502b2-1883-44d9-a2b4-03cfd5884e13",sha1=Hash(pack,"SHA1"),sha256=Hash(pack,"SHA256");
            string manifest=Path.Combine(output,"pack-release.json"),exe=Path.Combine(output,"PackHelper-"+appVersion+".exe");
            var utf8=new UTF8Encoding(false);
            File.WriteAllText(manifest,json.Serialize(new{appVersion=appVersion,packVersion=packVersion,packId=id,sha1=sha1,sha256=sha256,size=new FileInfo(pack).Length,server="lavabox.fun",sourceUrl="https://github.com/svarcovv/PackHelper",discordUrl="https://discord.gg/xMBKSnDvt",developerUrl="https://t.me/AntiDoDmiGEI"}),utf8);
            string version=Path.Combine(build,"BuildVersion.cs");
            File.WriteAllText(version,"[assembly: System.Reflection.AssemblyVersion(\""+appVersion+".0\")]\n[assembly: System.Reflection.AssemblyFileVersion(\""+appVersion+".0\")]",utf8);
            string framework=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Microsoft.NET",Environment.Is64BitOperatingSystem?"Framework64":"Framework","v4.0.30319");
            string compiler=Path.Combine(framework,"csc.exe");if(!File.Exists(compiler))throw new Exception("Не найден компилятор .NET Framework.");
            var args=new List<string>{"/nologo","/target:winexe","/platform:anycpu","/optimize+","/codepage:65001","/r:System.Windows.Forms.dll","/r:System.Management.dll","/r:System.Web.Extensions.dll","/r:System.Net.Http.dll","/r:System.Xml.Linq.dll","/r:System.Xaml.dll",
                "/out:"+exe,"/win32icon:"+Path.Combine(root,"assets","packhelper.ico"),"/win32manifest:"+Path.Combine(root,"app.manifest"),"/resource:"+pack+",LavaBox.Pack.zip","/resource:"+manifest+",LavaBox.Release.json","/resource:"+Path.Combine(root,"src","Window.xaml")+",PackHelper.Window.xaml"};
            foreach(string dll in new[]{"PresentationCore.dll","PresentationFramework.dll","WindowsBase.dll"})args.Add("/r:"+Path.Combine(framework,"WPF",dll));
            args.Add("/resource:"+Path.Combine(root,"assets","update-public-key.xml")+",PackHelper.UpdatePublicKey.xml");
            args.AddRange(Directory.GetFiles(Path.Combine(root,"src"),"*.cs"));args.Add(version);
            args.Add("/resource:"+Path.Combine(root,"src","Theme.xaml")+",PackHelper.Theme.xaml");
            args.Add("/resource:"+Path.Combine(root,"assets","packhelper.svg")+",PackHelper.App.svg");
            args.Add("/resource:"+Path.Combine(root,"LICENSE")+",PackHelper.Licenses.PackHelper.txt");
            foreach(string icon in Directory.GetFiles(Path.Combine(root,"assets","icons")))args.Add("/resource:"+icon+","+(Path.GetExtension(icon)==".svg"?"PackHelper.Icons.":"PackHelper.Licenses.")+Path.GetFileName(icon));
            using(var process=Process.Start(new ProcessStartInfo(compiler,String.Join(" ",args.Select(Q))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}))
            {string message=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(message);}
            string update=Path.Combine(output,"update.json");
            File.WriteAllText(update,json.Serialize(new{schemaVersion=1,appVersion=appVersion,packVersion=packVersion,packId=id,packSha1=sha1,packSha256=sha256,exeName=Path.GetFileName(exe),exeSize=new FileInfo(exe).Length,exeSha256=Hash(exe,"SHA256"),downloadUrl="https://github.com/svarcovv/PackHelper/releases/download/v"+appVersion+"/"+Path.GetFileName(exe)}),utf8);
            File.WriteAllText(update,ReleaseSigning.Sign(File.ReadAllText(update),Path.Combine(root,"assets","update-public-key.xml")),utf8);
            File.WriteAllLines(Path.Combine(output,"SHA256SUMS.txt"),new[]{exe,manifest,update}.Select(file=>Hash(file,"SHA256")+"  "+Path.GetFileName(file)),utf8);
            return output;
        }
    }
}
