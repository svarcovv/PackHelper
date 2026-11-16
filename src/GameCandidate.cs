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
    public sealed class GameCandidate
    {
        public string GameDirectory { get; set; }
        public string CacheDirectory;
        public string Description;
        public bool Isolated;
        public bool NeedsCacheEvidence;
        public string DisplayName
        {
            get
            {
                string name=Path.GetFileName(GameDirectory.TrimEnd(Path.DirectorySeparatorChar));
                if(name.Equals(".minecraft",StringComparison.OrdinalIgnoreCase)||name.Equals("minecraft",StringComparison.OrdinalIgnoreCase))
                {string parent=Path.GetFileName(Path.GetDirectoryName(GameDirectory));if(!String.IsNullOrEmpty(parent)&&!parent.Equals("Roaming",StringComparison.OrdinalIgnoreCase))name=parent;else name="Minecraft";}
                return name+(Isolated?" · особый кэш":"");
            }
        }
        public override string ToString() { return Description + " - " + GameDirectory; }
        public string Destination(PackRelease release)
        { return Path.Combine(CacheDirectory, release.packId, release.sha1); }
    }

}
