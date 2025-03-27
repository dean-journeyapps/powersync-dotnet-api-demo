namespace PowerSync.Infrastructure.Configuration
{
    /// <summary>
    /// Secure configuration for PowerSync authentication
    /// </summary>
    public class PowerSyncConfig
    {
        public const string SectionName = "PowerSync";

        public string? PrivateKey { get; set; }
        public string? PublicKey { get; set; }
        public string? JwtIssuer { get; set; }
        public string? Url { get; set; }
        public string? DatabaseType { get; set; }
        public string? DatabaseUri { get; set; }

        public bool ValidateConfiguration(out List<string> validationErrors)
        {
            validationErrors = [];

            // Validate required configuration fields
            if (string.IsNullOrWhiteSpace(JwtIssuer))
                validationErrors.Add("JWT Issuer is required");

            if (string.IsNullOrWhiteSpace(Url))
                validationErrors.Add("PowerSync URL is required");

            if (string.IsNullOrWhiteSpace(DatabaseType))
                validationErrors.Add("Database Type is required");

            if (string.IsNullOrWhiteSpace(DatabaseUri))
                validationErrors.Add("Database URI is required");

            // Optional: Add more specific validations
            if (!string.IsNullOrWhiteSpace(Url) && !Uri.TryCreate(Url, UriKind.Absolute, out _))
                validationErrors.Add("Invalid PowerSync URL format");

            // Optional: Validate supported database types
            var supportedDatabaseTypes = new[] { "postgres", "mysql", "sqlite" };
            if (!string.IsNullOrWhiteSpace(DatabaseType) && 
                !supportedDatabaseTypes.Contains(DatabaseType.ToLowerInvariant()))
                validationErrors.Add($"Unsupported database type. Supported types are: {string.Join(", ", supportedDatabaseTypes)}");

            // Optional: Basic validation for keys if they are used
            if (!string.IsNullOrWhiteSpace(PrivateKey) && PrivateKey.Length < 16)
                validationErrors.Add("Private key seems too short and may be invalid");

            if (!string.IsNullOrWhiteSpace(PublicKey) && PublicKey.Length < 16)
                validationErrors.Add("Public key seems too short and may be invalid");

            return validationErrors.Count == 0;
        }
    }
}