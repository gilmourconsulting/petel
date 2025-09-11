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
                    .Include(u => u.Entity) // Include entity details
                    .FirstOrDefaultAsync(u => u.Username == request.Username);
                    
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User {Username} not found", request.Username);
                    return Unauthorized(new { success = false, message = "שם משתמש או סיסמה שגויים" });
                }

                // Validate password
                bool passwordValid = await _authService.VerifyPasswordAsync(user, request.Password);

                if (!passwordValid)
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", request.Username);
                    return Unauthorized(new { success = false, message = "שם משתמש או סיסמה שגויים" });
                }
                
                // Validate entity ID matches
                if (user.EntityId != request.EntityId)
                {
                    _logger.LogWarning("Login failed: Entity mismatch for user {Username}. Expected: {ExpectedEntity}, Got: {ActualEntity}", 
                        request.Username, user.EntityId, request.EntityId);
                    return Unauthorized(new { success = false, message = "המשתמש אינו שייך לישות שנבחרה" });
                }
                
                // Get entity details including entity type
                var entity = await _context.Entities
                    .Include(e => e.EntityType)
                    .FirstOrDefaultAsync(e => e.Id == user.EntityId);
                    
                if (entity == null)
                {
                    _logger.LogWarning("Login failed: Entity {EntityId} not found", user.EntityId);
                    return Unauthorized(new { success = false, message = "ישות לא נמצאה" });
                }

                // Update user's last login time
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Create session following Entity-Based Request Flow
                var sessionId = Guid.NewGuid().ToString();
                var userSession = new UserSession
                {
                    SessionId = sessionId,
                    UserId = user.Id.ToString(),
                    Username = user.Username,
                    UserFullName = $"{user.FirstName} {user.LastName}".Trim(),
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Phone = user.Phone ?? string.Empty,
                    EntityId = user.EntityId.ToString(),
                    EntityName = entity.Name,
                    EntityTypeId = entity.EntityTypeId.ToString(),
                    EntityTypeName = entity.EntityType?.Name ?? "Unknown", // Fixed: TypeName -> Name
                    LastLogin = user.LastLogin ?? DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                // Store session using UserSessionService following Authentication & Session Management
                _userSessionService.CreateUserSession(userSession); // Use correct method to add session

                // Get user roles
                var userRoles = await _userRoleService.GetUserRolesAsync(user.Id);
                userSession.Roles = userRoles.ToList();

                _logger.LogInformation("User {Username} logged in successfully with session {SessionId}", 
                    request.Username, sessionId);

                // Return comprehensive login response
                return Ok(new
                {
                    success = true,
                    message = "התחברות בוצעה בהצלחה",
                    token = sessionId, // Session ID serves as token
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        fullName = userSession.UserFullName,
                        email = user.Email,
                        phone = user.Phone,
                        lastLogin = user.LastLogin
                    },
                    entity = new
                    {
                        id = entity.Id,
                        name = entity.Name,
                        entityTypeId = entity.EntityTypeId,
                        entityTypeName = entity.EntityType?.Name // Fixed: TypeName -> Name
                    },
                    session = new
                    {
                        sessionId = sessionId,
                        createdAt = userSession.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", request.Username);
                return StatusCode(500, new { success = false, message = "שגיאה פנימית במערכת" });
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