using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PowerSync.Infrastructure.Utils
{
    public class KeyPairGenerator
    {
        public static (string privateKey, string publicKey, string kid) GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);

            // Generate a random key identifier
            var kid = $"powersync-{GenerateRandomHex(5)}";

            string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            string publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());

            return (privateKey, publicKey, kid);
        }

        private static string GenerateRandomHex(int byteLength)
        {
            var randomBytes = new byte[byteLength];
            RandomNumberGenerator.Fill(randomBytes);
            return Convert.ToHexString(randomBytes).ToLowerInvariant();
        }
    }
}