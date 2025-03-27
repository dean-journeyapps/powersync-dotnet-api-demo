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

            // return Ok(new { token, powersync_url = _config.Url });
            return Ok(new { token = "eyJhbGciOiJSUzI1NiIsImtpZCI6InBvd2Vyc3luYy1kZXYtMzIyM2Q0ZTMifQ.eyJzdWIiOiJjYzFiYTRjMy0zYTQyLTQyNTAtOTU5Yi0xNTA4YzQzYWY3MTIiLCJpYXQiOjE3NDMwOTIxMzcsImlzcyI6Imh0dHBzOi8vcG93ZXJzeW5jLWFwaS5qb3VybmV5YXBwcy5jb20iLCJhdWQiOiJodHRwczovLzY3ZTJiZGJlYWIyYzUwOTBjOWM4MjY5Yy5wb3dlcnN5bmMuam91cm5leWFwcHMuY29tIiwiZXhwIjoxNzQzMTM1MzM3fQ.tpsmsBhd-BL59ypUnsL1sh57_qWFw7uGzDe3AJvvCL98bSI4qnFL7ZU4q0yxO2ojHio4PzPWW56NNpN-9yS11XEddT1ldRU6aXjkyzYvzoBinRRIdIo-dQv2d9f_Ae7P_uTvbkCGmAaTmgGAwLDxdx1s_bQXLeQMEyr0I2ESlRuJDngaMOam9lvKAnjvS4m5MdNDZkdm5OLgfzPGpUMlAuxc4YdXH19NBwEYxxejfxt_XEehgPIc4_Jdm52Nn-FJhgN3qxJ9H-Zzn8ij4b1O3B1kQMZ31iY7CUHap48Tibit39iuylt3FhPjLT862YeU5cNqHTjiaGFmn-NPv4ydKA", powersync_url = _config.Url });
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
