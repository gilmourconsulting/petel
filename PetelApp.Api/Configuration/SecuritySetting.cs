namespace PetelApp.Api.Configuration
{
    public class SecuritySettings
    {
        public bool OtpEnabled { get; set; }
        public string OtpIssuer { get; set; } = "Petel System";

        /// <summary>
        /// Session idle timeout in minutes. Default: 10 minutes
        /// User will be automatically logged out after this period of inactivity
        /// </summary>
        public int SessionTimeoutMinutes { get; set; } = 10;

                /// <summary>
        /// Maximum failed password attempts before user is locked. Default: 5
        /// </summary>
        public int MaxPasswordAttempts { get; set; } = 5;

                /// <summary>
        /// Maximum failed OTP attempts before user is locked. Default: 3
        /// </summary>
        public int MaxOtpAttempts { get; set; } = 3;

                /// <summary>
        /// Password expiration period in months. Default: 3 months. Set to 0 to disable expiration.
        /// </summary>
        public int PasswordExpirationMonths { get; set; } = 3;
        
        // ✅  JWT Settings for main session tokens
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