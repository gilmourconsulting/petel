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
}