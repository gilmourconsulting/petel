using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelATH.Api.Configuration;
using PetelATH.Api.Data;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;
using PetelATH.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtpController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly SecuritySettings _securitySettings;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly SystemAttributeCache _systemAttributeCache;
        private readonly JwtTokenService _jwtTokenService;

        public OtpController(
            UserSessionService userSessionService,
            ILogger<OtpController> logger,
            AppDbContext context,
            IOptions<SecuritySettings> securitySettings,
            IAuthService authService,
            IEmailService emailService,
            SystemAttributeCache systemAttributeCache,
            JwtTokenService jwtTokenService)
            : base(userSessionService, logger)
        {
            _context = context;
            _securitySettings = securitySettings.Value;
            _authService = authService;
            _emailService = emailService;
            _systemAttributeCache = systemAttributeCache;
            _jwtTokenService = jwtTokenService;
        }

        private int GetMaxOtpAttempts()
        {
            var attribute = _systemAttributeCache.GetAttributeByName("Security_MaxOtpAttempts");
            if (attribute != null && int.TryParse(attribute.Value, out int maxAttempts))
                return maxAttempts;
            return _securitySettings.MaxOtpAttempts;
        }

        private int GetPasswordExpirationMonths()
        {
            var attribute = _systemAttributeCache.GetAttributeByName("Security_PasswordExpirationMonths");
            if (attribute != null && int.TryParse(attribute.Value, out int months))
                return months;
            return _securitySettings.PasswordExpirationMonths;
        }

        /// <summary>
        /// POST /api/otp/send - Generate and email a 6-digit OTP to the user.
        /// Accepts TempToken (from login response) in the request body.
        /// Also used for "resend" from the OTP verification modal.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            try
            {
                var user = await GetUserFromTempToken(dto.TempToken);
                if (user == null)
                    return Unauthorized(new { success = false, message = "טוקן לא תקין" });

                if (string.IsNullOrWhiteSpace(user.Email))
                    return BadRequest(new { success = false, message = "כתובת דוא\"ל לא מוגדרת לחשבון זה" });

                if (user.IsLocked)
                    return Unauthorized(new { success = false, message = "חשבון המשתמש נעול. אנא פנה למנהל המערכת" });

                // Generate 6-digit code
                var code = GenerateSixDigitCode();

                // BCrypt-hash before storing (same security level as passwords)
                user.EmailOtpCode = BCrypt.Net.BCrypt.HashPassword(code, 11);
                user.EmailOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                user.EmailOtpAttempts = 0;
                await _context.SaveChangesAsync();

                // Send email — fires-and-forgets exception handling in SmtpEmailService
                await _emailService.SendOtpAsync(user.Email, code, user.Username);

                _logger.LogInformation("Email OTP sent for user {UserId}", user.Id);

                return Ok(new SendOtpResponseDto
                {
                    Success = true,
                    MaskedEmail = SmtpEmailService.MaskEmail(user.Email),
                    Message = "קוד אימות נשלח לדוא\"ל שלך"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP email");
                return StatusCode(500, new { success = false, message = "שגיאה בשליחת קוד האימות" });
            }
        }

        /// <summary>
        /// POST /api/otp/validate - Verify the email OTP code and complete login.
        /// Returns full session token on success, or password-change requirement.
        /// </summary>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpDto dto)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(dto.TempToken) as JwtSecurityToken;
                var userIdClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
                var purposeClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "purpose")?.Value;
                bool isForgotPasswordFlow = purposeClaim == "password_reset";

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { success = false, message = "טוקן לא תקין" });

                var user = await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return Unauthorized(new { success = false, message = "משתמש לא נמצא" });

                if (user.IsLocked)
                {
                    _logger.LogWarning("OTP validation attempt for locked user {UserId}", user.Id);
                    return Unauthorized(new { success = false, message = "חשבון המשתמש נעול. אנא פנה למנהל המערכת" });
                }

                // Check that a code was sent and has not expired
                if (string.IsNullOrEmpty(user.EmailOtpCode) || user.EmailOtpExpiry == null)
                    return BadRequest(new { success = false, message = "יש לשלוח קוד אימות תחילה" });

                if (DateTime.UtcNow > user.EmailOtpExpiry)
                {
                    user.EmailOtpCode = null;
                    user.EmailOtpExpiry = null;
                    await _context.SaveChangesAsync();
                    return BadRequest(new { success = false, message = "קוד האימות פג תוקף. אנא בקש קוד חדש" });
                }

                // Verify code via BCrypt
                var isValid = BCrypt.Net.BCrypt.Verify(dto.Code, user.EmailOtpCode);

                if (!isValid)
                {
                    user.EmailOtpAttempts++;
                    user.LastFailedAttempt = DateTime.UtcNow;

                    int maxAttempts = GetMaxOtpAttempts();
                    if (user.EmailOtpAttempts >= maxAttempts)
                    {
                        user.IsLocked = true;
                        user.LockedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogWarning("User {UserId} locked after {Attempts} failed email OTP attempts", user.Id, user.EmailOtpAttempts);
                        return Unauthorized(new { success = false, message = "חשבון המשתמש נעול. אנא פנה למנהל המערכת" });
                    }

                    await _context.SaveChangesAsync();

                    int remaining = maxAttempts - user.EmailOtpAttempts;
                    return BadRequest(new { success = false, message = $"קוד אימות שגוי. נותרו {remaining} ניסיונות" });
                }

                // Success — clear OTP fields
                user.EmailOtpCode = null;
                user.EmailOtpExpiry = null;
                user.EmailOtpAttempts = 0;
                user.LastFailedAttempt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Email OTP validated successfully for user {UserId}", user.Id);

                // Forgot-password flow: OTP verified — issue a short-lived reset-verified token
                // so the reset-password endpoint can accept the request without a new OTP round.
                if (isForgotPasswordFlow)
                {
                    var resetToken = _jwtTokenService.GeneratePasswordResetVerifiedToken(user.Id);
                    return Ok(new OtpValidationResponseDto
                    {
                        Success = false,
                        RequiresPasswordChange = true,
                        TempToken = resetToken,
                        PasswordExpirationMessage = "אמת את זהותך, כעת הזן סיסמה חדשה",
                        Message = "נדרש שינוי סיסמה"
                    });
                }

                // Check password expiration after successful OTP
                var (isExpired, expirationMessage) = CheckPasswordExpiration(user);
                if (isExpired)
                {
                    return Ok(new OtpValidationResponseDto
                    {
                        Success = false,
                        RequiresPasswordChange = true,
                        TempToken = dto.TempToken,
                        PasswordExpirationMessage = expirationMessage,
                        Message = "נדרש שינוי סיסמה"
                    });
                }

                var sessionId = await _authService.CompleteLoginAsync(user, user.Entity);

                return Ok(new OtpValidationResponseDto
                {
                    Success = true,
                    Token = sessionId,
                    Message = "התחברות הצליחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTP");
                return StatusCode(500, new { success = false, message = "שגיאה באימות הקוד" });
            }
        }

        /// <summary>
        /// POST /api/otp/disable - Turn off OTP for current user (requires password)
        /// </summary>
        [HttpPost("disable")]
        public async Task<IActionResult> DisableOtp([FromBody] DisableOtpDto dto)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var user = await _context.Users.FindAsync(int.Parse(session.UserId));
                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                    return BadRequest(new { success = false, message = "סיסמה שגויה" });

                user.OtpEnabled = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Email OTP disabled for user {UserId}", user.Id);

                return Ok(new { success = true, message = "אימות דוא\"ל בוטל" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling OTP for user {UserId}", session.UserId);
                return StatusCode(500, new { success = false, message = "שגיאה בביטול אימות דו-שלבי" });
            }
        }

        /// <summary>
        /// GET /api/otp/status - Check if OTP is enabled for current user
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetOtpStatus()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var user = await _context.Users.FindAsync(int.Parse(session.UserId));
                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                return Ok(new OtpStatusDto
                {
                    OtpEnabled = user.OtpEnabled,
                    SystemOtpEnabled = _securitySettings.OtpEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OTP status for user {UserId}", session.UserId);
                return StatusCode(500, new { success = false, message = "שגיאה בקבלת מצב אימות דו-שלבי" });
            }
        }

        // ─── Private helpers ────────────────────────────────────────────────────

        private static string GenerateSixDigitCode()
        {
            // Cryptographically random 6-digit code (000000–999999)
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            uint value = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return value.ToString("D6");
        }

        private (bool IsExpired, string? Message) CheckPasswordExpiration(User user)
        {
            int expirationMonths = GetPasswordExpirationMonths();
            if (expirationMonths <= 0) return (false, null);

            if (user.PasswordChangeRequired)
                return (true, "מנהל המערכת דורש החלפת סיסמה");

            if (user.IsPasswordExpired(expirationMonths))
            {
                var days = (DateTime.UtcNow - user.PasswordChangedAt).Days;
                return (true, $"הסיסמה פגה תוקף ");
            }

            return (false, null);
        }

        private async Task<User?> GetUserFromTempToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var userIdClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return null;

                return await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType)
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }
            catch
            {
                return null;
            }
        }
    }
}