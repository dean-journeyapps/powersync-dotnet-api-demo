using System;
using System.Security.Cryptography;

namespace PowerSync.Infrastructure.Utils
{
    public class KeyPairGenerator
    {
        public static (string privateKey, string publicKey, string kid) GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);

            // Generate a key identifier that matches the format used in the original code
            var kid = $"powersync-dev-{GenerateRandomHex(4)}";

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