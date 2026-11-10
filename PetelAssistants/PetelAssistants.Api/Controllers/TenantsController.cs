using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TenantsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;
        private readonly AssistDbContext _assistContext;

        public TenantsController(
            SharedDbContext sharedContext,
            AssistDbContext assistContext,
            UserSessionService userSessionService,
            ILogger<TenantsController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
            _assistContext = assistContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var entities = await _sharedContext.Entities
                    .AsNoTracking()
                    .Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.IsActive,
                        e.EntityTypeId,
                        e.SymbolCode,
                        EntityTypeName = e.EntityType != null ? e.EntityType.Name : null
                    })
                    .OrderBy(e => e.Name)
                    .ToListAsync();

                var entityIds = entities.Select(e => e.Id).ToList();

                var userCounts = await _assistContext.Users
                    .IgnoreQueryFilters()
                    .Where(u => entityIds.Contains(u.EntityId) && u.IsActive)
                    .GroupBy(u => u.EntityId)
                    .Select(g => new { EntityId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var countDict = userCounts.ToDictionary(x => x.EntityId, x => x.Count);

                var result = entities.Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.IsActive,
                    e.EntityTypeId,
                    e.SymbolCode,
                    e.EntityTypeName,
                    UserCount = countDict.TryGetValue(e.Id, out var c) ? c : 0
                }).ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tenants");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת רשויות", error = ex.Message });
            }
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetEntityTypes()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var types = await _sharedContext.EntityTypes
                    .AsNoTracking()
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name, t.Description })
                    .ToListAsync();

                return Ok(new { success = true, data = types });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity types");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סוגי רשויות", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "שם רשות הוא שדה חובה" });

                var name = request.Name.Trim();
                if (!TryNormalizeSymbolCode(request.SymbolCode, out var symbolCode, out var symbolError))
                    return BadRequest(new { success = false, message = symbolError });

                var exists = await _sharedContext.Entities
                    .AnyAsync(e => e.Name == name);

                if (exists)
                    return BadRequest(new { success = false, message = "רשות עם שם זה כבר קיימת" });

                if (symbolCode != null)
                {
                    var symbolTaken = await _sharedContext.Entities
                        .AnyAsync(e => e.SymbolCode == symbolCode);
                    if (symbolTaken)
                        return BadRequest(new { success = false, message = "רשות עם סמל זה כבר קיימת" });
                }

                var entity = new Entity
                {
                    Name         = name,
                    EntityTypeId = request.EntityTypeId,
                    SymbolCode   = symbolCode,
                    IsActive     = true
                };

                _sharedContext.Entities.Add(entity);
                await _sharedContext.SaveChangesAsync();

                _logger.LogInformation("Created tenant {TenantName} (ID: {Id})", entity.Name, entity.Id);
                return Ok(new { success = true, message = "רשות נוצרה בהצלחה", data = new { entity.Id } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenant");
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת רשות", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTenant(int id, [FromBody] UpdateTenantRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "שם רשות הוא שדה חובה" });

                var entity = await _sharedContext.Entities.FindAsync(id);
                if (entity == null)
                    return NotFound(new { success = false, message = "רשות לא נמצאה" });

                var name = request.Name.Trim();
                if (!TryNormalizeSymbolCode(request.SymbolCode, out var symbolCode, out var symbolError))
                    return BadRequest(new { success = false, message = symbolError });

                if (symbolCode != null)
                {
                    var symbolTaken = await _sharedContext.Entities
                        .AnyAsync(e => e.SymbolCode == symbolCode && e.Id != id);
                    if (symbolTaken)
                        return BadRequest(new { success = false, message = "רשות עם סמל זה כבר קיימת" });
                }

                entity.Name         = name;
                entity.EntityTypeId = request.EntityTypeId;
                entity.SymbolCode   = symbolCode;

                await _sharedContext.SaveChangesAsync();
                return Ok(new { success = true, message = "פרטי רשות עודכנו בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tenant {TenantId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון רשות", error = ex.Message });
            }
        }

        private static bool TryNormalizeSymbolCode(string? symbolCode, out string? normalized, out string error)
        {
            normalized = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(symbolCode))
                return true;

            normalized = symbolCode.Trim();
            if (normalized.Length > 20)
            {
                error = "סמל רשות ארוך מדי";
                normalized = null;
                return false;
            }

            return true;
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> ActivateTenant(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var entity = await _sharedContext.Entities.FindAsync(id);
                if (entity == null)
                    return NotFound(new { success = false, message = "רשות לא נמצאה" });

                entity.IsActive = true;
                await _sharedContext.SaveChangesAsync();
                return Ok(new { success = true, message = "רשות הופעלה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating tenant {TenantId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בהפעלת רשות", error = ex.Message });
            }
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivateTenant(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var entity = await _sharedContext.Entities.FindAsync(id);
                if (entity == null)
                    return NotFound(new { success = false, message = "רשות לא נמצאה" });

                if (!int.TryParse(GetCurrentSession()?.EntityId, out int sessionEntityId) || sessionEntityId == id)
                    return BadRequest(new { success = false, message = "לא ניתן להשבית את הרשות שאתה מחובר אליה" });

                entity.IsActive = false;
                await _sharedContext.SaveChangesAsync();
                return Ok(new { success = true, message = "רשות הושבתה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating tenant {TenantId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בהשבתת רשות", error = ex.Message });
            }
        }
    }

    public class CreateTenantRequest
    {
        public string Name          { get; set; } = string.Empty;
        public int?   EntityTypeId  { get; set; }
        public string? SymbolCode   { get; set; }
    }

    public class UpdateTenantRequest
    {
        public string Name          { get; set; } = string.Empty;
        public int?   EntityTypeId  { get; set; }
        public string? SymbolCode   { get; set; }
    }
}
