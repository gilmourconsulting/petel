using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : BaseController
    {
        private readonly SharedDbContext _sharedContext;
        private readonly SystemAttributeCache _cache;

        public SystemAttributesController(
            SharedDbContext sharedContext,
            SystemAttributeCache cache,
            UserSessionService userSessionService,
            ILogger<SystemAttributesController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
            _cache = cache;
        }

        /// <summary>
        /// Public read used by login / layout. Does not require a session.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var attrs = await _sharedContext.SystemAttributes
                    .AsNoTracking()
                    .OrderBy(a => a.Id)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        value = a.Value,
                        valueType = a.ValueType,
                        description = a.Description
                    })
                    .ToListAsync();

                return Ok(attrs);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "system_attributes table not found; returning empty configuration list");
                return Ok(Array.Empty<object>());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading system attributes; returning empty configuration list");
                return Ok(Array.Empty<object>());
            }
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetAllAdmin()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var attrs = await _sharedContext.SystemAttributes
                .AsNoTracking()
                .OrderBy(a => a.Id)
                .Select(a => new SystemAttributeAdminDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Value = a.Value,
                    ValueType = a.ValueType,
                    Description = a.Description
                })
                .ToListAsync();

            return Ok(new { success = true, data = attrs });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSystemAttributeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "שם ההגדרה הוא שדה חובה" });

            if (request.Value == null)
                return BadRequest(new { success = false, message = "ערך ההגדרה הוא שדה חובה" });

            var name = request.Name.Trim();
            if (await _sharedContext.SystemAttributes.AnyAsync(a => a.Name == name))
                return BadRequest(new { success = false, message = $"הגדרה בשם '{name}' כבר קיימת" });

            var valueType = string.IsNullOrWhiteSpace(request.ValueType) ? "string" : request.ValueType.Trim();
            if (!ValidateDataType(request.Value, valueType))
                return BadRequest(new { success = false, message = $"ערך לא תקין עבור סוג {valueType}" });

            var entity = new SystemAttribute
            {
                Name = name,
                Value = request.Value,
                ValueType = valueType,
                Description = request.Description?.Trim()
            };

            _sharedContext.SystemAttributes.Add(entity);
            await _sharedContext.SaveChangesAsync();
            await ReloadCacheFromDatabaseAsync();

            return Ok(new { success = true, message = "הגדרה נוצרה בהצלחה", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSystemAttributeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.Value == null)
                return BadRequest(new { success = false, message = "ערך ההגדרה לא יכול להיות ריק" });

            var attribute = await _sharedContext.SystemAttributes.FirstOrDefaultAsync(a => a.Id == id);
            if (attribute == null)
                return NotFound(new { success = false, message = "הגדרה לא נמצאה" });

            var valueType = string.IsNullOrWhiteSpace(request.ValueType)
                ? attribute.ValueType
                : request.ValueType.Trim();

            if (!ValidateDataType(request.Value, valueType))
                return BadRequest(new { success = false, message = $"ערך לא תקין עבור סוג {valueType}" });

            attribute.Value = request.Value;
            attribute.ValueType = valueType;
            if (request.Description != null)
                attribute.Description = request.Description.Trim();

            await _sharedContext.SaveChangesAsync();
            await ReloadCacheFromDatabaseAsync();

            return Ok(new { success = true, message = "הגדרה עודכנה בהצלחה" });
        }

        [HttpPost("reload")]
        public async Task<IActionResult> Reload()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            await ReloadCacheFromDatabaseAsync();
            return Ok(new { success = true, message = "מטמון מאפייני המערכת עודכן" });
        }

        private async Task ReloadCacheFromDatabaseAsync()
        {
            var attributes = await _sharedContext.SystemAttributes
                .AsNoTracking()
                .Select(a => new { a.Name, a.Value })
                .ToListAsync();

            _cache.Load(attributes.Select(a => (a.Name, a.Value)));
        }

        private static bool ValidateDataType(string value, string dataType)
        {
            return dataType?.ToLowerInvariant() switch
            {
                "integer" => int.TryParse(value, out _),
                "boolean" => bool.TryParse(value, out _) ||
                             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                             value == "0" || value == "1",
                _ => true
            };
        }
    }
}
