namespace Petel.Core.Security
{
    public class SecuritySettings
    {
        public JwtSettings Jwt { get; set; } = new();
        public DataEncryptionSettings DataEncryption { get; set; } = new();
        public bool OtpEnabled { get; set; } = false;
        public string OtpIssuer { get; set; } = string.Empty;
        public int SessionTimeoutMinutes { get; set; } = 30;
        public int MaxPasswordAttempts { get; set; } = 5;
        public int MaxOtpAttempts { get; set; } = 3;
        public int PasswordExpirationMonths { get; set; } = 3;

        public class JwtSettings
        {
            public string SecretKey { get; set; } = string.Empty;
            public string Issuer { get; set; } = "PetelApp";
            public string Audience { get; set; } = "PetelAppUsers";
            public int ExpirationHours { get; set; } = 8;
        }

        public class DataEncryptionSettings
        {
            /// <summary>
            /// Base64-encoded 32-byte (256-bit) AES encryption key
            /// MUST be loaded from Azure Key Vault in production/test
            /// </summary>
            public string EncryptionKey { get; set; } = string.Empty;
        }
    }
}