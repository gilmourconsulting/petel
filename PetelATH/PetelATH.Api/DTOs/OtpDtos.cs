// Models/DTOs/OtpDtos.cs
using System.ComponentModel.DataAnnotations;

namespace PetelATH.Api.DTOs
{
    /// <summary>
    /// Request to send (or resend) email OTP — body contains TempToken only
    /// </summary>
    public class SendOtpDto
    {
        [Required]
        public string TempToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to validate email OTP code during login
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

    /// <summary>
    /// Response returned after sending OTP email — exposes masked email for UI display
    /// </summary>
    public class SendOtpResponseDto
    {
        public bool Success { get; set; }
        public string MaskedEmail { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to initiate the forgot-password flow (no authentication required).
    /// </summary>
    public class ForgotPasswordDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public int EntityId { get; set; }
    }

    /// <summary>
    /// Request to set a new password after OTP was verified in the forgot-password flow.
    /// TempToken must carry purpose=password_reset_verified (issued by otp/validate).
    /// </summary>
    public class ResetPasswordDto
    {
        [Required]
        public string TempToken { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;
    }
}