using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Jose;
using PowerSync.Infrastructure.Configuration;

namespace PowerSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PowerSyncConfig _config;
        private readonly ILogger<AuthController> _logger;
        private static RSAParameters? _privateKey;
        private static RSAParameters? _publicKey;
        private static string? _kid;

        public AuthController(
            IOptions<PowerSyncConfig> config,
            ILogger<AuthController> logger)
        {
            _config = config.Value;
            _logger = logger;
            EnsureKeys();
        }

        private void EnsureKeys()
        {
            if (_privateKey.HasValue && _publicKey.HasValue && _kid != null)
                return;

            using var rsa = RSA.Create(2048);
            _privateKey = rsa.ExportParameters(true);
            _publicKey = rsa.ExportParameters(false);
            _kid = $"powersync-{Guid.NewGuid():N}";
        }

        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
            if (string.IsNullOrEmpty(user_id))
                return BadRequest("User ID is required");

            if (!_privateKey.HasValue || _kid == null)
                return BadRequest("Unable to generate token");

            using var rsa = RSA.Create();
            rsa.ImportParameters(_privateKey.Value);

            string powerSyncInstanceUrl = _config.Url ?? throw new InvalidOperationException("PowerSync URL must be configured");

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                { "sub", user_id },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddMinutes(5).ToUnixTimeSeconds() }, 
                { "aud", powerSyncInstanceUrl }
            };

            var headers = new Dictionary<string, object>
            {
                { "alg", "RS256" },
                { "kid", _kid }
            };

            string token = JWT.Encode(payload, rsa, JwsAlgorithm.RS256, headers);

            return Ok(new { token, powersync_url = powerSyncInstanceUrl });
        }

        [HttpGet("keys")]
        public IActionResult GetKeys()
        {
            if (!_publicKey.HasValue)
                return BadRequest("No public keys available");

            var rsaParams = _publicKey.Value;
            var jwk = new
            {
                kty = "RSA",
                alg = "RS256",
                kid = _kid,
                n = Base64UrlEncode(rsaParams.Modulus!),
                e = Base64UrlEncode(rsaParams.Exponent!)
            };

            return Ok(new { keys = new[] { jwk } });
        }

        private string Base64UrlEncode(byte[] input) => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}