using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
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
        }

        private void EnsureKeys()
        {
            if (_privateKey.HasValue && _publicKey.HasValue && _kid != null)
                return;

            if (string.IsNullOrEmpty(_config.PrivateKey))
            {
                _logger.LogWarning("Private key not found. Generating a temporary key pair.");
                GenerateKeyPair();
            }
            else
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(_config.PrivateKey), out _);
                _privateKey = rsa.ExportParameters(true);
                _publicKey = rsa.ExportParameters(false);
                //_kid = _config.Kid ?? Guid.NewGuid().ToString();
                _kid = Guid.NewGuid().ToString();
            }
        }

        private void GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);
            _privateKey = rsa.ExportParameters(true);
            _publicKey = rsa.ExportParameters(false);
            _kid = $"powersync-{Guid.NewGuid():N}";
        }

        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
            EnsureKeys();
            if (!_privateKey.HasValue || _kid == null)
                return BadRequest("Unable to generate token");

            using var rsa = RSA.Create();
            rsa.ImportParameters(_privateKey.Value);

            var payload = new Dictionary<string, object>
            {
                { "sub", user_id ?? "UserID" },
                { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                //{ "iss", _config.JwtIssuer! },
                {"iss", "powersync-dev"},
                { "aud", _config.Url! },
                { "exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }
            };

            var headers = new Dictionary<string, object>
            {
                { "alg", "RS256" },
                { "typ", "JWT" },
                { "kid", _kid }
            };

            string token = JWT.Encode(payload, rsa, JwsAlgorithm.RS256, headers);

            return Ok(new { token, powersync_url = _config.Url });
        }

        [HttpGet("keys")]
        public IActionResult GetKeys()
        {
            EnsureKeys();
            if (!_publicKey.HasValue)
                return BadRequest("No public keys available");

            var rsaParams = _publicKey.Value;
            // var jwk = new
            // {
            //     kty = "RSA",
            //     alg = "RS256",
            //     kid = _kid,
            //     n = Convert.ToBase64String(rsaParams.Modulus!),
            //     e = Convert.ToBase64String(rsaParams.Exponent!)
            // };
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

        string Base64UrlEncode(byte[] input) => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
