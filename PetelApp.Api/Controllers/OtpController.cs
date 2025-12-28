using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;
using PetelApp.Api.Configuration;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;
using PetelApp.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtpController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly SecuritySettings _securitySettings;
        //private readonly UserSessionService _sessionService;
        private readonly IAuthService _authService;

        public OtpController(
            UserSessionService userSessionService,
            ILogger<OtpController> logger,
            AppDbContext context,
            IOptions<SecuritySettings> securitySettings,
            IAuthService authService)
            : base(userSessionService, logger)
        {
            _context = context;
            _securitySettings = securitySettings.Value;
            //  _sessionService = userSessionService;
            _authService = authService;
        }

        /// <summary>
        /// GET /api/otp/setup - Generate QR code for user to scan
        /// Requires TempToken from initial login
        /// </summary>
        [HttpGet("setup")]
        public async Task<IActionResult> SetupOtp()
        {
            // Get user from temp token
            var user = await GetUserFromTempToken();
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            try
            {
                // Generate new TOTP secret (Base32 encoded, 20 bytes = 160 bits)
                var secretBytes = new byte[20];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(secretBytes);
                }
                var secret = Base32Encoding.ToString(secretBytes);

                // Save to database
                user.OtpSecret = secret;
                user.OtpEnabled = true;
                user.OtpVerified = false;
                await _context.SaveChangesAsync();

                // Generate QR code URL
                var issuer = _securitySettings.OtpIssuer ?? "Petel System";
                var username = user.Username;
                var qrCodeUrl = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(username)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

                _logger.LogInformation("OTP setup initiated for user {UserId}", user.Id);

                return Ok(new OtpSetupResponseDto
                {
                    QrCodeUrl = qrCodeUrl,
                    Secret = secret,
                    Issuer = issuer,
                    Username = username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up OTP for user {UserId}", user.Id);
                return StatusCode(500, new { success = false, message = "שגיאה בהגדרת אימות דו-שלבי" });
            }
        }

        /// <summary>
        /// POST /api/otp/verify-setup - Confirm user scanned QR correctly
        /// </summary>
        [HttpPost("verify-setup")]
        public async Task<IActionResult> VerifySetup([FromBody] VerifyOtpSetupDto dto)
        {
            var user = await GetUserFromTempToken();
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            if (string.IsNullOrEmpty(user.OtpSecret))
            {
                return BadRequest(new { success = false, message = "OTP לא הוגדר" });
            }

            try
            {
                // Validate the code
                var secretBytes = Base32Encoding.ToBytes(user.OtpSecret);
                var totp = new Totp(secretBytes);
                var isValid = totp.VerifyTotp(dto.Code, out _, new VerificationWindow(2, 2));

                if (isValid)
                {
                    user.OtpVerified = true;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("OTP setup verified for user {UserId}", user.Id);

                    return Ok(new { success = true, message = "אימות דו-שלבי הופעל בהצלחה" });
                }
                else
                {
                    _logger.LogWarning("Invalid OTP code during setup for user {UserId}", user.Id);
                    return BadRequest(new { success = false, message = "קוד שגוי" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP setup for user {UserId}", user.Id);
                return StatusCode(500, new { success = false, message = "שגיאה באימות הקוד" });
            }
        }

        /// <summary>
        /// POST /api/otp/validate - Validate OTP code at login (uses TempToken)
        /// Returns full session token on success
        /// </summary>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpDto dto)
        {
            try
            {
                // Decode temp token to get user ID
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(dto.TempToken) as JwtSecurityToken;
                var userIdClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "טוקן לא תקין" });
                }

                var user = await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || string.IsNullOrEmpty(user.OtpSecret))
                {
                    return Unauthorized(new { success = false, message = "משתמש לא נמצא" });
                }

                // Validate the OTP code
                var secretBytes = Base32Encoding.ToBytes(user.OtpSecret);
                var totp = new Totp(secretBytes);

                var currentUtcTime = DateTime.UtcNow;
                _logger.LogInformation("Validating OTP for user {UserId}. Code: {Code}, UTC Time: {Time}",
                    user.Id, dto.Code, currentUtcTime);

                var isValid = totp.VerifyTotp(dto.Code, out long timeStepMatched, new VerificationWindow(2, 2));

                if (!isValid)
                {
                    _logger.LogWarning("Invalid OTP code for user {UserId}. Code: {Code}, Expected at time: {Time}",
                        user.Id, dto.Code, currentUtcTime);
                    return BadRequest(new { success = false, message = "קוד אימות שגוי" });
                }

                // ✅ Single logging statement after validation
                _logger.LogInformation("OTP validated successfully for user {UserId}. TimeStep: {TimeStep}",
                    user.Id, timeStepMatched);

                // OTP is valid - complete login using AuthService helper
                var sessionId = await _authService.CompleteLoginAsync(user, user.Entity);

                _logger.LogInformation("Login completed for user {UserId}, SessionId: {SessionId}", user.Id, sessionId);

                // ✅ Single return statement
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
        /// POST /api/otp/disable - Turn off OTP for current user
        /// </summary>
        [HttpPost("disable")]
        public async Task<IActionResult> DisableOtp([FromBody] DisableOtpDto dto)
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            try
            {
                var user = await _context.Users.FindAsync(int.Parse(session.UserId));
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                {
                    return BadRequest(new { success = false, message = "סיסמה שגויה" });
                }

                // Disable OTP
                user.OtpEnabled = false;
                user.OtpVerified = false;
                user.OtpSecret = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("OTP disabled for user {UserId}", user.Id);

                return Ok(new { success = true, message = "אימות דו-שלבי בוטל" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling OTP for user {UserId}", session.UserId);
                return StatusCode(500, new { success = false, message = "שגיאה בביטול אימות דו-שלבי" });
            }
        }

        /// <summary>
        /// GET /api/otp/status - Check if user has OTP enabled
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetOtpStatus()
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            try
            {
                var user = await _context.Users.FindAsync(int.Parse(session.UserId));
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                return Ok(new OtpStatusDto
                {
                    OtpEnabled = user.OtpEnabled,
                    OtpVerified = user.OtpVerified,
                    SystemOtpEnabled = _securitySettings.OtpEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OTP status for user {UserId}", session.UserId);
                return StatusCode(500, new { success = false, message = "שגיאה בקבלת מצב אימות דו-שלבי" });
            }
        }

        /// <summary>
        /// Helper method to get user from temp token in Authorization header
        /// </summary>
        private async Task<User?> GetUserFromTempToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader?.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var userIdClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return null;
                }

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