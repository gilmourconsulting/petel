// PetelApp.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Session;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;

        public AuthController(
            UserSessionService userSessionService,
            ILogger<AuthController> logger,
            AppDbContext context,
            IAuthService authService)
            : base(userSessionService, logger)
        {
            _context = context;
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            try 
            {
                if (string.IsNullOrEmpty(loginRequest.Username) || string.IsNullOrEmpty(loginRequest.Password))
                {
                    return BadRequest(new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש וסיסמה נדרשים"
                    });
                }

                // Find user by username following Entity-Based Request Flow - using Data.User
                var user = await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e!.EntityType)
                    .FirstOrDefaultAsync(u => u.Username == loginRequest.Username);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: User {Username} not found", loginRequest.Username);
                    return Unauthorized(new LoginResponseDto { Success = false, Message = "שם משתמש או סיסמה שגויים" });
                }

                // Validate password using AuthService
                bool passwordValid = await _authService.VerifyPasswordAsync(user, loginRequest.Password);
                if (!passwordValid)
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", loginRequest.Username);
                    return Unauthorized(new LoginResponseDto { Success = false, Message = "שם משתמש או סיסמה שגויים" });
                }

                // Validate entity ID matches (Entity-Based Request Flow)
                if (user.EntityId != loginRequest.EntityId)
                {
                    _logger.LogWarning("Login failed: Entity mismatch for user {Username}", loginRequest.Username);
                    return Unauthorized(new LoginResponseDto { Success = false, Message = "המשתמש אינו שייך לישות שנבחרה" });
                }

                // Get entity details - now no ambiguity with single Data layer
                var userEntity = user.Entity;
                if (userEntity == null)
                {
                    _logger.LogWarning("Login failed: Entity {EntityId} not found", user.EntityId);
                    return Unauthorized(new LoginResponseDto { Success = false, Message = "ישות לא נמצאה" });
                }

                // Update user's last login time
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Invalidate existing sessions following Authentication & Session Management
                var existingSessions = _userSessionService.GetAllActiveSessions()
                    .Where(s => s.UserId == user.Id.ToString() && s.EntityId == user.EntityId.ToString())
                    .ToList();

                foreach (var existingSession in existingSessions)
                {
                    _userSessionService.InvalidateSession(existingSession.SessionId);
                    _logger.LogInformation("Invalidated existing session {SessionId} for user {UserId}", 
                        existingSession.SessionId, user.Id);
                }

                // Create UserFullName following Authentication & Session Management
                var userFullName = $"{user.FirstName} {user.LastName}".Trim();

                // Create session using existing UserSessionService following Entity-Based Request Flow
                var sessionId = _userSessionService.CreateSessionWithFullData(
                    userId: user.Id.ToString(),
                    username: user.Username,
                    userFullName: userFullName,
                    entityId: user.EntityId.ToString(),
                    entityName: userEntity.Name,
                    entityTypeId: userEntity.EntityTypeId.ToString(),
                    entityTypeName: userEntity.EntityType?.Name ?? "",
                    lastLogin: user.LastLogin
                );

                // Get created session for response
                var session = _userSessionService.GetUserSession(sessionId);

                _logger.LogInformation("Login successful for user {UserId} in entity {EntityId}, session {SessionId}", 
                    user.Id, user.EntityId, sessionId);

                // Return success response with DTOs following Authentication & Session Management
                return Ok(new LoginResponseDto
                {
                    Success = true,
                    Message = "התחברות בוצעה בהצלחה",
                    Token = sessionId // Frontend Session Token Only pattern

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", loginRequest.Username);
                return StatusCode(500, new LoginResponseDto { Success = false, Message = "שגיאה בהתחברות" });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                var session = GetCurrentSession(); // From BaseController
                if (session != null)
                {
                    _userSessionService.InvalidateSession(session.SessionId);
                    _logger.LogInformation("User logged out, session {SessionId} invalidated", session.SessionId);
                }

                return Ok(new { success = true, message = "התנתקות בוצעה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "שגיאת שרת פנימית" });
            }
        }
    }
}