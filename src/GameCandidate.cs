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
        public string GameDirectory;
        public string CacheDirectory;
        public string Description;
        public bool Isolated;
        public bool NeedsCacheEvidence;
        public override string ToString() { return Description + " - " + GameDirectory; }
        public string Destination(PackRelease release)
        { return Path.Combine(CacheDirectory, release.packId, release.sha1); }
    }

}
