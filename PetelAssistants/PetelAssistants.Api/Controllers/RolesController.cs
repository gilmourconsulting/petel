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
                        DisplayName = sa?.DisplayName ?? sa?.Name ?? "",
                        Reference = sa?.Reference,
                        ActionTypeName = sa?.ActionType?.Name ?? ""
                    };
                })
                .OrderBy(a => a.ActionTypeName)
                .ThenBy(a => a.DisplayName)
                .ToList();

                var users = roleBase.Users.Select(u => new
                {
                    Id = u.UserId,
                    u.Username,
                    u.FullName,
                    IsActive = u.IsActive
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        role = new
                        {
                            roleBase.Id,
                            roleBase.Name,
                            roleBase.Description,
                            roleBase.CreatedAt,
                            roleBase.UpdatedAt
                        },
                        users,
                        actions
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
        public async Task<IActionResult> GetAvailableActions([FromQuery] int? roleId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var query = _sharedContext.SystemActions
                    .AsNoTracking()
                    .Include(a => a.ActionType)
                    .Where(a => a.IsActive);

                if (roleId.HasValue)
                {
                    var assignedActionIds = await _context.RolesActions
                        .IgnoreQueryFilters()
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
                        ActionTypeName = a.ActionType != null ? a.ActionType.Name : ""
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

        [HttpGet("actions/export")]
        public async Task<IActionResult> ExportActions([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var query = _sharedContext.SystemActions
                    .AsNoTracking()
                    .Include(a => a.ActionType)
                    .AsQueryable();

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
                        ActionTypeName = a.ActionType != null ? a.ActionType.Name : "",
                        a.Reference,
                        a.IsActive,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

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
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא הפעולות", error = ex.Message });
            }
        }

        [HttpPost("actions/import")]
        public async Task<IActionResult> ImportActions(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();
                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportActionsData>(jsonContent);

                if (importData?.Actions == null || !importData.Actions.Any())
                    return BadRequest(new { success = false, message = "No actions found in file" });

                var (imported, skipped, errorCount, errors) = await ImportActionsInternal(importData.Actions);

                if (imported > 0)
                    await _authService.RefreshCacheAsync();

                return Ok(new
                {
                    success = true,
                    message = $"ייבוא הסתיים: {imported} פעולות יובאו, {skipped} כבר קיימות, {errorCount} שגיאות",
                    ImportedCount = imported,
                    SkippedCount = skipped,
                    ErrorCount = errorCount,
                    Errors = errors.Take(50).ToList(),
                    HasMoreErrors = errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing actions file");
                return StatusCode(500, new { success = false, message = $"שגיאה בעיבוד הקובץ: {ex.Message}", error = ex.Message });
            }
        }

        [HttpGet("role-actions/export")]
        public async Task<IActionResult> ExportRoleActions([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var mappingsQuery = _context.RolesActions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(ra => ra.EntityId == targetEntityId);

                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    mappingsQuery = mappingsQuery.Where(ra => ra.UpdatedAt >= fromDateUtc);
                }

                var rawMappings = await mappingsQuery
                    .Select(ra => new { ra.RoleId, ra.ActionId, ra.ActionLevel, ra.UpdatedAt })
                    .ToListAsync();

                var roleNames = await _context.Roles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.EntityId == targetEntityId)
                    .ToDictionaryAsync(r => r.Id, r => r.Name);

                var actionIds = rawMappings.Select(m => m.ActionId).Distinct().ToList();
                var actionNames = await _sharedContext.SystemActions
                    .AsNoTracking()
                    .Where(a => actionIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, a => a.Name);

                var roleActions = rawMappings
                    .Where(m => roleNames.ContainsKey(m.RoleId) && actionNames.ContainsKey(m.ActionId))
                    .Select(m => new
                    {
                        RoleName = roleNames[m.RoleId],
                        ActionName = actionNames[m.ActionId],
                        m.ActionLevel,
                        m.UpdatedAt
                    })
                    .OrderBy(ra => ra.RoleName)
                    .ThenBy(ra => ra.ActionName)
                    .ToList();

                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    FromDate = fromDate,
                    EntityId = targetEntityId,
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
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא קישורי תפקידים-פעולות", error = ex.Message });
            }
        }

        [HttpPost("role-actions/import")]
        public async Task<IActionResult> ImportRoleActions(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.UserId, out int currentUserId))
                return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();
                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportRoleActionsData>(jsonContent);

                if (importData?.RoleActions == null || !importData.RoleActions.Any())
                    return BadRequest(new { success = false, message = "No role-action mappings found in file" });

                var targetEntityId = ResolveTargetEntityId(session);
                var (imported, skipped, errorCount, errors) = await ImportRoleActionsInternal(importData.RoleActions, targetEntityId, currentUserId);

                if (imported > 0)
                    await _authService.RefreshCacheAsync();

                return Ok(new
                {
                    success = true,
                    message = $"ייבוא הסתיים: {imported} קישורים יובאו, {skipped} כבר קיימים, {errorCount} שגיאות",
                    ImportedCount = imported,
                    SkippedCount = skipped,
                    ErrorCount = errorCount,
                    Errors = errors.Take(50).ToList(),
                    HasMoreErrors = errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing role-actions file");
                return StatusCode(500, new { success = false, message = $"שגיאה בעיבוד הקובץ: {ex.Message}", error = ex.Message });
            }
        }

        [HttpGet("complete-export")]
        public async Task<IActionResult> ExportComplete([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var targetEntityId = ResolveTargetEntityId(session);

                var actionsQuery = _sharedContext.SystemActions
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
                        ActionTypeName = a.ActionType != null ? a.ActionType.Name : "",
                        a.Reference,
                        a.IsActive,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .OrderBy(a => a.ActionTypeName)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

                var mappingsQuery = _context.RolesActions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(ra => ra.EntityId == targetEntityId);

                if (fromDate.HasValue)
                {
                    var fromDateUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                    mappingsQuery = mappingsQuery.Where(ra => ra.UpdatedAt >= fromDateUtc);
                }

                var rawMappings = await mappingsQuery
                    .Select(ra => new { ra.RoleId, ra.ActionId, ra.ActionLevel, ra.UpdatedAt })
                    .ToListAsync();

                var roleNames = await _context.Roles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.EntityId == targetEntityId)
                    .ToDictionaryAsync(r => r.Id, r => r.Name);

                var actionIds = rawMappings.Select(m => m.ActionId).Distinct().ToList();
                var actionNames = await _sharedContext.SystemActions
                    .AsNoTracking()
                    .Where(a => actionIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, a => a.Name);

                var roleActions = rawMappings
                    .Where(m => roleNames.ContainsKey(m.RoleId) && actionNames.ContainsKey(m.ActionId))
                    .Select(m => new
                    {
                        RoleName = roleNames[m.RoleId],
                        ActionName = actionNames[m.ActionId],
                        m.ActionLevel,
                        m.UpdatedAt
                    })
                    .OrderBy(ra => ra.RoleName)
                    .ThenBy(ra => ra.ActionName)
                    .ToList();

                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    FromDate = fromDate,
                    EntityId = targetEntityId,
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
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא החבילה המלאה", error = ex.Message });
            }
        }

        [HttpPost("complete-import")]
        public async Task<IActionResult> ImportComplete(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .json files are supported" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "File too large (max 10MB)" });

            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.UserId, out int currentUserId))
                return BadRequest(new { success = false, message = "מזהה משתמש לא תקין בסשן" });

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var jsonContent = await reader.ReadToEndAsync();
                var importData = System.Text.Json.JsonSerializer.Deserialize<ImportCompleteData>(jsonContent);

                if (importData == null)
                    return BadRequest(new { success = false, message = "Invalid file format" });

                var targetEntityId = ResolveTargetEntityId(session);
                var errors = new List<string>();
                var actionsImported = 0;
                var actionsSkipped = 0;
                var actionsErrors = 0;
                var roleActionsImported = 0;
                var roleActionsSkipped = 0;
                var roleActionsErrors = 0;

                if (importData.Actions != null && importData.Actions.Any())
                {
                    var result = await ImportActionsInternal(importData.Actions);
                    actionsImported = result.imported;
                    actionsSkipped = result.skipped;
                    actionsErrors = result.errors;
                    errors.AddRange(result.errorMessages);
                }

                if (importData.RoleActions != null && importData.RoleActions.Any())
                {
                    var result = await ImportRoleActionsInternal(importData.RoleActions, targetEntityId, currentUserId);
                    roleActionsImported = result.imported;
                    roleActionsSkipped = result.skipped;
                    roleActionsErrors = result.errors;
                    errors.AddRange(result.errorMessages);
                }

                if (actionsImported > 0 || roleActionsImported > 0)
                    await _authService.RefreshCacheAsync();

                var message = $"ייבוא מלא הסתיים:\nפעולות: {actionsImported} יובאו, {actionsSkipped} כבר קיימות\nקישורים: {roleActionsImported} יובאו, {roleActionsSkipped} כבר קיימים\nשגיאות: {actionsErrors + roleActionsErrors}";

                return Ok(new
                {
                    success = true,
                    message,
                    ActionsImported = actionsImported,
                    ActionsSkipped = actionsSkipped,
                    ActionsErrors = actionsErrors,
                    RoleActionsImported = roleActionsImported,
                    RoleActionsSkipped = roleActionsSkipped,
                    RoleActionsErrors = roleActionsErrors,
                    Errors = errors.Take(50).ToList(),
                    HasMoreErrors = errors.Count > 50
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing complete package");
                return StatusCode(500, new { success = false, message = $"שגיאה בייבוא החבילה המלאה: {ex.Message}", error = ex.Message });
            }
        }

        private async Task<(int imported, int skipped, int errors, List<string> errorMessages)> ImportActionsInternal(
            List<ImportActionDto> actions)
        {
            var imported = 0;
            var skipped = 0;
            var errorCount = 0;
            var errors = new List<string>();

            var existingActionNames = await _sharedContext.SystemActions
                .Select(a => a.Name.ToLower())
                .ToListAsync();

            var actionTypes = await _sharedContext.ActionTypes
                .ToDictionaryAsync(at => at.Name.ToLower(), at => at.Id);

            foreach (var actionDto in actions)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(actionDto.Name))
                    {
                        errors.Add("Action with missing name - skipped");
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
                        DisplayName = actionDto.DisplayName ?? actionDto.Name,
                        Description = actionDto.Description,
                        ActionTypeId = actionTypes[actionTypeLower],
                        Reference = actionDto.Reference,
                        IsActive = actionDto.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _sharedContext.SystemActions.Add(action);
                    existingActionNames.Add(actionDto.Name.ToLower());
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Action '{actionDto.Name}': {ex.Message}");
                    errorCount++;
                }
            }

            if (imported > 0)
                await _sharedContext.SaveChangesAsync();

            return (imported, skipped, errorCount, errors);
        }

        private async Task<(int imported, int skipped, int errors, List<string> errorMessages)> ImportRoleActionsInternal(
            List<ImportRoleActionDto> mappings, int targetEntityId, int currentUserId)
        {
            var imported = 0;
            var skipped = 0;
            var errorCount = 0;
            var errors = new List<string>();

            var roles = await _context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.EntityId == targetEntityId)
                .ToDictionaryAsync(r => r.Name.ToLower(), r => r.Id);

            var actions = await _sharedContext.SystemActions
                .ToDictionaryAsync(a => a.Name.ToLower(), a => a.Id);

            var existingMappings = await _context.RolesActions
                .IgnoreQueryFilters()
                .Where(ra => ra.EntityId == targetEntityId)
                .Select(ra => new { ra.RoleId, ra.ActionId })
                .ToListAsync();

            var existingSet = new HashSet<(int, int)>(existingMappings.Select(m => (m.RoleId, m.ActionId)));

            foreach (var mappingDto in mappings)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(mappingDto.RoleName))
                    {
                        errors.Add("Mapping with missing role name - skipped");
                        errorCount++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(mappingDto.ActionName))
                    {
                        errors.Add("Mapping with missing action name - skipped");
                        errorCount++;
                        continue;
                    }

                    var roleLower = mappingDto.RoleName.ToLower();
                    if (!roles.ContainsKey(roleLower))
                    {
                        errors.Add($"Role '{mappingDto.RoleName}' not found in entity - skipped");
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

                    _context.RolesActions.Add(new RolesAction
                    {
                        EntityId = targetEntityId,
                        RoleId = roleId,
                        ActionId = actionId,
                        ActionLevel = mappingDto.ActionLevel ?? 0,
                        UpdateUser = currentUserId,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });

                    existingSet.Add((roleId, actionId));
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Mapping '{mappingDto.RoleName}' -> '{mappingDto.ActionName}': {ex.Message}");
                    errorCount++;
                }
            }

            if (imported > 0)
                await _context.SaveChangesAsync();

            return (imported, skipped, errorCount, errors);
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

    public class ImportActionsData
    {
        public DateTime ExportDate { get; set; }
        public DateTime? FromDate { get; set; }
        public int TotalActions { get; set; }
        public List<ImportActionDto> Actions { get; set; } = new();
    }

    public class ImportActionDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ImportRoleActionsData
    {
        public DateTime ExportDate { get; set; }
        public DateTime? FromDate { get; set; }
        public int TotalMappings { get; set; }
        public List<ImportRoleActionDto> RoleActions { get; set; } = new();
    }

    public class ImportRoleActionDto
    {
        public string RoleName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public int? ActionLevel { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ImportCompleteData
    {
        public List<ImportActionDto> Actions { get; set; } = new();
        public List<ImportRoleActionDto> RoleActions { get; set; } = new();
    }
}
