using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Jose;
using System.Security.Cryptography;
using PowerSync.Infrastructure.Configuration;
using PowerSync.Infrastructure.Utils;

namespace PowerSync.Api.Controllers
{
    /// <summary>
    /// Controller for handling PowerSync authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        IOptions<PowerSyncConfig> config,
        ILogger<AuthController> logger) : ControllerBase
    {
        private readonly PowerSyncConfig _config = config.Value;
        private readonly ILogger<AuthController> _logger = logger;

        // Thread-safe key storage
        private static Dictionary<string, object>? _privateKey;
        private static Dictionary<string, object>? _publicKey;

        /// <summary>
        /// Ensures JWT keys are loaded
        /// </summary>
        private void EnsureKeysAsync()
        {
            // If keys are already loaded, return
            if (_privateKey != null && _publicKey != null)
                return;

            // Check if private key is in configuration
            if (string.IsNullOrEmpty(_config.PrivateKey))
            {
                _logger.LogWarning("Private key not found in configuration. Generating temporary key pair.");

                // Generate a temporary key pair
                var (privateBase64, publicBase64) = KeyPairGenerator.GenerateKeyPair();

                _config.PrivateKey = privateBase64;
                _config.PublicKey = publicBase64;
            }

            // Decode and parse keys
            var privateKeyBytes = Convert.FromBase64String(_config.PrivateKey);
            var privateKeyJson = Encoding.UTF8.GetString(privateKeyBytes);
            _privateKey = JsonSerializer.Deserialize<Dictionary<string, object>>(privateKeyJson);

            var publicKeyBytes = Convert.FromBase64String(_config.PublicKey);
            var publicKeyJson = Encoding.UTF8.GetString(publicKeyBytes);
            _publicKey = JsonSerializer.Deserialize<Dictionary<string, object>>(publicKeyJson);
        }

        /// <summary>
        /// Generates a PowerSync authentication token
        /// </summary>
        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
             EnsureKeysAsync();

            // Validate private key
            if (_privateKey == null)
                return BadRequest("Unable to generate token");

            // Get algorithm and key ID from private key
            var alg = _privateKey["alg"] as string ?? Convert.ToString(_privateKey["alg"]);
            var kid = _privateKey["kid"] as string ?? Convert.ToString(_privateKey["kid"]);

            // Parse private key for signing
            var rsaKey = new RSACryptoServiceProvider();
            var rsaParameters = new RSAParameters
            {
                Modulus = Base64UrlDecode(GetString(_privateKey, "n")),
                Exponent = Base64UrlDecode(GetString(_privateKey, "e")),
                D = Base64UrlDecode(GetString(_privateKey, "d")),
                P = Base64UrlDecode(GetString(_privateKey, "p")),
                Q = Base64UrlDecode(GetString(_privateKey, "q")),
                DP = Base64UrlDecode(GetString(_privateKey, "dp")),
                DQ = Base64UrlDecode(GetString(_privateKey, "dq")),
                InverseQ = Base64UrlDecode(GetString(_privateKey, "qi"))

            };
            rsaKey.ImportParameters(rsaParameters);

            // Create JWT token
            var payload = new Dictionary<string, object>
            {
                { "sub", user_id ?? "UserID" },
                { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "iss", _config.JwtIssuer },
                { "aud", _config.Url },
                { "exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }
            };

            var token = JWT.Encode(payload, rsaKey, JwsAlgorithm.RS256, new Dictionary<string, object>
            {
                { "alg", alg },
                { "kid", kid }
            });

            return Ok(new
            {
                token = token,
                powersync_url = _config.Url
            });
        }

        /// <summary>
        /// JWKS endpoint for PowerSync authentication
        /// </summary>
        [HttpGet("keys")]
        public async Task<IActionResult> GetKeys()
        {
            EnsureKeysAsync();

            // Validate public key
            if (_publicKey == null)
                return BadRequest("No public keys available");

            return Ok(new
            {
                keys = new[] { _publicKey }
            });
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is JsonElement jsonElement)
                {
                    string? extracted = jsonElement.GetString();
                    return extracted?.Trim() ?? throw new InvalidOperationException($"Key {key} is null.");
                }
                if (value is string strValue)
                {
                    return strValue.Trim();  // Trim extra spaces or newlines
                }
            }
            throw new InvalidOperationException($"Key {key} is missing or has an invalid type.");
        }

        private static byte[] Base64UrlDecode(string input)
        {
            string base64 = input.Replace("-", "+").Replace("_", "/");
            switch (base64.Length % 4) // Pad with "=" to make it valid Base64
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}