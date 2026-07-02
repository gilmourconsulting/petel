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
    public class RolesController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly ActionAuthorizationService _authService;

        public RolesController(
            AssistDbContext context,
            SharedDbContext sharedContext,
            ActionAuthorizationService authService,
            UserSessionService userSessionService,
            ILogger<RolesController> logger)
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

        private IQueryable<Role> QueryRoles(int targetEntityId)
        {
            int.TryParse(GetCurrentSession()?.EntityId, out int sessionEntityId);
            if (targetEntityId != sessionEntityId)
                return _context.Roles.IgnoreQueryFilters().Where(r => r.EntityId == targetEntityId);
            return _context.Roles;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var roles = await QueryRoles(targetEntityId)
                    .AsNoTracking()
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.EntityId,
                        r.CreatedAt,
                        r.UpdatedAt,
                        UserCount = r.UserRoles.Count(ur => ur.IsActive),
                        ActionCount = r.RolesActions.Count()
                    })
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת תפקידים", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleDetails(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                // Step 1: load role + users + action IDs from assist_schema
                var roleBase = await _context.Roles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.Id == id && r.EntityId == targetEntityId)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.EntityId,
                        r.CreatedAt,
                        r.UpdatedAt,
                        Users = r.UserRoles
                            .Where(ur => ur.IsActive)
                            .Select(ur => new
                            {
                                ur.UserId,
                                ur.User!.Username,
                                FirstName = ur.User.FirstName ?? "",
                                LastName = ur.User.LastName ?? "",
                                FullName = (ur.User.FirstName ?? "") + " " + (ur.User.LastName ?? ""),
                                ur.User.IsActive
                            }).ToList(),
                        RoleActionIds = r.RolesActions
                            .Select(ra => new { ra.Id, ra.ActionId })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (roleBase == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                // Step 2: load action details from shared_schema
                var actionIds = roleBase.RoleActionIds.Select(a => a.ActionId).ToList();
                var actionMap = await _sharedContext.SystemActions
                    .AsNoTracking()
                    .Where(a => actionIds.Contains(a.Id))
                    .Include(a => a.ActionType)
                    .ToDictionaryAsync(a => a.Id);

                // Step 3: join in memory
                var actions = roleBase.RoleActionIds.Select(a =>
                {
                    actionMap.TryGetValue(a.ActionId, out var sa);
                    return new
                    {
                        a.Id,
                        a.ActionId,
                        ActionName = sa?.Name ?? "",
                        ActionDisplayName = sa?.DisplayName ?? "",
                        ActionReference = sa?.Reference,
                        ActionTypeName = sa?.ActionType?.Name ?? ""
                    };
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        roleBase.Id,
                        roleBase.Name,
                        roleBase.Description,
                        roleBase.EntityId,
                        roleBase.CreatedAt,
                        roleBase.UpdatedAt,
                        roleBase.Users,
                        Actions = actions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading role details {RoleId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת פרטי תפקיד", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "שם תפקיד הוא שדה חובה" });

                var exists = await _context.Roles
                    .IgnoreQueryFilters()
                    .AnyAsync(r => r.EntityId == targetEntityId && r.Name == request.Name.Trim());

                if (exists)
                    return BadRequest(new { success = false, message = "תפקיד עם שם זה כבר קיים ברשות זו" });

                var role = new Role
                {
                    EntityId    = targetEntityId,
                    Name        = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    CreatedAt   = DateTime.UtcNow,
                    UserId      = currentUserId,
                    UpdatedAt   = DateTime.UtcNow,
                    UpdateUser  = currentUserId
                };

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created role {RoleName} for entity {EntityId}", role.Name, targetEntityId);
                return Ok(new { success = true, message = "תפקיד נוצר בהצלחה", data = new { role.Id } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת תפקיד", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == id && r.EntityId == targetEntityId);

                if (role == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                role.Name        = request.Name.Trim();
                role.Description = request.Description?.Trim();
                role.UpdatedAt   = DateTime.UtcNow;
                role.UpdateUser  = currentUserId;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "תפקיד עודכן בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role {RoleId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון תפקיד", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == id && r.EntityId == targetEntityId);

                if (role == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                var hasUsers = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .AnyAsync(ur => ur.RoleId == id && ur.IsActive);

                if (hasUsers)
                    return BadRequest(new { success = false, message = "לא ניתן למחוק תפקיד שמשויכים אליו משתמשים פעילים" });

                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();
                await _authService.RefreshCacheAsync();

                _logger.LogInformation("Deleted role {RoleId}", id);
                return Ok(new { success = true, message = "תפקיד נמחק בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {RoleId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה במחיקת תפקיד", error = ex.Message });
            }
        }

        [HttpGet("available-users")]
        public async Task<IActionResult> GetAvailableUsers([FromQuery] int roleId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var assignedUserIds = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.RoleId == roleId && ur.IsActive)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                var users = await _context.Users
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(u => u.EntityId == targetEntityId && u.IsActive && !assignedUserIds.Contains(u.Id))
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        FullName = (u.FirstName ?? "") + " " + (u.LastName ?? "")
                    })
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available users");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת משתמשים זמינים", error = ex.Message });
            }
        }

        [HttpGet("available-actions")]
        public async Task<IActionResult> GetAvailableActions([FromQuery] int roleId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var assignedActionIds = await _context.RolesActions
                    .IgnoreQueryFilters()
                    .Where(ra => ra.RoleId == roleId)
                    .Select(ra => ra.ActionId)
                    .ToListAsync();

                var actions = await _sharedContext.SystemActions
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.DisplayName,
                        a.Reference,
                        a.Description,
                        ActionTypeName = a.ActionType != null ? a.ActionType.Name : "",
                        IsAssigned = assignedActionIds.Contains(a.Id)
                    })
                    .OrderBy(a => a.Reference)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

                return Ok(new { success = true, data = actions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available actions");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת הרשאות זמינות", error = ex.Message });
            }
        }

        [HttpPost("{roleId}/users")]
        public async Task<IActionResult> AddUserToRole(int roleId, [FromBody] AddUserToRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == roleId && r.EntityId == targetEntityId);

                if (role == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == request.UserId && u.EntityId == targetEntityId);

                if (user == null)
                    return NotFound(new { success = false, message = "משתמש לא נמצא" });

                var existing = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == roleId);

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
                        UserId      = request.UserId,
                        RoleId      = roleId,
                        IsActive    = true,
                        CreatedAt   = DateTime.UtcNow,
                        UpdateUser  = currentUserId
                    });
                }

                await _context.SaveChangesAsync();
                _authService.InvalidateUserCache(request.UserId);
                return Ok(new { success = true, message = "משתמש הוסף לתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to role {RoleId}", roleId);
                return StatusCode(500, new { success = false, message = "שגיאה בהוספת משתמש לתפקיד", error = ex.Message });
            }
        }

        [HttpDelete("{roleId}/users/{userId}")]
        public async Task<IActionResult> RemoveUserFromRole(int roleId, int userId)
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
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.EntityId == targetEntityId);

                if (userRole == null)
                    return NotFound(new { success = false, message = "שיוך לא נמצא" });

                userRole.IsActive   = false;
                userRole.UpdatedAt  = DateTime.UtcNow;
                userRole.UpdateUser = currentUserId;

                await _context.SaveChangesAsync();
                _authService.InvalidateUserCache(userId);
                return Ok(new { success = true, message = "משתמש הוסר מהתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from role {RoleId}", roleId);
                return StatusCode(500, new { success = false, message = "שגיאה בהסרת משתמש מתפקיד", error = ex.Message });
            }
        }

        [HttpPost("{roleId}/actions")]
        public async Task<IActionResult> AddActionToRole(int roleId, [FromBody] AddActionToRoleRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.UserId, out int currentUserId))
                    return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

                var targetEntityId = ResolveTargetEntityId(session);

                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == roleId && r.EntityId == targetEntityId);

                if (role == null)
                    return NotFound(new { success = false, message = "תפקיד לא נמצא" });

                var action = await _sharedContext.SystemActions.FindAsync(request.ActionId);
                if (action == null)
                    return NotFound(new { success = false, message = "הרשאה לא נמצאה" });

                var exists = await _context.RolesActions
                    .IgnoreQueryFilters()
                    .AnyAsync(ra => ra.RoleId == roleId && ra.ActionId == request.ActionId);

                if (exists)
                    return BadRequest(new { success = false, message = "הרשאה כבר משויכת לתפקיד" });

                _context.RolesActions.Add(new RolesAction
                {
                    EntityId    = targetEntityId,
                    RoleId      = roleId,
                    ActionId    = request.ActionId,
                    CreatedAt   = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await _authService.RefreshCacheAsync();
                return Ok(new { success = true, message = "הרשאה הוסיפה לתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding action to role {RoleId}", roleId);
                return StatusCode(500, new { success = false, message = "שגיאה בהוספת הרשאה לתפקיד", error = ex.Message });
            }
        }

        [HttpDelete("{roleId}/actions/{actionId}")]
        public async Task<IActionResult> RemoveActionFromRole(int roleId, int actionId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var rolesAction = await _context.RolesActions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ra => ra.RoleId == roleId && ra.ActionId == actionId && ra.EntityId == targetEntityId);

                if (rolesAction == null)
                    return NotFound(new { success = false, message = "שיוך הרשאה לא נמצא" });

                _context.RolesActions.Remove(rolesAction);
                await _context.SaveChangesAsync();
                await _authService.RefreshCacheAsync();
                return Ok(new { success = true, message = "הרשאה הוסרה מהתפקיד בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing action from role {RoleId}", roleId);
                return StatusCode(500, new { success = false, message = "שגיאה בהסרת הרשאה מתפקיד", error = ex.Message });
            }
        }

        [HttpPost("refresh-cache")]
        public async Task<IActionResult> RefreshCache()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                await _authService.RefreshCacheAsync();
                return Ok(new { success = true, message = "מטמון האבטחה רוענן בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache");
                return StatusCode(500, new { success = false, message = "שגיאה ברענון המטמון", error = ex.Message });
            }
        }
    }

    public class CreateRoleRequest
    {
        public string Name         { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Name         { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class AddUserToRoleRequest
    {
        public int UserId { get; set; }
    }

    public class AddActionToRoleRequest
    {
        public int ActionId { get; set; }
    }
}
