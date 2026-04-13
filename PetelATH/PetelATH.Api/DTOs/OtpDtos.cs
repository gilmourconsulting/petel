// Models/DTOs/OtpDtos.cs
using System.ComponentModel.DataAnnotations;
using PetelATH.Api.Data;

namespace PetelATH.Api.DTOs
{
    /// <summary>
    /// Response for OTP setup containing QR code URL and secret
    /// </summary>
    public class OtpSetupResponseDto
    {
        public string QrCodeUrl { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to verify OTP setup with a code from authenticator app
    /// </summary>
    public class VerifyOtpSetupDto
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to validate OTP code during login
    /// </summary>
    public class ValidateOtpDto
    {
        [Required]
        public string TempToken { get; set; } = string.Empty;
        
        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to disable OTP for current user (requires password confirmation)
    /// </summary>
    public class DisableOtpDto
    {
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for OTP status check
    /// </summary>
    public class OtpStatusDto
    {
        public bool OtpEnabled { get; set; }
        public bool OtpVerified { get; set; }
        public bool SystemOtpEnabled { get; set; }
    }

        /// <summary>
        /// OTP validation response DTO
        /// </summary>
        public class OtpValidationResponseDto
        {
            public bool Success { get; set; }
            public string? Token { get; set; }
            public string Message { get; set; } = string.Empty;
        
            public bool RequiresPasswordChange { get; set; }
            public string? TempToken { get; set; }
            public string? PasswordExpirationMessage { get; set; }
        }
}