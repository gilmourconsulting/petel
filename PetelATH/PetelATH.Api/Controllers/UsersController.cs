using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Services;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly SystemAttributeService _systemAttributeService;
        private readonly IEmailService _emailService;

        public UsersController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<UsersController> logger,
            SystemAttributeService systemAttributeService,
            IEmailService emailService)
            : base(userSessionService, logger)
        {
            _context = context;
            _systemAttributeService = systemAttributeService;
            _emailService = emailService;
        }

        /// <summary>
        /// Get all users for the current entity
        /// Returns user information without password hashes
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for users request");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    _logger.LogError("Invalid EntityId in session: '{EntityId}'", session.EntityId);
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                _logger.LogInformation("Loading users for entity {EntityId}", sessionEntityId);

                var users = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Entity)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.Phone,
                        u.FirstName,
                        u.LastName,
                        FullName = u.FirstName + " " + u.LastName,
                        u.IsActive,
                        u.IsLocked,  
                        u.LockedAt, 
                        u.LastLogin,
                        u.CreatedAt,
                        u.UpdatedAt,
                        u.PasswordChangedAt,
                        u.PasswordChangeRequired,
                        EntityId = u.EntityId,  // ✅ Show entity ID
                        EntityName = u.Entity != null ? u.Entity.Name : "לא משויך",  // ✅ Show entity name
                        u.FailedPasswordAttempts,
                        u.FailedOtpAttempts,
                        u.OtpVerified
                    })
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} users for entity {EntityId}", users.Count, sessionEntityId);

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת המשתמשים",
                    error = ex.Message
                });
            }
        }

                /// <summary>
        /// Lock a user account
        /// </summary>
        [HttpPost("{id}/lock")]
        public async Task<IActionResult> LockUser(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }
        
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }
        
                if (user.IsLocked)
                {
                    return BadRequest(new { success = false, message = "המשתמש כבר נעול" });
                }
        
                // Lock the user
                user.IsLocked = true;
                user.LockedAt = DateTime.UtcNow;
                user.LockedBy = int.TryParse(session.UserId, out int adminId) ? adminId : null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = user.LockedBy;
        
                await _context.SaveChangesAsync();
        
                _logger.LogInformation("User {UserId} locked by admin {AdminId}", id, session.UserId);
        
                return Ok(new
                {
                    success = true,
                    message = "המשתמש ננעל בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בנעילת המשתמש",
                    error = ex.Message
                });
            }
        }
        
        /// <summary>
        /// Unlock a user account and reset failed attempt counters
        /// </summary>
        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> UnlockUser(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }
        
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }
        
                if (!user.IsLocked)
                {
                    return BadRequest(new { success = false, message = "המשתמש אינו נעול" });
                }
        
                // Unlock the user and reset counters
                user.IsLocked = false;
                user.LockedAt = null;
                user.LockedBy = null;
                user.FailedPasswordAttempts = 0;
                user.FailedOtpAttempts = 0;
                user.LastFailedAttempt = null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int adminId) ? adminId : null;
        
                await _context.SaveChangesAsync();
        
                _logger.LogInformation("User {UserId} unlocked by admin {AdminId}", id, session.UserId);
        
                return Ok(new
                {
                    success = true,
                    message = "המשתמש שוחרר בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בשחרור המשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Reset OTP verification for a user
        /// Sets otp_secret to null and otp_verified to false
        /// </summary>
        [HttpPost("{id}/reset-otp")]
        public async Task<IActionResult> ResetOtpVerification(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Reset OTP verification
                user.OtpSecret = null;
                user.OtpVerified = false;
                user.FailedOtpAttempts = 0;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int adminId) ? adminId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("OTP verification reset for user {UserId} by admin {AdminId}", id, session.UserId);

                return Ok(new
                {
                    success = true,
                    message = "אימות דו-שלבי אופס בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting OTP verification for user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה באיפוס אימות דו-שלבי",
                    error = ex.Message
                });
            }
        }


        /// <summary>
        /// Reset failed login and OTP attempt counters for a user
        /// </summary>
        [HttpPost("{id}/reset-failed-attempts")]
        public async Task<IActionResult> ResetFailedAttempts(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                user.FailedPasswordAttempts = 0;
                user.FailedOtpAttempts = 0;
                user.LastFailedAttempt = null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int adminId) ? adminId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Failed attempts reset for user {UserId} by admin {AdminId}", id, session.UserId);

                return Ok(new
                {
                    success = true,
                    message = "נסיונות כושלים אופסו בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting failed attempts for user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה באיפוס נסיונות כושלים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Force user to change password on next login
        /// </summary>
        [HttpPost("{id}/force-password-change")]
        public async Task<IActionResult> ForcePasswordChange(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Set flag to require password change
                user.PasswordChangeRequired = true;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int adminId) ? adminId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} marked for forced password change by admin {AdminId}", 
                    id, session.UserId);

                return Ok(new
                {
                    success = true,
                    message = "המשתמש יידרש להחליף סיסמה בהתחברות הבאה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing password change for user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בסימון שינוי סיסמה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == id && u.EntityId == sessionEntityId)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.Phone,
                        u.FirstName,
                        u.LastName,
                        u.IsActive,
                        u.LastLogin,
                        u.CreatedAt,
                        u.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                return Ok(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת פרטי המשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                        // ✅ Use entityId from request instead of session
                    var entityId = request.EntityId;

                    // Validate entity exists
                    var entityExists = await _context.Entities
                        .AnyAsync(e => e.Id == entityId);

                    if (!entityExists) {
                        return BadRequest(new { success = false, message = "ישות לא קיימת" });
                    }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Username))
                    return BadRequest(new { success = false, message = "שם משתמש חובה" });

                if (string.IsNullOrWhiteSpace(request.FirstName))
                    return BadRequest(new { success = false, message = "שם פרטי חובה" });

                if (string.IsNullOrWhiteSpace(request.LastName))
                    return BadRequest(new { success = false, message = "שם משפחה חובה" });

                // Check username uniqueness
                var existingUser = await _context.Users
                    .Where(u => u.Username == request.Username)
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return BadRequest(new { success = false, message = "שם משתמש זה כבר קיים" });
                }

                // Check email uniqueness if provided
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    var existingEmail = await _context.Users
                        .Where(u => u.Email == request.Email)
                        .FirstOrDefaultAsync();

                    if (existingEmail != null)
                    {
                        return BadRequest(new { success = false, message = "דוא\"ל זה כבר קיים" });
                    }
                }

                // Hash the password (in production, use proper password hashing)
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "");

                // ✅ Get OTP setting from system attributes database
                var otpEnabled = _systemAttributeService.GetAttributeValueAsBool("Security_OtpEnabled");

                var newUser = new User
                {
                    EntityId = entityId,
                    Username = request.Username,
                    Email = request.Email ?? string.Empty,
                    Phone = request.Phone,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PasswordHash = passwordHash,
                    IsActive = true,
                    OtpEnabled = otpEnabled,  // ✅ Auto-enable OTP if system flag is on
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new user {UserId} with username {Username}", newUser.Id, newUser.Username);

                return Ok(new
                {
                    success = true,
                    message = "משתמש נוצר בהצלחה",
                    data = new
                    {
                        newUser.Id,
                        newUser.Username,
                        newUser.Email,
                        newUser.Phone,
                        newUser.FirstName,
                        newUser.LastName,
                        newUser.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת משתמש חדש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update user information
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Update allowed fields
                if (!string.IsNullOrWhiteSpace(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrWhiteSpace(request.LastName))
                    user.LastName = request.LastName;

                if (!string.IsNullOrWhiteSpace(request.Email))
                    user.Email = request.Email;

                if (!string.IsNullOrWhiteSpace(request.Phone))
                    user.Phone = request.Phone;

                if (request.IsActive.HasValue)
                    user.IsActive = request.IsActive.Value;

                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int updateUserId) ? updateUserId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated user {UserId}", id);

                return Ok(new
                {
                    success = true,
                    message = "פרטי המשתמש עודכנו בהצלחה",
                    data = new
                    {
                        user.Id,
                        user.Username,
                        user.Email,
                        user.Phone,
                        user.FirstName,
                        user.LastName,
                        user.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון פרטי המשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Change user password (admin can change any user's password)
        /// </summary>
        [HttpPut("{id}/change-password")]
        public async Task<IActionResult> ChangeUserPassword(int id, [FromBody] ChangePasswordRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate password
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new { success = false, message = "סיסמה חדשה נדרשת" });
                }

                if (request.NewPassword.Length < 6)
                {
                    return BadRequest(new { success = false, message = "סיסמה חייבת להכיל לפחות 6 תווים" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Hash new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.PasswordChangedAt = DateTime.UtcNow; // ✅ NEW: Update timestamp
                user.PasswordChangeRequired = false; // ✅ NEW: Clear forced change flag
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int updateUserId) ? updateUserId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password changed for user {UserId} by admin {AdminId}", id, session.UserId);

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
                            _logger.LogWarning(emailEx, "Failed to send password-change notification to user {UserId}", id);
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "הסיסמה שונתה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בשינוי הסיסמה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a user (soft delete - set IsActive to false)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id && u.EntityId == sessionEntityId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Soft delete - just deactivate
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdateUser = int.TryParse(session.UserId, out int updateUserId) ? updateUserId : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted user {UserId}", id);

                return Ok(new { success = true, message = "משתמש הוסר בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהסרת המשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get roles for a specific user
        /// </summary>
        [HttpGet("{id}/roles")]
        public async Task<IActionResult> GetUserRoles(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id)// && u.EntityId == sessionEntityId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                var roles = await _context.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.UserId == id && ur.IsActive)
                    .Include(ur => ur.Role)
                    .Select(ur => new
                    {
                        ur.Role!.Id,
                        ur.Role.Name,
                        ur.CreatedAt
                    })
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles for user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תפקידי המשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add role to user
        /// </summary>
        [HttpPost("{id}/roles")]
        public async Task<IActionResult> AddRoleToUser(int id, [FromBody] AddRoleToUserRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id )//&& u.EntityId == sessionEntityId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Check if role exists
                var roleExists = await _context.Roles.AnyAsync(r => r.Id == request.RoleId);
                if (!roleExists)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                // Check if user already has this role
                var existingUserRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == request.RoleId);

                if (existingUserRole != null)
                {
                    if (existingUserRole.IsActive)
                    {
                        return BadRequest(new { success = false, message = "משתמש כבר משויך לתפקיד זה" });
                    }
                    else
                    {
                        // Re-activate existing role
                        existingUserRole.IsActive = true;
                        existingUserRole.UpdatedAt = DateTime.UtcNow;
                        existingUserRole.UpdateUserId = int.Parse(session.UserId);
                    }
                }
                else
                {
                    // Create new user-role assignment
                    var userRole = new UserRole
                    {
                        UserId = id,
                        RoleId = request.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUserId = int.Parse(session.UserId)
                    };

                    _context.UserRoles.Add(userRole);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Added role {RoleId} to user {UserId}", request.RoleId, id);

                return Ok(new { success = true, message = "התפקיד נוסף למשתמש בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role to user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת התפקיד למשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Remove role from user
        /// </summary>
        [HttpDelete("{id}/roles/{roleId}")]
        public async Task<IActionResult> RemoveRoleFromUser(int id, int roleId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }

                var user = await _context.Users
                    .Where(u => u.Id == id && u.EntityId == sessionEntityId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == roleId && ur.IsActive);

                if (userRole == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא משויך לתפקיד זה" });
                }

                // Soft delete - set IsActive to false
                userRole.IsActive = false;
                userRole.UpdatedAt = DateTime.UtcNow;
                userRole.UpdateUserId = int.Parse(session.UserId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Removed role {RoleId} from user {UserId}", roleId, id);

                return Ok(new { success = true, message = "התפקיד הוסר מהמשתמש בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role from user {UserId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהסרת התפקיד מהמשתמש",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available roles (for adding to user)
        /// </summary>
        [HttpGet("available-roles")]
        public async Task<IActionResult> GetAvailableRoles([FromQuery] int? userId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.Roles.AsNoTracking();

                // If userId provided, exclude roles already assigned to that user
                if (userId.HasValue)
                {
                    var assignedRoleIds = await _context.UserRoles
                        .Where(ur => ur.UserId == userId.Value && ur.IsActive)
                        .Select(ur => ur.RoleId)
                        .ToListAsync();

                    query = query.Where(r => !assignedRoleIds.Contains(r.Id));
                }

                var roles = await query
                    .Select(r => new
                    {
                        r.Id,
                        r.Name
                    })
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available roles");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת התפקידים",
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request model for creating a user
    /// </summary>
    public class CreateUserRequest
    {
        public int EntityId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    /// <summary>
    /// Request model for updating a user
    /// </summary>
    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Request model for adding role to user
    /// </summary>
    public class AddRoleToUserRequest
    {
        public int RoleId { get; set; }
    }

        /// <summary>
    /// Request model for changing user password
    /// </summary>
    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}