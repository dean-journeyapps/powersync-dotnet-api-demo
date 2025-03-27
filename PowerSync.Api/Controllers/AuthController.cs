using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Jose;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerSync.Domain.Records;
using PowerSync.Infrastructure.Configuration;
using PowerSync.Infrastructure.Utils;

namespace PowerSync.Api.Controllers
{
    /// <summary>
    /// Controller for handling PowerSync authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PowerSyncConfig _config;
        private readonly ILogger<AuthController> _logger;
        private static string? _privateKey;
        private static string? _publicKey;
        private static string? _kid; // Added key identifier

        public AuthController(
            IOptions<PowerSyncConfig> config,
            ILogger<AuthController> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Ensures JWT keys are loaded
        /// </summary>
        private void EnsureKeysAsync()
        {
            // If keys are already loaded, return
            if (_privateKey != null && _publicKey != null && _kid != null)
                return;

            // Check if private key is in configuration
            if (string.IsNullOrEmpty(_config.PrivateKey))
            {
                _logger.LogWarning("Private key not found in configuration. Generating temporary key pair.");

                // Generate a temporary key pair
                var (privateBase64, publicBase64, kid) = KeyPairGenerator.GenerateKeyPair();

                _config.PrivateKey = privateBase64;
                _config.PublicKey = publicBase64;
                _kid = kid; 
            }

            _privateKey = _config.PrivateKey;
            _publicKey = _config.PublicKey;
            _kid ??= Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Generates a PowerSync authentication token
        /// </summary>
        [HttpGet("token")]
        public IActionResult GenerateToken([FromQuery] string? user_id)
        {
            EnsureKeysAsync();

            // Validate private key
            if (_privateKey == null || _kid == null)
                return BadRequest("Unable to generate token");

            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(_privateKey), out _);

            // Create payload
            var payload = new Dictionary<string, object>
            {
                { "sub", user_id ?? "UserID" },
                { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "iss", _config.JwtIssuer! },
                { "aud", _config.Url! },
                { "exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }
            };

            // Create header with explicit algorithm and key ID
            var headers = new Dictionary<string, object>
            {
                { "alg", "RS256" },
                { "typ", "JWT" },
                { "kid", _kid }
            };

            string token = JWT.Encode(payload, rsa, JwsAlgorithm.RS256, headers);

            return Ok(new TokenResponse
            {
                Token = token,
                PowersyncUrl = _config.Url
            });
        }

        /// <summary>
        /// JWKS endpoint for PowerSync authentication
        /// </summary>
        [HttpGet("keys")]
        public IActionResult GetKeys()
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
    }
}