// PetelApp.Api/Controllers/RolesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ActionAuthorizationService _actionAuthService;


        public RolesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ActionAuthorizationService actionAuthService,
            ILogger<RolesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _actionAuthService = actionAuthService;
        }

        /// <summary>
        /// Get all roles
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Loading all roles");

                var roles = await _context.Roles
                    .AsNoTracking()
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.CreatedAt,
                        r.UpdatedAt,
                        UserCount = r.UserRoles.Count(ur => ur.IsActive),
                        ActionCount = r.RolesActions.Count()
                    })
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} roles", roles.Count);

                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת התפקידים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get role details with users and actions
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleDetails(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var role = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.CreatedAt,
                        r.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                // Get users with this role
                var users = await _context.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.RoleId == id && ur.IsActive)
                    .Include(ur => ur.User)
                    .Select(ur => new
                    {
                        ur.User!.Id,
                        ur.User.Username,
                        ur.User.FirstName,
                        ur.User.LastName,
                        FullName = ur.User.FirstName + " " + ur.User.LastName,
                        ur.User.IsActive
                    })
                    .ToListAsync();

                // Get actions for this role
                var actions = await _context.RolesActions
                    .AsNoTracking()
                    .Where(ra => ra.RoleId == id)
                    .Include(ra => ra.SystemAction)
                    .ThenInclude(a => a.ActionType)
                    .Select(ra => new
                    {
                        ra.Id,
                        ra.ActionId,
                        ActionName = ra.SystemAction.Name,
                        DisplayName = ra.SystemAction.DisplayName ?? ra.SystemAction.Name,
                        Reference = ra.SystemAction.Reference,
                        ActionTypeName = ra.SystemAction.ActionType.Name,
                        ra.ActionLevel
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.DisplayName)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        role,
                        users,
                        actions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading role details for role {RoleId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת פרטי התפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create new role
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { success = false, message = "שם התפקיד חובה" });
                }

                // Check if role name already exists
                var exists = await _context.Roles.AnyAsync(r => r.Name == request.Name);
                if (exists)
                {
                    return BadRequest(new { success = false, message = "תפקיד בשם זה כבר קיים" });
                }

                var role = new Role
                {
                    Name = request.Name,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new role: {RoleName} (ID: {RoleId})", role.Name, role.Id);

                return Ok(new
                {
                    success = true,
                    message = "התפקיד נוצר בהצלחה",
                    data = new { role.Id, role.Name }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת התפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update role name
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { success = false, message = "שם התפקיד חובה" });
                }

                // Check if new name already exists (excluding current role)
                var exists = await _context.Roles.AnyAsync(r => r.Name == request.Name && r.Id != id);
                if (exists)
                {
                    return BadRequest(new { success = false, message = "תפקיד בשם זה כבר קיים" });
                }

                role.Name = request.Name;
                role.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated role {RoleId}: {RoleName}", id, role.Name);

                return Ok(new
                {
                    success = true,
                    message = "התפקיד עודכן בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role {RoleId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון התפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete role (only if no active users assigned)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var role = await _context.Roles
                    .Include(r => r.UserRoles)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (role == null)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                // Check if role has active users
                var hasActiveUsers = role.UserRoles.Any(ur => ur.IsActive);
                if (hasActiveUsers)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "לא ניתן למחוק תפקיד עם משתמשים פעילים"
                    });
                }

                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();

                await _actionAuthService.RefreshCacheAsync();

                _logger.LogInformation("Deleted role {RoleId}: {RoleName}", id, role.Name);

                return Ok(new
                {
                    success = true,
                    message = "התפקיד נמחק בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {RoleId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת התפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add action to role
        /// </summary>
        [HttpPost("{roleId}/actions")]
        public async Task<IActionResult> AddActionToRole(int roleId, [FromBody] AddActionRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Check if role exists
                var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
                if (!roleExists)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                // Check if action exists
                var actionExists = await _context.Set<SystemAction>().AnyAsync(a => a.Id == request.ActionId);
                if (!actionExists)
                {
                    return NotFound(new { success = false, message = "פעולה לא נמצאה" });
                }

                // Check if already assigned
                var exists = await _context.RolesActions
                    .AnyAsync(ra => ra.RoleId == roleId && ra.ActionId == request.ActionId);

                if (exists)
                {
                    return BadRequest(new { success = false, message = "הפעולה כבר משויכת לתפקיד זה" });
                }

                var roleAction = new RolesAction
                {
                    RoleId = roleId,
                    ActionId = request.ActionId,
                    ActionLevel = request.ActionLevel ?? 1,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = int.Parse(session.UserId)
                };

                _context.RolesActions.Add(roleAction);
                await _context.SaveChangesAsync();

                await _actionAuthService.RefreshCacheAsync();

                _logger.LogInformation("Added action {ActionId} to role {RoleId}", request.ActionId, roleId);

                return Ok(new
                {
                    success = true,
                    message = "הפעולה נוספה לתפקיד בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding action to role {RoleId}", roleId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת הפעולה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Remove action from role
        /// </summary>
        [HttpDelete("{roleId}/actions/{actionId}")]
        public async Task<IActionResult> RemoveActionFromRole(int roleId, int actionId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var roleAction = await _context.RolesActions
                    .FirstOrDefaultAsync(ra => ra.RoleId == roleId && ra.ActionId == actionId);

                if (roleAction == null)
                {
                    return NotFound(new { success = false, message = "הפעולה לא משויכת לתפקיד זה" });
                }

                _context.RolesActions.Remove(roleAction);
                await _context.SaveChangesAsync();

                await _actionAuthService.RefreshCacheAsync();

                _logger.LogInformation("Removed action {ActionId} from role {RoleId}", actionId, roleId);

                return Ok(new
                {
                    success = true,
                    message = "הפעולה הוסרה מהתפקיד בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing action from role {RoleId}", roleId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהסרת הפעולה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Refresh authorization cache (reload all roles, actions, and role-action mappings)
        /// </summary>
        [HttpPost("refresh-cache")]
        public async Task<IActionResult> RefreshCache()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("🔄 Refreshing authorization cache (all roles, actions, and role-action mappings)...");

                // Refresh the entire authorization cache
                await _actionAuthService.RefreshCacheAsync();

                _logger.LogInformation("✅ Authorization cache refreshed successfully");

                return Ok(new
                {
                    success = true,
                    message = "המטמון רוענן בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error refreshing cache");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ברענון המטמון",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available actions (for adding to role)
        /// </summary>
        [HttpGet("available-actions")]
        public async Task<IActionResult> GetAvailableActions([FromQuery] int? roleId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.Set<SystemAction>()
                    .AsNoTracking()
                    .Include(a => a.ActionType)
                    .Where(a => a.IsActive);

                // If roleId provided, exclude actions already assigned to that role
                if (roleId.HasValue)
                {
                    var assignedActionIds = await _context.RolesActions
                        .Where(ra => ra.RoleId == roleId.Value)
                        .Select(ra => ra.ActionId)
                        .ToListAsync();

                    query = query.Where(a => !assignedActionIds.Contains(a.Id));
                }

                var actions = await query
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        DisplayName = a.DisplayName ?? a.Name,
                        a.Reference,
                        a.Description,
                        ActionTypeName = a.ActionType.Name
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.DisplayName)
                    .ToListAsync();

                return Ok(new { success = true, data = actions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available actions");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת הפעולות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add user to role
        /// </summary>
        [HttpPost("{roleId}/users")]
        public async Task<IActionResult> AddUserToRole(int roleId, [FromBody] AddUserToRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Check if role exists
                var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
                if (!roleExists)
                {
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });
                }

                // Check if user exists
                var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
                if (!userExists)
                {
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });
                }

                // Check if user already has this role
                var existingUserRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == roleId);

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
                        UserId = request.UserId,
                        RoleId = roleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUserId = int.Parse(session.UserId)
                    };

                    _context.UserRoles.Add(userRole);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Added user {UserId} to role {RoleId}", request.UserId, roleId);

                return Ok(new { success = true, message = "המשתמש נוסף לתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to role {RoleId}", roleId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת המשתמש לתפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Remove user from role
        /// </summary>
        [HttpDelete("{roleId}/users/{userId}")]
        public async Task<IActionResult> RemoveUserFromRole(int roleId, int userId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.IsActive);

                if (userRole == null)
                {
                    return NotFound(new { success = false, message = "משתמש לא משויך לתפקיד זה" });
                }

                // Soft delete - set IsActive to false
                userRole.IsActive = false;
                userRole.UpdatedAt = DateTime.UtcNow;
                userRole.UpdateUserId = int.Parse(session.UserId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Removed user {UserId} from role {RoleId}", userId, roleId);

                return Ok(new { success = true, message = "המשתמש הוסר מהתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from role {RoleId}", roleId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהסרת המשתמש מהתפקיד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available users (for adding to role)
        /// </summary>
        [HttpGet("available-users")]
        public async Task<IActionResult> GetAvailableUsers([FromQuery] int? roleId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive);

                // If roleId provided, exclude users already assigned to that role
                if (roleId.HasValue)
                {
                    var assignedUserIds = await _context.UserRoles
                        .Where(ur => ur.RoleId == roleId.Value && ur.IsActive)
                        .Select(ur => ur.UserId)
                        .ToListAsync();

                    query = query.Where(u => !assignedUserIds.Contains(u.Id));
                }

                var users = await query
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.FirstName,
                        u.LastName,
                        FullName = u.FirstName + " " + u.LastName
                    })
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .ToListAsync();

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available users");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת המשתמשים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Export all actions to JSON file (optionally from a specific date)
        /// </summary>
        [HttpGet("actions/export")]
        public async Task<IActionResult> ExportActions([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Exporting actions" + (fromDate.HasValue ? $" from date {fromDate.Value:yyyy-MM-dd}" : ""));

                var query = _context.Set<SystemAction>()
                    .AsNoTracking()
                    .Include(a => a.ActionType)
                    .AsQueryable();

                // Filter by date if provided (convert to UTC for PostgreSQL)
                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    query = query.Where(a => a.CreatedAt >= fromDateUtc || a.UpdatedAt >= fromDateUtc);
                }

                var actions = await query
                    .Select(a => new
                    {
                        a.Name,
                        DisplayName = a.DisplayName ?? a.Name,
                        a.Description,
                        a.OnclickName,
                        ActionTypeName = a.ActionType.Name,
                        a.Reference,
                        a.SortOrder,
                        a.IsActive,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

                _logger.LogInformation("Exported {Count} actions", actions.Count);

                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    FromDate = fromDate,
                    TotalActions = actions.Count,
                    Actions = actions
                };

                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(exportData, jsonOptions);
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
                var stream = new MemoryStream(bytes);

                var fileName = $"Actions_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                return File(stream, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting actions");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בייצוא הפעולות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Import actions from JSON file
        /// Matches by name - if action already exists, it will be skipped
        /// </summary>
        [HttpPost("actions/import")]
        public async Task<IActionResult> ImportActions(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)  // 10MB limit
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            var errors = new List<string>();
            var importedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();

                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportActionsData>(jsonContent);
                
                if (importData?.Actions == null || !importData.Actions.Any())
                {
                    return BadRequest(new { success = false, message = "No actions found in file" });
                }

                _logger.LogInformation("Processing {Count} actions from import file", importData.Actions.Count);

                // Get all existing action names for comparison
                var existingActionNames = await _context.Set<SystemAction>()
                    .Select(a => a.Name.ToLower())
                    .ToListAsync();

                // Get all action types for mapping
                var actionTypes = await _context.Set<ActionType>()
                    .ToDictionaryAsync(at => at.Name.ToLower(), at => at.Id);

                var userId = int.Parse(session.UserId);

                foreach (var actionDto in importData.Actions)
                {
                    try
                    {
                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(actionDto.Name))
                        {
                            errors.Add($"Action with missing name - skipped");
                            errorCount++;
                            continue;
                        }

                        // Check if action already exists (case-insensitive)
                        if (existingActionNames.Contains(actionDto.Name.ToLower()))
                        {
                            _logger.LogInformation("Action '{Name}' already exists - skipped", actionDto.Name);
                            skippedCount++;
                            continue;
                        }

                        // Resolve action type
                        if (string.IsNullOrWhiteSpace(actionDto.ActionTypeName))
                        {
                            errors.Add($"Action '{actionDto.Name}': Missing action type - skipped");
                            errorCount++;
                            continue;
                        }

                        var actionTypeLower = actionDto.ActionTypeName.ToLower();
                        if (!actionTypes.ContainsKey(actionTypeLower))
                        {
                            errors.Add($"Action '{actionDto.Name}': Action type '{actionDto.ActionTypeName}' not found - skipped");
                            errorCount++;
                            continue;
                        }

                        // Create new action
                        var action = new SystemAction
                        {
                            Name = actionDto.Name,
                            DisplayName = actionDto.DisplayName,
                            Description = actionDto.Description,
                            OnclickName = actionDto.OnclickName,
                            ActionTypeId = actionTypes[actionTypeLower],
                            Reference = actionDto.Reference,
                            SortOrder = actionDto.SortOrder,
                            IsActive = actionDto.IsActive,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Set<SystemAction>().Add(action);
                        importedCount++;
                        
                        _logger.LogInformation("Imported action: {Name}", actionDto.Name);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Action '{actionDto.Name}': {ex.Message}");
                        errorCount++;
                        _logger.LogError(ex, "Error importing action '{Name}'", actionDto.Name);
                    }
                }

                if (importedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    
                    // Reload actions cache
                    await _actionAuthService.RefreshCacheAsync();
                    
                    _logger.LogInformation("Actions cache reloaded after import");
                }

                var message = $"ייבוא הסתיים: {importedCount} פעולות יובאו, {skippedCount} כבר קיימות, {errorCount} שגיאות";

                return Ok(new
                {
                    success = true,
                    message = message,
                    ImportedCount = importedCount,
                    SkippedCount = skippedCount,
                    ErrorCount = errorCount,
                    Errors = errors.Take(50).ToList(),
                    HasMoreErrors = errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing actions file");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"שגיאה בעיבוד הקובץ: {ex.Message}",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Export role-actions mappings to JSON file
        /// Shows which actions are assigned to which roles
        /// </summary>
        [HttpGet("role-actions/export")]
        public async Task<IActionResult> ExportRoleActions([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Exporting role-actions mappings" + (fromDate.HasValue ? $" from date {fromDate.Value:yyyy-MM-dd}" : ""));

                var query = _context.RolesActions
                    .AsNoTracking()
                    .Include(ra => ra.Role)
                    .Include(ra => ra.SystemAction)
                    .AsQueryable();

                // Filter by date if provided (convert to UTC for PostgreSQL)
                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    query = query.Where(ra => ra.UpdatedAt >= fromDateUtc);
                }

                var roleActions = await query
                    .Select(ra => new
                    {
                        RoleName = ra.Role.Name,
                        ActionName = ra.SystemAction.Name,
                        ActionLevel = ra.ActionLevel,
                        ra.UpdatedAt
                    })
                    .OrderBy(ra => ra.RoleName)
                    .ThenBy(ra => ra.ActionName)
                    .ToListAsync();

                _logger.LogInformation("Exported {Count} role-action mappings", roleActions.Count);

                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    FromDate = fromDate,
                    TotalMappings = roleActions.Count,
                    RoleActions = roleActions
                };

                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(exportData, jsonOptions);
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
                var stream = new MemoryStream(bytes);

                var fileName = $"RoleActions_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                return File(stream, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting role-actions");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בייצוא קישורי תפקידים-פעולות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Import role-actions mappings from JSON file
        /// Matches roles and actions by name
        /// Creates mappings only if both role and action exist
        /// </summary>
        [HttpPost("role-actions/import")]
        public async Task<IActionResult> ImportRoleActions(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)  // 10MB limit
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            var errors = new List<string>();
            var importedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();

                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportRoleActionsData>(jsonContent);
                
                if (importData?.RoleActions == null || !importData.RoleActions.Any())
                {
                    return BadRequest(new { success = false, message = "No role-action mappings found in file" });
                }

                _logger.LogInformation("Processing {Count} role-action mappings from import file", importData.RoleActions.Count);

                // Get all roles and actions for name matching
                var roles = await _context.Roles
                    .ToDictionaryAsync(r => r.Name.ToLower(), r => r.Id);

                var actions = await _context.Set<SystemAction>()
                    .ToDictionaryAsync(a => a.Name.ToLower(), a => a.Id);

                // Get existing mappings to check for duplicates
                var existingMappings = await _context.RolesActions
                    .Select(ra => new { ra.RoleId, ra.ActionId })
                    .ToListAsync();
                var existingSet = new HashSet<(int, int)>(existingMappings.Select(m => (m.RoleId, m.ActionId)));

                var userId = int.Parse(session.UserId);

                foreach (var mappingDto in importData.RoleActions)
                {
                    try
                    {
                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(mappingDto.RoleName))
                        {
                            errors.Add($"Mapping with missing role name - skipped");
                            errorCount++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(mappingDto.ActionName))
                        {
                            errors.Add($"Mapping with missing action name - skipped");
                            errorCount++;
                            continue;
                        }

                        // Resolve role by name
                        var roleLower = mappingDto.RoleName.ToLower();
                        if (!roles.ContainsKey(roleLower))
                        {
                            errors.Add($"Role '{mappingDto.RoleName}' not found - skipped");
                            errorCount++;
                            continue;
                        }

                        // Resolve action by name
                        var actionLower = mappingDto.ActionName.ToLower();
                        if (!actions.ContainsKey(actionLower))
                        {
                            errors.Add($"Action '{mappingDto.ActionName}' not found - skipped");
                            errorCount++;
                            continue;
                        }

                        var roleId = roles[roleLower];
                        var actionId = actions[actionLower];

                        // Check if mapping already exists
                        if (existingSet.Contains((roleId, actionId)))
                        {
                            _logger.LogInformation("Mapping '{RoleName}' -> '{ActionName}' already exists - skipped", 
                                mappingDto.RoleName, mappingDto.ActionName);
                            skippedCount++;
                            continue;
                        }

                        // Create new mapping
                        var roleAction = new RolesAction
                        {
                            RoleId = roleId,
                            ActionId = actionId,
                            ActionLevel = mappingDto.ActionLevel ?? 0,
                            UpdateUser = userId,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.RolesActions.Add(roleAction);
                        importedCount++;
                        
                        _logger.LogInformation("Imported mapping: '{RoleName}' -> '{ActionName}'", 
                            mappingDto.RoleName, mappingDto.ActionName);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Mapping '{mappingDto.RoleName}' -> '{mappingDto.ActionName}': {ex.Message}");
                        errorCount++;
                        _logger.LogError(ex, "Error importing mapping '{RoleName}' -> '{ActionName}'", 
                            mappingDto.RoleName, mappingDto.ActionName);
                    }
                }

                if (importedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    
                    // Reload actions cache
                    await _actionAuthService.RefreshCacheAsync();
                    
                    _logger.LogInformation("Role-actions cache reloaded after import");
                }

                var message = $"ייבוא הסתיים: {importedCount} קישורים יובאו, {skippedCount} כבר קיימים, {errorCount} שגיאות";

                return Ok(new
                {
                    success = true,
                    message = message,
                    ImportedCount = importedCount,
                    SkippedCount = skippedCount,
                    ErrorCount = errorCount,
                    Errors = errors.Take(50).ToList(),
                    HasMoreErrors = errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing role-actions file");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"שגיאה בעיבוד הקובץ: {ex.Message}",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Export complete package: both actions and role-actions mappings
        /// Creates a combined JSON file with all data needed for environment migration
        /// </summary>
        [HttpGet("complete-export")]
        public async Task<IActionResult> ExportComplete([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Exporting complete package" + (fromDate.HasValue ? $" from date {fromDate.Value:yyyy-MM-dd}" : ""));

                // Export actions
                var actionsQuery = _context.Set<SystemAction>()
                    .AsNoTracking()
                    .Include(a => a.ActionType)
                    .AsQueryable();

                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    actionsQuery = actionsQuery.Where(a => a.CreatedAt >= fromDateUtc || a.UpdatedAt >= fromDateUtc);
                }

                var actions = await actionsQuery
                    .Select(a => new
                    {
                        a.Name,
                        DisplayName = a.DisplayName ?? a.Name,
                        a.Description,
                        a.OnclickName,
                        ActionTypeName = a.ActionType.Name,
                        a.Reference,
                        a.SortOrder,
                        a.IsActive,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

                // Export role-actions mappings
                var roleActionsQuery = _context.RolesActions
                    .AsNoTracking()
                    .Include(ra => ra.Role)
                    .Include(ra => ra.SystemAction)
                    .AsQueryable();

                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    roleActionsQuery = roleActionsQuery.Where(ra => ra.UpdatedAt >= fromDateUtc);
                }

                var roleActions = await roleActionsQuery
                    .Select(ra => new
                    {
                        RoleName = ra.Role.Name,
                        ActionName = ra.SystemAction.Name,
                        ActionLevel = ra.ActionLevel,
                        ra.UpdatedAt
                    })
                    .OrderBy(ra => ra.RoleName)
                    .ThenBy(ra => ra.ActionName)
                    .ToListAsync();

                _logger.LogInformation("Exported {ActionsCount} actions and {MappingsCount} role-action mappings", 
                    actions.Count, roleActions.Count);

                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    FromDate = fromDate,
                    TotalActions = actions.Count,
                    TotalRoleActions = roleActions.Count,
                    Actions = actions,
                    RoleActions = roleActions
                };

                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(exportData, jsonOptions);
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
                var stream = new MemoryStream(bytes);

                var fileName = $"Complete_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                return File(stream, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating complete export");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בייצוא החבילה המלאה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Import complete package: both actions and role-actions mappings
        /// Imports in correct order (actions first, then role-actions)
        /// </summary>
        [HttpPost("complete-import")]
        public async Task<IActionResult> ImportComplete(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)  // 10MB limit
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            var results = new
            {
                ActionsImported = 0,
                ActionsSkipped = 0,
                ActionsErrors = 0,
                RoleActionsImported = 0,
                RoleActionsSkipped = 0,
                RoleActionsErrors = 0,
                Errors = new List<string>()
            };

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();

                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportCompleteData>(jsonContent);
                
                if (importData == null)
                {
                    return BadRequest(new { success = false, message = "Invalid file format" });
                }

                _logger.LogInformation("Processing complete import: {ActionsCount} actions, {MappingsCount} role-actions", 
                    importData.Actions?.Count ?? 0, importData.RoleActions?.Count ?? 0);

                var userId = int.Parse(session.UserId);
                var errors = new List<string>();

                // Step 1: Import Actions
                if (importData.Actions != null && importData.Actions.Any())
                {
                    var (imported, skipped, errorCount, actionErrors) = await ImportActionsInternal(importData.Actions, userId);
                    results = new
                    {
                        ActionsImported = imported,
                        ActionsSkipped = skipped,
                        ActionsErrors = errorCount,
                        RoleActionsImported = 0,
                        RoleActionsSkipped = 0,
                        RoleActionsErrors = 0,
                        Errors = actionErrors
                    };
                    errors.AddRange(actionErrors);
                }

                // Step 2: Import Role-Actions Mappings
                if (importData.RoleActions != null && importData.RoleActions.Any())
                {
                    var (imported, skipped, errorCount, mappingErrors) = await ImportRoleActionsInternal(importData.RoleActions, userId);
                    results = new
                    {
                        results.ActionsImported,
                        results.ActionsSkipped,
                        results.ActionsErrors,
                        RoleActionsImported = imported,
                        RoleActionsSkipped = skipped,
                        RoleActionsErrors = errorCount,
                        Errors = errors.Concat(mappingErrors).ToList()
                    };
                }

                // Reload cache if any changes were made
                if (results.ActionsImported > 0 || results.RoleActionsImported > 0)
                {
                    await _actionAuthService.RefreshCacheAsync();
                    _logger.LogInformation("Cache reloaded after complete import");
                }

                var message = $"ייבוא מלא הסתיים:\nפעולות: {results.ActionsImported} יובאו, {results.ActionsSkipped} כבר קיימות\nקישורים: {results.RoleActionsImported} יובאו, {results.RoleActionsSkipped} כבר קיימים\nשגיאות: {results.ActionsErrors + results.RoleActionsErrors}";

                return Ok(new
                {
                    success = true,
                    message = message,
                    results.ActionsImported,
                    results.ActionsSkipped,
                    results.ActionsErrors,
                    results.RoleActionsImported,
                    results.RoleActionsSkipped,
                    results.RoleActionsErrors,
                    Errors = results.Errors.Take(50).ToList(),
                    HasMoreErrors = results.Errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing complete package");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"שגיאה בייבוא החבילה המלאה: {ex.Message}",
                    error = ex.Message
                });
            }
        }

        // Helper methods for complete import
        private async Task<(int imported, int skipped, int errors, List<string> errorMessages)> ImportActionsInternal(
            List<ImportActionDto> actions, int userId)
        {
            var imported = 0;
            var skipped = 0;
            var errorCount = 0;
            var errors = new List<string>();

            var existingActionNames = await _context.Set<SystemAction>()
                .Select(a => a.Name.ToLower())
                .ToListAsync();

            var actionTypes = await _context.Set<ActionType>()
                .ToDictionaryAsync(at => at.Name.ToLower(), at => at.Id);

            foreach (var actionDto in actions)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(actionDto.Name))
                    {
                        errors.Add($"Action with missing name - skipped");
                        errorCount++;
                        continue;
                    }

                    if (existingActionNames.Contains(actionDto.Name.ToLower()))
                    {
                        skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(actionDto.ActionTypeName))
                    {
                        errors.Add($"Action '{actionDto.Name}': Missing action type - skipped");
                        errorCount++;
                        continue;
                    }

                    var actionTypeLower = actionDto.ActionTypeName.ToLower();
                    if (!actionTypes.ContainsKey(actionTypeLower))
                    {
                        errors.Add($"Action '{actionDto.Name}': Action type '{actionDto.ActionTypeName}' not found - skipped");
                        errorCount++;
                        continue;
                    }

                    var action = new SystemAction
                    {
                        Name = actionDto.Name,
                        DisplayName = actionDto.DisplayName,
                        Description = actionDto.Description,
                        OnclickName = actionDto.OnclickName,
                        ActionTypeId = actionTypes[actionTypeLower],
                        Reference = actionDto.Reference,
                        SortOrder = actionDto.SortOrder,
                        IsActive = actionDto.IsActive,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Set<SystemAction>().Add(action);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Action '{actionDto.Name}': {ex.Message}");
                    errorCount++;
                }
            }

            if (imported > 0)
            {
                await _context.SaveChangesAsync();
            }

            return (imported, skipped, errorCount, errors);
        }

        private async Task<(int imported, int skipped, int errors, List<string> errorMessages)> ImportRoleActionsInternal(
            List<ImportRoleActionDto> mappings, int userId)
        {
            var imported = 0;
            var skipped = 0;
            var errorCount = 0;
            var errors = new List<string>();

            var roles = await _context.Roles
                .ToDictionaryAsync(r => r.Name.ToLower(), r => r.Id);

            var actions = await _context.Set<SystemAction>()
                .ToDictionaryAsync(a => a.Name.ToLower(), a => a.Id);

            var existingMappings = await _context.RolesActions
                .Select(ra => new { ra.RoleId, ra.ActionId })
                .ToListAsync();
            var existingSet = new HashSet<(int, int)>(existingMappings.Select(m => (m.RoleId, m.ActionId)));

            foreach (var mappingDto in mappings)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(mappingDto.RoleName))
                    {
                        errors.Add($"Mapping with missing role name - skipped");
                        errorCount++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(mappingDto.ActionName))
                    {
                        errors.Add($"Mapping with missing action name - skipped");
                        errorCount++;
                        continue;
                    }

                    var roleLower = mappingDto.RoleName.ToLower();
                    if (!roles.ContainsKey(roleLower))
                    {
                        errors.Add($"Role '{mappingDto.RoleName}' not found - skipped");
                        errorCount++;
                        continue;
                    }

                    var actionLower = mappingDto.ActionName.ToLower();
                    if (!actions.ContainsKey(actionLower))
                    {
                        errors.Add($"Action '{mappingDto.ActionName}' not found - skipped");
                        errorCount++;
                        continue;
                    }

                    var roleId = roles[roleLower];
                    var actionId = actions[actionLower];

                    if (existingSet.Contains((roleId, actionId)))
                    {
                        skipped++;
                        continue;
                    }

                    var roleAction = new RolesAction
                    {
                        RoleId = roleId,
                        ActionId = actionId,
                        ActionLevel = mappingDto.ActionLevel ?? 0,
                        UpdateUser = userId,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.RolesActions.Add(roleAction);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Mapping '{mappingDto.RoleName}' -> '{mappingDto.ActionName}': {ex.Message}");
                    errorCount++;
                }
            }

            if (imported > 0)
            {
                await _context.SaveChangesAsync();
            }

            return (imported, skipped, errorCount, errors);
        }
    }

    // DTOs
    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class AddActionRequest
    {
        public int ActionId { get; set; }
        public int? ActionLevel { get; set; }
    }

    public class AddUserToRoleRequest
    {
        public int UserId { get; set; }
    }

    public class ImportActionsData
    {
        public DateTime ExportDate { get; set; }
        public DateTime? FromDate { get; set; }
        public int TotalActions { get; set; }
        public List<ImportActionDto> Actions { get; set; } = new List<ImportActionDto>();
    }

    public class ImportActionDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? OnclickName { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ImportRoleActionsData
    {
        public DateTime ExportDate { get; set; }
        public DateTime? FromDate { get; set; }
        public int TotalMappings { get; set; }
        public List<ImportRoleActionDto> RoleActions { get; set; } = new List<ImportRoleActionDto>();
    }

    public class ImportRoleActionDto
    {
        public string RoleName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public int? ActionLevel { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ImportCompleteData
    {
        public DateTime ExportDate { get; set; }
        public DateTime? FromDate { get; set; }
        public int TotalActions { get; set; }
        public int TotalRoleActions { get; set; }
        public List<ImportActionDto> Actions { get; set; } = new List<ImportActionDto>();
        public List<ImportRoleActionDto> RoleActions { get; set; } = new List<ImportRoleActionDto>();
    }
}