// PetelApp.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.DTOs;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserSessionService _sessionService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            UserSessionService sessionService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _sessionService = sessionService;
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
        /// Change expired password - requires TempToken from login
        /// </summary>
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

                // Validate new password
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new { success = false, message = "סיסמה חדשה נדרשת" });
                }

                if (request.NewPassword.Length < 6)
                {
                    return BadRequest(new { success = false, message = "סיסמה חייבת להכיל לפחות 6 תווים" });
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
    }
}