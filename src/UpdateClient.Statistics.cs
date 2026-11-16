using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LavaBox.PackHelper
{
    public sealed class DownloadStatistics
    {
        public long Count;
        public bool Complete;
    }
    public sealed partial class UpdateClient
    {
        public async Task<DownloadStatistics> Statistics(CancellationToken cancel)
        {
            var result=new DownloadStatistics();var serializer=new JavaScriptSerializer();
            for(int page=1;page<=10;page++)
            {
                string json=await Json("https://api.github.com/repos/svarcovv/PackHelper/releases?per_page=30&page="+page,cancel);
                if(json==null)throw new IOException("Статистика GitHub недоступна.");
                var releases=serializer.DeserializeObject(json) as object[];
                if(releases==null)throw new InvalidDataException("Не удалось прочитать статистику GitHub.");
                foreach(var item in releases)
                {
                    var release=item as Dictionary<string,object>;object value;
                    if(release==null)continue;
                    if(release.TryGetValue("draft",out value)&&Object.Equals(value,true)||release.TryGetValue("prerelease",out value)&&Object.Equals(value,true))continue;
                    if(!release.TryGetValue("assets",out value)||!(value is object[]))continue;
                    foreach(var entry in (object[])value)
                    {
                        var asset=entry as Dictionary<string,object>;object name,state,count;
                        if(asset==null||!asset.TryGetValue("name",out name)||!(name is string)||!Regex.IsMatch((string)name,"\\APackHelper-[0-9]+\\.[0-9]+\\.[0-9]+\\.exe\\z"))continue;
                        if(!asset.TryGetValue("state",out state)||!Object.Equals(state,"uploaded"))continue;
                        long number;
                        if(!asset.TryGetValue("download_count",out count)||!Int64.TryParse(Convert.ToString(count,CultureInfo.InvariantCulture),NumberStyles.None,CultureInfo.InvariantCulture,out number)||number<0||number>Int64.MaxValue-result.Count)
                            throw new InvalidDataException("Неверный счётчик загрузок GitHub.");
                        result.Count+=number;
                    }
                }
                if(releases.Length<30){result.Complete=true;break;}
            }
            return result;
        }
    }
}
