// PetelApp.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Session;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly UserSessionService _userSessionService;
        private readonly IAuthService _authService;
        private readonly UserRoleService _userRoleService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AppDbContext context,
            UserSessionService userSessionService, 
            IAuthService authService,
            UserRoleService userRoleService,
            ILogger<AuthController> logger)
        {
            _context = context;
            _userSessionService = userSessionService;
            _authService = authService;
            _userRoleService = userRoleService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { success = false, message = "Username and password are required" });
                }

                // Find user by username first
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username);
                        
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User {Username} not found", request.Username);
                    return Unauthorized(new { success = false, message = "שם משתמש או סיסמה שגויים" });
                }

                // Validate password (use proper password hashing in production)
                bool passwordValid = await _authService.VerifyPasswordAsync(user, request.Password);
                if (!passwordValid)
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", request.Username);
                    return Unauthorized(new { success = false, message = "שם משתמש או סיסמה שגויים" });
                }
                
                // Check if user belongs to the selected entity
                if (user.EntityId != request.EntityId)
                {
                    _logger.LogWarning("Login failed: User {Username} does not belong to entity {EntityId}", 
                        request.Username, request.EntityId);
                    return Unauthorized(new { success = false, message = "המשתמש אינו משויך לארגון זה" });
                }

                // Get entity information
                var entity = await _context.Entities
                    .Include(e => e.EntityType)
                    .FirstOrDefaultAsync(e => e.Id == user.EntityId);
                    
                if (entity == null)
                {
                    _logger.LogWarning("Login failed: Entity {EntityId} not found", user.EntityId);
                    return NotFound(new { success = false, message = "הארגון לא נמצא במערכת" });
                }

                // Generate session
                var sessionId = Guid.NewGuid().ToString();
                var session = new UserSession
                {
                    SessionId = sessionId,
                    UserId = user.Id.ToString(),
                    UserFullName = $"{user.FirstName} {user.LastName}".Trim(),
                    EntityId = user.EntityId.ToString(), // Using EntityId instead of TenantId
                    EntityName = entity.Name,
                    EntityTypeId = entity.EntityTypeId.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    Roles = await _userRoleService.GetUserRolesAsync(user.Id)
                };

                // Store session
                _userSessionService.SetUserSession(session);

                _logger.LogInformation("User {Username} logged in successfully for entity {EntityId}", 
                    request.Username, request.EntityId);

                // Return successful login response
                return Ok(new
                {
                    success = true,
                    token = sessionId,
                    userId = user.Id.ToString(),
                    userFullName = $"{user.FirstName} {user.LastName}".Trim(),
                    entityId = user.EntityId.ToString(),
                    entityName = entity.Name,
                    entityTypeId = entity.EntityTypeId.ToString(),
                    entityTypeName = entity.EntityType?.Name ?? string.Empty,
                    roles = session.Roles ?? new List<string>(),
                    message = "התחברות בוצעה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user {Username}", request.Username);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהתחברות: " + ex.Message
                });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                var sessionId = GetSessionId();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    _userSessionService.InvalidateSession(sessionId);
                    _logger.LogInformation("User logged out, session {SessionId} invalidated", sessionId);
                }

                return Ok(new { success = true, message = "התנתקות בוצעה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "שגיאת שרת פנימית" });
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }
}

// PetelApp.Api/Services/IAuthService.cs
namespace PetelApp.Api.Services
{
    /// <summary>
    /// Authentication service interface following the Entity-Based Request Flow pattern
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Verifies a user's password
        /// </summary>
        /// <param name="user">The user entity</param>
        /// <param name="password">The password to verify</param>
        /// <returns>True if password is valid, false otherwise</returns>
        Task<bool> VerifyPasswordAsync(User user, string password);
        
        /// <summary>
        /// Creates a hash of the provided password
        /// </summary>
        /// <param name="password">Password to hash</param>
        /// <returns>Hashed password</returns>
        Task<string> HashPasswordAsync(string password);
    }
}