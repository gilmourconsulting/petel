using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseController
    {
        private readonly AppDbContext _context;

        public UsersController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<UsersController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
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
                        u.LastLogin,
                        u.CreatedAt,
                        u.UpdatedAt,
                        EntityId = u.EntityId,  // ✅ Show entity ID
                        EntityName = u.Entity != null ? u.Entity.Name : "לא משויך"  // ✅ Show entity name
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

                var newUser = new User
                {
                    EntityId = sessionEntityId,
                    Username = request.Username,
                    Email = request.Email ?? string.Empty,
                    Phone = request.Phone,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PasswordHash = passwordHash,
                    IsActive = true,
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
                    .Where(u => u.Id == id && u.EntityId == sessionEntityId)
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
    }

    /// <summary>
    /// Request model for creating a user
    /// </summary>
    public class CreateUserRequest
    {
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
}