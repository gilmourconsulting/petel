namespace PetelApp.Api.Configuration
{
    public class SecuritySettings
    {
        public bool OtpEnabled { get; set; }
        public string OtpIssuer { get; set; } = "Petel System";
        
        // ✅ NEW: JWT Settings for main session tokens
        public JwtSettings Jwt { get; set; } = new JwtSettings();
    }

    public class JwtSettings
    {
        /// <summary>
        /// Secret key for signing JWT tokens (minimum 256 bits / 32 characters)
        /// MUST be stored in Azure Key Vault or Environment Variable in production
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Token issuer (e.g., "PetelSystem")
        /// </summary>
        public string Issuer { get; set; } = "PetelSystem";
        
        /// <summary>
        /// Token audience (e.g., "PetelWebApp")
        /// </summary>
        public string Audience { get; set; } = "PetelWebApp";
        
        /// <summary>
        /// Token expiration time in hours (default: 24 hours)
        /// </summary>
        public int ExpirationHours { get; set; } = 24;
    }
}