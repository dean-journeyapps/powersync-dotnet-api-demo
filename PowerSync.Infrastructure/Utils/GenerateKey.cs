using System.Security.Cryptography;
using System.Text.Json;

namespace PowerSync.Infrastructure.Utils
{
    public class KeyPairGenerator
    {
        public static (string privateBase64, string publicBase64) GenerateKeyPair()
        {
            // Use modern cryptography APIs
            using var rsa = RSA.Create(2048);
            
            // Generate a random key identifier
            var kid = $"powersync-{GenerateRandomHex(5)}";
            
            // Create public key JWK
            var publicJwk = new Dictionary<string, object>
            {
                { "kty", "RSA" },
                { "kid", kid },
                { "alg", "RS256" },
                { "n", Base64UrlEncode(rsa.ExportParameters(false).Modulus!) },
                { "e", Base64UrlEncode(rsa.ExportParameters(false).Exponent!) }
            };

            // Create private key JWK with all necessary parameters
            var privateJwk = new Dictionary<string, object>
            {
                { "kty", "RSA" },
                { "kid", kid },
                { "alg", "RS256" },
                { "n", Base64UrlEncode(rsa.ExportParameters(true).Modulus!) },
                { "e", Base64UrlEncode(rsa.ExportParameters(true).Exponent!) },
                { "d", Base64UrlEncode(rsa.ExportParameters(true).D!) },
                { "p", Base64UrlEncode(rsa.ExportParameters(true).P!) },
                { "q", Base64UrlEncode(rsa.ExportParameters(true).Q!) },
                { "dp", Base64UrlEncode(rsa.ExportParameters(true).DP!) },
                { "dq", Base64UrlEncode(rsa.ExportParameters(true).DQ!) },
                { "qi", Base64UrlEncode(rsa.ExportParameters(true).InverseQ!) }
            };

            // Serialize and Base64 encode the JWKs
            var privateBase64 = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(privateJwk))
            );

            var publicBase64 = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(publicJwk))
            );

            return (privateBase64, publicBase64);
        }

        private static string GenerateRandomHex(int byteLength)
        {
            // Use RandomNumberGenerator instead of the obsolete RNGCryptoServiceProvider
            var randomBytes = new byte[byteLength];
            RandomNumberGenerator.Fill(randomBytes);
            return Convert.ToHexString(randomBytes).ToLowerInvariant();
        }

        private static string Base64UrlEncode(byte[] input)
        {
            // Align with Base64UrlDecode in AuthController
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}