using Jose;
using System.Security.Cryptography;
using System.Text;

namespace PowerSync.Infrastructure.Utils
{
    public class KeyPairGenerator
    {
        public static (string privateBase64, string publicBase64) GenerateKeyPair()
        {
            // Generate RSA key pair
            var alg = JwsAlgorithm.RS256;
            var kid = $"powersync-{GenerateRandomHex(5)}";

            // Manually create RSA key pair
            var rsaKey = new RSACryptoServiceProvider(2048);
        
            // Export public key
            var publicKey = rsaKey.ExportParameters(false);
            var publicJwk = new Dictionary<string, object>
            {
                { "kty", "RSA" },
                { "kid", kid },
                { "alg", alg.ToString() },
                { "n", Base64UrlEncode(publicKey.Modulus) },
                { "e", Base64UrlEncode(publicKey.Exponent) }
            };

            // Export private key
            var privateKey = rsaKey.ExportParameters(true);
            var privateJwk = new Dictionary<string, object>
            {
                { "kty", "RSA" },
                { "kid", kid },
                { "alg", alg.ToString() },
                { "n", Base64UrlEncode(privateKey!.Modulus!) },
                { "e", Base64UrlEncode(privateKey.Exponent) },
                { "d", Base64UrlEncode(privateKey.D) },
                { "p", Base64UrlEncode(privateKey.P) },
                { "q", Base64UrlEncode(privateKey.Q) },
                { "dp", Base64UrlEncode(privateKey.DP) },
                { "dq", Base64UrlEncode(privateKey.DQ) },
                { "qi", Base64UrlEncode(privateKey.InverseQ) }
            };

            // Convert to Base64
            var privateBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(privateJwk))
            );

            var publicBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(publicJwk))
            );

            return (privateBase64, publicBase64);
        }

        private static string GenerateRandomHex(int byteLength)
        {
            using var rng = new RNGCryptoServiceProvider();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            return BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return input == null ? "" : Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}