using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private static RSA? _rsaPrivate;
        private static RSA? _rsaPublic;
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
            if (_rsaPrivate != null && _rsaPublic != null && _kid != null)
                return;

            var (privateKeyBase64, publicKeyBase64, keyId) = KeyPairGenerator.GenerateKeyPair();
            
            _rsaPrivate = RSA.Create();
            _rsaPrivate.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
            
            _rsaPublic = RSA.Create();
            _rsaPublic.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
            
            _kid = keyId;
        }

        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
            if (string.IsNullOrEmpty(user_id))
                return BadRequest("User ID is required");

            if (_rsaPrivate == null || _kid == null)
                return BadRequest("Unable to generate token");

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

            string token = JWT.Encode(payload, _rsaPrivate, JwsAlgorithm.RS256, headers);

            _logger.LogInformation($"Audience value: {powerSyncInstanceUrl}");

            return Ok(new { token, powersync_url = powerSyncInstanceUrl });
        }

        [HttpGet("keys")]
        public IActionResult GetKeys()
        {
            if (_rsaPublic == null || _kid == null)
                return BadRequest("No public keys available");

            var rsaParams = _rsaPublic.ExportParameters(false);
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