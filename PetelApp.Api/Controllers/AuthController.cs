// PetelApp.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Session;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TenantService _tenantService;
        private readonly ILogger<AuthController> _logger;
        private readonly UserSessionService _userSessionService;
        private readonly SystemAttributeService _systemAttributeService; // Add service for system attributes

        public AuthController(
            AppDbContext context, 
            TenantService tenantService,
            ILogger<AuthController> logger,
            UserSessionService userSessionService,
            SystemAttributeService systemAttributeService) // Inject the service
        {
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
            _userSessionService = userSessionService;
            _systemAttributeService = systemAttributeService; // Initialize the service
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(request.Username) || 
                    string.IsNullOrEmpty(request.Password) || 
                    request.TenantId <= 0)
                {
                    return BadRequest(new LoginResponse 
                    { 
                        Success = false, 
                        Message = "נתונים חסרים או לא תקינים" 
                    });
                }

                // Get tenant context from middleware
                var contextTenantId = _tenantService.GetCurrentTenantId();
                
                _logger.LogInformation("Login attempt - Request TenantId: {RequestTenantId}, Context TenantId: {ContextTenantId}", 
                    request.TenantId, contextTenantId);
                
                // Verify tenant context matches request
                if (string.IsNullOrEmpty(contextTenantId) || contextTenantId != request.TenantId.ToString())
                {
                    _logger.LogWarning("Tenant context mismatch. Context: '{ContextTenant}', Request: '{RequestTenant}'", 
                        contextTenantId ?? "null", request.TenantId);
                    
                    return BadRequest(new LoginResponse 
                    { 
                        Success = false, 
                        Message = "שגיאה בזיהוי הארגון" 
                    });
                }

                // Get user with entity information including entity type
                var user = await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType) // Include EntityType navigation
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

                if (user == null)
                {
                    return Unauthorized(new LoginResponse 
                    { 
                        Success = false, 
                        Message = "שם משתמש או סיסמה שגויים" 
                    });
                }

                // Check that the user belongs to the selected entity
                if (user.EntityId != request.TenantId)
                {
                    return Unauthorized(new LoginResponse 
                    { 
                        Success = false, 
                        Message = "המשתמש אינו שייך לארגון הנבחר" 
                    });
                }

                _logger.LogInformation("Found user {Username} with hash: {Hash}", user.Username, user.PasswordHash);

                // Verify password
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login attempt failed: Invalid password for user {Username} in tenant {TenantId}", 
                        request.Username, request.TenantId);
                    
                    return Unauthorized(new LoginResponse 
                    { 
                        Success = false, 
                        Message = "שם משתמש או סיסמה שגויים, או שאינך רשאי לגשת לארגון זה" 
                    });
                }

                // Get user roles
                var roleIdsAndNames = await _context.UserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .Join(_context.Roles,
                          ur => ur.RoleId,
                          r => r.Id,
                          (ur, r) => new { r.Id, r.Name })
                    .ToListAsync();

                var roleNames = roleIdsAndNames.Select(r => r.Name).ToList();
                var roleIds = roleIdsAndNames.Select(r => r.Id).ToList();

                // Get allowed actions for these roles
                var allowedActions = await _context.RolesActions
                    .Where(ra => roleIds.Contains(ra.RoleId) && ra.ActionLevel != 0)
                    .Select(ra => ra.ActionId)
                    .Distinct()
                    .ToListAsync();

                // Get entity information
                var entity = await _context.Entities
                    .Include(e => e.EntityType) // Ensure EntityType is loaded
                    .FirstOrDefaultAsync(e => e.Id == request.TenantId && e.IsActive);

                if (entity == null)
                {
                    return BadRequest(new { message = "גוף חינוכי לא נמצא או לא פעיל" });
                }

                // Create session with entity type data
                var userSession = new UserSession
                {
                    UserId = user.Id,
                    UserFullName = $"{user.FirstName} {user.LastName}",
                    UserEmail = user.Email,
                    TenantId = entity.Id,
                    TenantName = entity.Name,
                    EntityTypeId = entity.EntityTypeId, // This should not be null
                    EntityTypeName = entity.EntityType?.Name ?? "לא מוגדר", // Handle null EntityType
                    Roles = roleNames,
                    LoginTime = DateTime.UtcNow,
                    AllowedActions = allowedActions
                };

                // Store session
                _userSessionService.SetUserSession(userSession);

                // Update last login
                user.LastLogin = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate authentication token
                var token = GenerateAuthToken(user);

                _logger.LogInformation("Successful login for user {Username} in tenant {TenantId} with entity type {EntityTypeId}", 
                    request.Username, request.TenantId, user.Entity.EntityTypeId);

                // Return response with entity type
                Console.WriteLine($"Entity loaded: EntityTypeId={entity.EntityTypeId}, EntityTypeName={entity.EntityType?.Name}");

                var response = new
                {
                    success = true,
                    message = "התחברות בוצעה בהצלחה",
                    userFullName = $"{user.FirstName} {user.LastName}",
                    tenantId = entity.Id,
                    tenantName = entity.Name,
                    entityTypeId = entity.EntityTypeId, // Ensure this exists
                    entityTypeName = entity.EntityType?.Name ?? "לא מוגדר"
                };

                Console.WriteLine($"Returning entityTypeId: {response.entityTypeId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login attempt for user {Username} in tenant {TenantId}", 
                    request.Username, request.TenantId);
                
                return StatusCode(500, new LoginResponse 
                { 
                    Success = false, 
                    Message = "שגיאה פנימית במערכת" 
                });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout() // Remove async/await since no async operations
        {
            // Implement logout logic (invalidate token, update database, etc.)
            try
            {
                // You can add token blacklisting logic here
                
                _logger.LogInformation("User logged out successfully");
                
                return Ok(new { success = true, message = "התנתקות בוצעה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "שגיאה במהלך ההתנתקות" });
            }
        }

        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] TokenValidationRequest request) // Remove async/await
        {
            try
            {
                // Implement token validation logic
                var isValid = ValidateAuthToken(request.Token);
                
                if (!isValid)
                {
                    return Unauthorized(new { success = false, message = "Token לא תקין" });
                }

                return Ok(new { success = true, message = "Token תקין" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token validation");
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת Token" });
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            try
            {
                // Try BCrypt first (recommended)
             //   if (hash.StartsWith("$2") || hash.StartsWith("$2a") || hash.StartsWith("$2b") || hash.StartsWith("$2y"))
             //   {
             //       return BCrypt.Net.BCrypt.Verify(password, hash);
            //    }
                
                // Fall back to SHA256
                var hashedInput = HashPassword(password);
                return hashedInput == hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password");
                return false;
            }
        }

        private string HashPassword(string password)
        {
            // Simple SHA256 hashing (use BCrypt in production)
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private string GenerateAuthToken(dynamic user)
        {
            // Simple token generation (implement JWT in production)
            var tokenData = $"{user.Id}:{user.EntityId}:{DateTime.UtcNow.Ticks}";
            var tokenBytes = Encoding.UTF8.GetBytes(tokenData);
            return Convert.ToBase64String(tokenBytes);
        }

        private bool ValidateAuthToken(string token)
        {
            try
            {
                var tokenBytes = Convert.FromBase64String(token);
                var tokenData = Encoding.UTF8.GetString(tokenBytes);
                var parts = tokenData.Split(':');
                
                if (parts.Length != 3) return false;
                
                var timestamp = long.Parse(parts[2]);
                var tokenDate = new DateTime(timestamp);
                
                // Check if token is less than 24 hours old
                return DateTime.UtcNow.Subtract(tokenDate).TotalHours < 24;
            }
            catch
            {
                return false;
            }
        }
    }

    // Request/Response Models
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int TenantId { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int EntityTypeId { get; set; } // Add entity type ID
        public string EntityTypeName { get; set; } = string.Empty; // Add entity type name
        public DateTime ExpiresAt { get; set; }
    }

    public class TokenValidationRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}