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

        public bool ValidateConfiguration()
        {
            return !string.IsNullOrWhiteSpace(JwtIssuer) &&
                   !string.IsNullOrWhiteSpace(Url) &&
                   !string.IsNullOrWhiteSpace(DatabaseType) &&
                   !string.IsNullOrWhiteSpace(DatabaseUri);
        }
    }
}