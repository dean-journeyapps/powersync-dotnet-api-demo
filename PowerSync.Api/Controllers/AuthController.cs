using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Jose;
using PowerSync.Infrastructure.Configuration;
using PowerSync.Infrastructure.Utils;

namespace PowerSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PowerSyncConfig _config;
        private readonly ILogger<AuthController> _logger;
        private static string? _privateKey;
        private static string? _publicKey;
        private static string? _kid;

        public AuthController(
            IOptions<PowerSyncConfig> config,
            ILogger<AuthController> logger)
        {
            _config = config.Value;
            _logger = logger;
            EnsureKeys();
        }

        private static void EnsureKeys()
        {
            if (!string.IsNullOrEmpty(_privateKey) && !string.IsNullOrEmpty(_publicKey) && _kid != null)
                return;

            (_privateKey, _publicKey, _kid) = KeyPairGenerator.GenerateKeyPair();
        }

        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
            if (string.IsNullOrEmpty(user_id))
                return BadRequest("User ID is required");

            if (string.IsNullOrEmpty(_privateKey) || _kid == null)
                return BadRequest("Unable to generate token");

            using var rsa = RSA.Create();
            rsa.ImportFromPem(_privateKey);

            string powerSyncInstanceUrl = _config.PowerSyncUrl?.TrimEnd('/') ?? throw new InvalidOperationException("PowerSync URL must be configured");

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                { "sub", user_id },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddHours(12).ToUnixTimeSeconds() }, 
                { "aud", powerSyncInstanceUrl },
                { "iss", _config.JwtIssuer! }
            };

            var headers = new Dictionary<string, object>
            {
                { "alg", "RS256" },
                { "kid", _kid }
            };

            string token = JWT.Encode(payload, rsa, JwsAlgorithm.RS256, headers);

            _logger.LogInformation($"Audience value: {powerSyncInstanceUrl}");

            return Ok(new { token, powersync_url = powerSyncInstanceUrl });
        }

        [HttpGet("keys")]
        public IActionResult GetKeys()
        {
            if (string.IsNullOrEmpty(_publicKey))
                return BadRequest("No public keys available");

            var jwk = new
            {
                kty = "RSA",
                alg = "RS256",
                kid = _kid,
                n = _publicKey
            };

            return Ok(new { keys = new[] { jwk } });
        }
    }
}
