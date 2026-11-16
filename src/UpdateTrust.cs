using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace LavaBox.PackHelper
{
    public static class UpdateTrust
    {
#if PACKHELPER_TESTS
        internal static string TestPublicKey;
#endif
        public static byte[] Payload(UpdateManifest update)
        {
            // Fixed field order and invariant decimal encoding; all fields are validated first.
            return Encoding.UTF8.GetBytes(String.Join("\n", new[] {
                "PackHelper.Update.v1", update.schemaVersion.ToString(CultureInfo.InvariantCulture),
                update.appVersion, update.packVersion, update.packId, update.packSha1, update.packSha256,
                update.exeName, update.exeSize.ToString(CultureInfo.InvariantCulture), update.exeSha256,
                update.downloadUrl
            }));
        }
        public static void Verify(UpdateManifest update, string packId)
        {
            update.Validate(packId);
            try
            {
                string publicKey;
#if PACKHELPER_TESTS
                publicKey = TestPublicKey;
#else
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackHelper.UpdatePublicKey.xml"))
                using (var reader = new StreamReader(stream)) publicKey = reader.ReadToEnd();
#endif
                if (String.IsNullOrEmpty(update.signature) || update.signature.Length > 1024)
                    throw new InvalidDataException("У выпуска нет подписи разработчика. Обновление заблокировано.");
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(publicKey);
                    if (!rsa.VerifyData(Payload(update), CryptoConfig.MapNameToOID("SHA256"), Convert.FromBase64String(update.signature)))
                        throw new InvalidDataException("Подпись обновления не прошла проверку. Файл не будет запущен.");
                }
            }
            catch (FormatException) { throw new InvalidDataException("Повреждена подпись обновления."); }
            catch (CryptographicException) { throw new InvalidDataException("Не удалось проверить подпись обновления."); }
        }
    }
}
