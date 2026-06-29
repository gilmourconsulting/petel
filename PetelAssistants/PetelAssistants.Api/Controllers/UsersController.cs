using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly ActionAuthorizationService _authService;

        public UsersController(
            AssistDbContext context,
            SharedDbContext sharedContext,
            ActionAuthorizationService authService,
            UserSessionService userSessionService,
            ILogger<UsersController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _authService = authService;
        }

        private int ResolveTargetEntityId(UserSession session)
        {
            var adminEntityStr = session.GetProperty("AdminSelectedEntityId");
            if (!string.IsNullOrEmpty(adminEntityStr) && int.TryParse(adminEntityStr, out int adminEntityId))
                return adminEntityId;
            int.TryParse(session.EntityId, out int sessionEntityId);
            return sessionEntityId;
        }

        private IQueryable<User> QueryUsers(int targetEntityId)
        {
            int.TryParse(GetCurrentSession()?.EntityId, out int sessionEntityId);
            if (targetEntityId != sessionEntityId)
                return _context.Users.IgnoreQueryFilters().Where(u => u.EntityId == targetEntityId);
            return _context.Users;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);
                _logger.LogInformation("Loading users for entity {EntityId}", targetEntityId);

                var users = await QueryUsers(targetEntityId)
                    .AsNoTracking()
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.Phone,
                        u.FirstName,
                        u.LastName,
                        FullName = (u.FirstName ?? "") + " " + (u.LastName ?? ""),
                        u.IsActive,
                        u.IsLocked,
                        u.LockedAt,
                        u.LastLogin,
                        u.CreatedAt,
                        u.UpdatedAt,
                        u.PasswordChangedAt,
                        u.PasswordChangeRequired,
                        u.EntityId,
                        u.FailedPasswordAttempts,
                        u.FailedOtpAttempts,
                        u.OtpVerified,
                        u.LockReasonId,
                        LockReasonCode = u.LockReason != null ? u.LockReason.Code : null,
                        LockReasonName = u.LockReason != null ? u.LockReason.Name : null,
                        LockReasonAllowForgotPassword = u.LockReason != null ? (bool?)u.LockReason.AllowForgotPassword : null
                    })
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת משתמשים", error = ex.Message });
            }
        }

        [HttpGet("lock-reasons")]
        public async Task<IActionResult> GetLockReasons()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var reasons = await _sharedContext.UserLockReasons
                    .AsNoTracking()
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new { r.Id, r.Code, r.Name, r.Description, r.AllowForgotPassword })
                    .ToListAsync();

                return Ok(new { success = true, data = reasons });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lock reasons");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סיבות נעילה", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                if (string.IsNullOrWhiteSpace(request.Username))
                    return BadRequest(new { success = false, message = "שם משתמש הוא שדה חובה" });

                if (string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new { success = false, message = "סיסמה היא שדה חובה" });

                var exists = await _context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.EntityId == targetEntityId && u.Username == request.Username);

                if (exists)
                    return BadRequest(new { success = false, message = "שם המשתמש כבר קיים ברשות זו" });

                var user = new User
                {
                    EntityId                = targetEntityId,
                    Username                = request.Username.Trim(),
                    PasswordHash            = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
                    FirstName               = request.FirstName?.Trim(),
                    LastName                = request.LastName?.Trim(),
                    Email                   = request.Email?.Trim(),
                    Phone                   = request.Phone?.Trim(),
                    IsActive                = request.IsActive,
                    PasswordChangeRequired  = request.PasswordChangeRequired,
                    PasswordChangedAt       = DateTime.UtcNow,
                    CreatedAt               = DateTime.UtcNow,
                    UpdatedAt               = DateTime.UtcNow,
                    CreatedUser             = currentUserId,
                    UpdateUser              = currentUserId
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created user {Username} for entity {EntityId}", user.Username, targetEntityId);
                return Ok(new { success = true, message = "משתמש נוצר בהצלחה", data = new { user.Id } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת משתמש", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.FirstName              = request.FirstName?.Trim();
                user.LastName               = request.LastName?.Trim();
                user.Email                  = request.Email?.Trim();
                user.Phone                  = request.Phone?.Trim();
                user.IsActive               = request.IsActive;
                user.PasswordChangeRequired = request.PasswordChangeRequired;
                user.UpdatedAt              = DateTime.UtcNow;
                user.UpdateUser             = currentUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated user {UserId}", id);
                return Ok(new { success = true, message = "פרטי משתמש עודכנו בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון משתמש", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                if (int.TryParse(session.UserId, out int sessionUserId) && sessionUserId == id)
                    return BadRequest(new { success = false, message = "לא ניתן להשבית את המשתמש הנוכחי" });

                user.IsActive   = false;
                user.UpdatedAt  = DateTime.UtcNow;
                user.UpdateUser = currentUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Deactivated user {UserId}", id);
                return Ok(new { success = true, message = "משתמש הושבת בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בהשבתת משתמש", error = ex.Message });
            }
        }

        [HttpPut("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                if (string.IsNullOrWhiteSpace(request.NewPassword))
                    return BadRequest(new { success = false, message = "הסיסמה החדשה לא יכולה להיות ריקה" });

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.PasswordHash           = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
                user.PasswordChangedAt      = DateTime.UtcNow;
                user.PasswordChangeRequired = false;
                user.UpdatedAt              = DateTime.UtcNow;
                user.UpdateUser             = currentUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Changed password for user {UserId}", id);
                return Ok(new { success = true, message = "סיסמה שונתה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בשינוי סיסמה", error = ex.Message });
            }
        }

        [HttpPost("{id}/lock")]
        public async Task<IActionResult> LockUser(int id, [FromBody] LockUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.IsLocked       = true;
                user.LockedAt       = DateTime.UtcNow;
                user.LockedBy       = currentUserId;
                user.LockReasonId   = request.LockReasonId;
                user.UpdatedAt      = DateTime.UtcNow;
                user.UpdateUser     = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "המשתמש ננעל בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בנעילת משתמש", error = ex.Message });
            }
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> UnlockUser(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.IsLocked               = false;
                user.LockedAt               = null;
                user.LockedBy               = null;
                user.LockReasonId           = null;
                user.FailedPasswordAttempts = 0;
                user.FailedOtpAttempts      = 0;
                user.UpdatedAt              = DateTime.UtcNow;
                user.UpdateUser             = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "המשתמש שוחרר מנעילה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בשחרור נעילת משתמש", error = ex.Message });
            }
        }

        [HttpPost("{id}/reset-failed-attempts")]
        public async Task<IActionResult> ResetFailedAttempts(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.FailedPasswordAttempts = 0;
                user.FailedOtpAttempts      = 0;
                user.UpdatedAt              = DateTime.UtcNow;
                user.UpdateUser             = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "ניסיונות כושלים אופסו בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting failed attempts for user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה באיפוס ניסיונות כושלים", error = ex.Message });
            }
        }

        [HttpPost("{id}/force-password-change")]
        public async Task<IActionResult> ForcePasswordChange(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.PasswordChangeRequired = true;
                user.UpdatedAt              = DateTime.UtcNow;
                user.UpdateUser             = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "אכיפת שינוי סיסמה הוגדרה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing password change for user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה באכיפת שינוי סיסמה", error = ex.Message });
            }
        }

        [HttpPost("{id}/reset-otp")]
        public async Task<IActionResult> ResetOtp(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                user.OtpSecret      = null;
                user.OtpEnabled     = false;
                user.OtpVerified    = false;
                user.FailedOtpAttempts = 0;
                user.UpdatedAt      = DateTime.UtcNow;
                user.UpdateUser     = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "OTP אופס בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting OTP for user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה באיפוס OTP", error = ex.Message });
            }
        }

        [HttpGet("{id}/roles")]
        public async Task<IActionResult> GetUserRoles(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var userExists = await _context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (!userExists)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                var roles = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(ur => ur.UserId == id && ur.IsActive)
                    .Select(ur => new
                    {
                        ur.Id,
                        ur.RoleId,
                        RoleName = ur.Role != null ? ur.Role.Name : "",
                        RoleDescription = ur.Role != null ? ur.Role.Description : null
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles for user {UserId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת תפקידי משתמש", error = ex.Message });
            }
        }

        [HttpPost("{id}/roles/{roleId}")]
        public async Task<IActionResult> AddRoleToUser(int id, int roleId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == id && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == roleId && r.EntityId == targetEntityId);

                if (role == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                var existing = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == roleId);

                if (existing != null)
                {
                    existing.IsActive   = true;
                    existing.UpdatedAt  = DateTime.UtcNow;
                    existing.UpdateUser = currentUserId;
                }
                else
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        EntityId    = targetEntityId,
                        UserId      = id,
                        RoleId      = roleId,
                        IsActive    = true,
                        CreatedAt   = DateTime.UtcNow,
                        UpdateUser  = currentUserId
                    });
                }

                await _context.SaveChangesAsync();
                _authService.InvalidateUserCache(id);
                return Ok(new { success = true, message = "תפקיד הוסף למשתמש בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role {RoleId} to user {UserId}", roleId, id);
                return StatusCode(500, new { success = false, message = "שגיאה בהוספת תפקיד למשתמש", error = ex.Message });
            }
        }

        [HttpDelete("{id}/roles/{roleId}")]
        public async Task<IActionResult> RemoveRoleFromUser(int id, int roleId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var userRole = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == roleId && ur.EntityId == targetEntityId);

                if (userRole == null)
                    return NotFound(new { success = false, message = "שיוך תפקיד לא נמצא" });

                userRole.IsActive   = false;
                userRole.UpdatedAt  = DateTime.UtcNow;
                userRole.UpdateUser = currentUserId;

                await _context.SaveChangesAsync();
                _authService.InvalidateUserCache(id);
                return Ok(new { success = true, message = "תפקיד הוסר מהמשתמש בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, id);
                return StatusCode(500, new { success = false, message = "שגיאה בהסרת תפקיד מהמשתמש", error = ex.Message });
            }
        }
    }

    public class CreateUserRequest
    {
        public string Username               { get; set; } = string.Empty;
        public string Password               { get; set; } = string.Empty;
        public string? FirstName             { get; set; }
        public string? LastName              { get; set; }
        public string? Email                 { get; set; }
        public string? Phone                 { get; set; }
        public bool IsActive                 { get; set; } = true;
        public bool PasswordChangeRequired   { get; set; } = true;
    }

    public class UpdateUserRequest
    {
        public string? FirstName             { get; set; }
        public string? LastName              { get; set; }
        public string? Email                 { get; set; }
        public string? Phone                 { get; set; }
        public bool IsActive                 { get; set; }
        public bool PasswordChangeRequired   { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class LockUserRequest
    {
        public int? LockReasonId { get; set; }
    }
}
