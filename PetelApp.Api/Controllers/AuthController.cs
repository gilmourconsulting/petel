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

                // ✅ CORRECT: Call LoginAsync from IAuthService
                var result = await _authService.LoginAsync(request);

                if (!result.Success)
                {
                    _logger.LogWarning("Login failed for user: {Username}", request.Username);
                    return Unauthorized(result);
                }

                _logger.LogInformation("Login successful: {Username}, Token: {Token}", 
                    request.Username, result.Token);

                // Return response following Frontend Token-Only Storage pattern
                // Frontend will store ONLY result.Token in sessionStorage
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
    }
}