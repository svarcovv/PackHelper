using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LavaBox.PackHelper
{
    public sealed class PublicStatusCache
    {
        public UpdateManifest Latest;
        public DateTime ReleaseCheckedUtc;
        public long? Downloads;
        public bool DownloadsComplete;
        public DateTime DownloadsCheckedUtc;
        public static bool Fresh(DateTime utc)
        {var age=DateTime.UtcNow-utc.ToUniversalTime();return age>=TimeSpan.Zero&&age<TimeSpan.FromMinutes(1);}
        public static PublicStatusCache Read(string root,string packId)
        {
            try
            {
                string file=Path.Combine(root,"public-status.json");PackPaths.RejectLinks(file);
                if(!File.Exists(file)||new FileInfo(file).Length>65536)return new PublicStatusCache();
                var result=new JavaScriptSerializer().Deserialize<PublicStatusCache>(File.ReadAllText(file,Encoding.UTF8));
                if(result==null||result.Downloads<0)return new PublicStatusCache();
                if(result.Latest!=null)result.Latest.Validate(packId);
                return result;
            }
            catch(IOException){}catch(InvalidDataException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
            return new PublicStatusCache();
        }
        public void Save(string root)
        {
            string temp=null;
            try
            {
                PackPaths.RejectLinks(root);Directory.CreateDirectory(root);string target=Path.Combine(root,"public-status.json");PackPaths.RejectLinks(target);
                temp=Path.Combine(root,"public-status-"+Guid.NewGuid().ToString("N")+".tmp");
                File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
            finally{if(temp!=null)try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
