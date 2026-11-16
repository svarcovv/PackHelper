using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PackHelperBuilder
{
    public static class ReleaseSigning
    {
        public const string KeyName = "PackHelper.ReleaseSigning.v1";
        public static string Sign(string metadata, string publicKeyFile)
        {
            var json = new JavaScriptSerializer();
            var update = json.Deserialize<Dictionary<string, object>>(metadata);
            var fields = new List<string> { "PackHelper.Update.v1" };
            foreach (string name in new[] { "schemaVersion", "appVersion", "packVersion", "packId", "packSha1", "packSha256", "exeName", "exeSize", "exeSha256", "downloadUrl" })
                fields.Add(Convert.ToString(update[name], CultureInfo.InvariantCulture));
            var parameters = new CspParameters(24) { KeyContainerName = KeyName, Flags = CspProviderFlags.UseExistingKey };
            using (var rsa = new RSACryptoServiceProvider(parameters))
            {
                if (rsa.ToXmlString(false) != File.ReadAllText(publicKeyFile).Trim())
                    throw new InvalidDataException("Ключ Windows не соответствует ключу проверки приложения.");
                update["signature"] = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(String.Join("\n", fields)), CryptoConfig.MapNameToOID("SHA256")));
            }
            return json.Serialize(update);
        }
    }
}
