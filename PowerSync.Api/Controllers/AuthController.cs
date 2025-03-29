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
            //_kid = $"powersync-dev-{Guid.NewGuid():N}";
            _kid = "powersync-dev-3223d4e3";
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

            string powerSyncInstanceUrl = _config.PowerSyncUrl?.TrimEnd('/') ?? throw new InvalidOperationException("PowerSync URL must be configured");

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                { "sub", user_id },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddMinutes(5).ToUnixTimeSeconds() }, 
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

            token = "eyJhbGciOiJSUzI1NiIsImtpZCI6InBvd2Vyc3luYy1kZXYtMzIyM2Q0ZTMifQ.eyJzdWIiOiI4MWFmYjQyZC02YjA1LTQ0NTAtODA0OC04NDc0MTQ3OGE2M2IiLCJpYXQiOjE3NDMyMzA2OTksImlzcyI6Imh0dHBzOi8vcG93ZXJzeW5jLWFwaS5qb3VybmV5YXBwcy5jb20iLCJhdWQiOiJodHRwczovLzY3ZTJiZGJlYWIyYzUwOTBjOWM4MjY5Yy5wb3dlcnN5bmMuam91cm5leWFwcHMuY29tIiwiZXhwIjoxNzQzMjczODk5fQ.BY1dFZFd_1CcEFOew98TjjjRqji8QmtvnC1PoWcoQJ4ZF87SPv44mbKP06mtpDNQ8Nu9FZEi-SE2QhGWLNIUn7pc3YzW0O6yd1tfZE4ooKI3LP9mgcZCCnKgPJLMJ2mb5anUefaLE8VIBu-l0KwQ9KFyzCXgrZk1StdjXVH7V982YJ8-dvz-jaFETuHHpocz2FnFLfuntg4Xx50TFzypqm9s-Clm6kl3wf7tPkT11buB8fQGav6Uh-rDMe5Dg42sW3hLDZegvuuTSzvRQEHkkJywr1WIm8I1N9ct6UuIa3oAlZAYRQ5XxqLbjN8_51YCKK2j0idWDv0qhiZn5x6WOQ";

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