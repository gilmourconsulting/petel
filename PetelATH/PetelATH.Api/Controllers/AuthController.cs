// PetelATH.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using PetelATH.Api.DTOs;
using PetelATH.Api.Services;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserSessionService _sessionService;
        private readonly SystemAttributeCache _attributeCache;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            UserSessionService sessionService,
            SystemAttributeCache attributeCache,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _sessionService = sessionService;
            _attributeCache = attributeCache;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// User login - creates session and returns token
        /// Following Frontend Token-Only Storage pattern
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Username} in entity: {EntityId}", 
                    request.Username, request.EntityId);

                var result = await _authService.LoginAsync(request);

                if (!result.Success)
                {
                    // ✅ Return 200 OK with success: false and specific message
                    // Frontend will display result.Message to user
                    _logger.LogWarning("Login failed for user: {Username} - Reason: {Message}", 
                        request.Username, result.Message);
                    return Ok(result);
                }

                _logger.LogInformation("Login successful: {Username}, Token: {Token}", 
                    request.Username, result.Token);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error: {Username}", request.Username);
                return StatusCode(500, new LoginResponseDto
                { 
                    Success = false, 
                    Message = "אירעה שגיאה בעת ההתחברות" 
                });
            }
        }

        /// <summary>
        /// User logout - invalidates session
        /// Token from Authorization header (Frontend Token-Only Storage)
        /// </summary>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                // Get token from Authorization header
                var authHeader = Request.Headers["Authorization"].ToString();
                var sessionId = authHeader.Replace("Bearer ", "").Trim();
                
                if (!string.IsNullOrEmpty(sessionId))
                {
                    _sessionService.InvalidateSession(sessionId);
                    _logger.LogInformation("Session invalidated: {SessionId}", sessionId);
                }

                return Ok(new { success = true, message = "התנתקת בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, new { success = false, message = "שגיאה בהתנתקות" });
            }
        }

        /// <summary>
        /// Check authentication status
        /// Token from Authorization header
        /// </summary>
        [HttpGet("check")]
        public IActionResult CheckAuth()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                var token = authHeader.Replace("Bearer ", "").Trim();
                
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { isAuthenticated = false });
                }

                var session = _sessionService.GetUserSession(token);
                if (session == null)
                {
                    return Unauthorized(new { isAuthenticated = false });
                }

                return Ok(new
                {
                    isAuthenticated = true,
                    user = new
                    {
                        id = session.UserId,
                        username = session.Username,
                        fullName = session.UserFullName,
                        entityId = session.EntityId,
                        entityName = session.EntityName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth check error");
                return Unauthorized(new { isAuthenticated = false });
            }
        }

        /// <summary>
        /// Returns the password policy requirements as human-readable Hebrew strings.
        /// Public endpoint — called by the login page before any authentication.
        /// </summary>
        [HttpGet("password-policy")]
        public IActionResult GetPasswordPolicy()
        {
            const string defaultPolicy = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$";
            var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
            var policyRegex = !string.IsNullOrWhiteSpace(policyAttr?.Value) ? policyAttr.Value : defaultPolicy;
            return Ok(new { requirements = GetPasswordRequirements(policyRegex) });
        }

        /// <summary>
        [HttpPost("change-expired-password")]
        public async Task<IActionResult> ChangeExpiredPassword([FromBody] ChangeExpiredPasswordDto request)
        {
            try
            {
                // Decode temp token to get user ID
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(request.TempToken) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                var userIdClaim = jsonToken?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "טוקן לא תקין" });
                }

                var user = await _authService.ValidateUserAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "משתמש לא נמצא" });
                }

                // Verify old password
                if (!await _authService.VerifyPasswordAsync(user, request.OldPassword))
                {
                    return BadRequest(new { success = false, message = "סיסמה ישנה שגויה" });
                }

                // Validate new password against policy regex from system attributes
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                    return BadRequest(new { success = false, message = "סיסמה חדשה נדרשת" });

                const string defaultPolicy = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$";
                var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
                var policyRegex = !string.IsNullOrWhiteSpace(policyAttr?.Value) ? policyAttr.Value : defaultPolicy;

                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(request.NewPassword, policyRegex))
                    {
                        var requirements = GetPasswordRequirements(policyRegex);
                        var message = "הסיסמה אינה עומדת בדרישות המדיניות: " + string.Join(", ", requirements);
                        return BadRequest(new { success = false, message });
                    }
                }
                catch (System.Text.RegularExpressions.RegexParseException ex)
                {
                    _logger.LogError(ex, "Invalid password policy regex: {Regex}", policyRegex);
                    return StatusCode(500, new { success = false, message = "שגיאת מדיניות סיסמה" });
                }

                // Check if new password is same as old
                if (await _authService.VerifyPasswordAsync(user, request.NewPassword))
                {
                    return BadRequest(new { success = false, message = "הסיסמה החדשה חייבת להיות שונה מהישנה" });
                }

                // Hash and update password
                var newPasswordHash = await _authService.HashPasswordAsync(request.NewPassword);
                await _authService.UpdateUserPasswordAsync(user, newPasswordHash);

                _logger.LogInformation("User {UserId} changed expired password", userId);

                // Send password-change notification email (fire-and-forget; failure must not affect the response)
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var displayName = $"{user.FirstName} {user.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(displayName)) displayName = user.Username;
                            await _emailService.SendPasswordChangedAsync(user.Email, displayName);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "Failed to send password-change notification to user {UserId}", userId);
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "הסיסמה שונתה בהצלחה. אנא התחבר מחדש"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing expired password");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בשינוי הסיסמה"
                });
            }
        }

        private static List<string> GetPasswordRequirements(string pattern)
        {
            var reqs = new List<string>();

            var lenMatch = System.Text.RegularExpressions.Regex.Match(pattern, @"\{(\d+),(\d*)\}");
            if (lenMatch.Success)
            {
                var min = lenMatch.Groups[1].Value;
                var max = lenMatch.Groups[2].Value;
                reqs.Add(string.IsNullOrEmpty(max) ? $"\u05dc\u05e4\u05d7\u05d5\u05ea {min} \u05ea\u05d5\u05d5\u05d9\u05dd" : $"\u05d1\u05d9\u05df {min} \u05dc-{max} \u05ea\u05d5\u05d5\u05d9\u05dd");
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(pattern, @"\(\?=\.\*\[.*?a-z.*?\]\)"))
                reqs.Add("\u05dc\u05e4\u05d7\u05d5\u05ea \u05d0\u05d5\u05ea \u05e7\u05d8\u05e0\u05d4 \u05d0\u05d7\u05ea (a-z)");

            if (System.Text.RegularExpressions.Regex.IsMatch(pattern, @"\(\?=\.\*\[.*?A-Z.*?\]\)"))
                reqs.Add("\u05dc\u05e4\u05d7\u05d5\u05ea \u05d0\u05d5\u05ea \u05d2\u05d3\u05d5\u05dc\u05d4 \u05d0\u05d7\u05ea (A-Z)");

            if (System.Text.RegularExpressions.Regex.IsMatch(pattern, @"\(\?=\.\*\\d\)") ||
                System.Text.RegularExpressions.Regex.IsMatch(pattern, @"\(\?=\.\*\[.*?0-9.*?\]\)"))
                reqs.Add("\u05dc\u05e4\u05d7\u05d5\u05ea \u05e1\u05e4\u05e8\u05d4 \u05d0\u05d7\u05ea (0-9)");

            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(pattern, @"\(\?=\.\*\[([^\]]+)\]\)"))
            {
                var cls = m.Groups[1].Value;
                if (!cls.Contains("a-z") && !cls.Contains("A-Z") &&
                    !cls.Contains("0-9") && !cls.Contains(@"\d") && !cls.Contains(@"\w"))
                    reqs.Add($"\u05dc\u05e4\u05d7\u05d5\u05ea \u05ea\u05d5 \u05de\u05d9\u05d5\u05d7\u05d3 \u05d0\u05d7\u05d3 ({cls})");
            }

            return reqs;
        }
    }
}