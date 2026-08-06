using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Helpers;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/assistant-types")]
    public class AssistantTypesController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public AssistantTypesController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<AssistantTypesController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var query = from at in _sharedContext.AssistantTypes.AsNoTracking()
                        join lvl in _sharedContext.AssistantLevels.AsNoTracking()
                            on at.Level equals lvl.Code into levelJoin
                        from lvl in levelJoin.DefaultIfEmpty()
                        select new { at, LevelDisplayName = lvl != null ? lvl.DisplayName : null };

            if (!includeInactive)
                query = query.Where(x => x.at.IsActive);

            var types = await query
                .OrderBy(x => x.at.SortOrder)
                .ThenBy(x => x.at.DisplayName)
                .Select(x => new AssistantTypeDto
                {
                    Id = x.at.Id,
                    Name = x.at.Name,
                    DisplayName = x.at.DisplayName,
                    Description = x.at.Description,
                    SortOrder = x.at.SortOrder,
                    IsActive = x.at.IsActive,
                    Level = x.at.Level,
                    LevelDisplayName = x.LevelDisplayName,
                    PositionType = x.at.PositionType,
                    PositionHours = x.at.PositionHours
                })
                .ToListAsync();

            return Ok(new { success = true, data = types });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAssistantTypeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest(new { success = false, message = "שם ושם תצוגה הם שדות חובה" });

            if (!PositionTypeHelper.TryNormalize(request.PositionType, out var positionType, out var positionError))
                return BadRequest(new { success = false, message = positionError });

            var levelResult = await NormalizeLevelAsync(request.Level);
            if (!levelResult.Ok)
                return BadRequest(new { success = false, message = levelResult.Error });

            var name = request.Name.Trim().ToLowerInvariant().Replace(' ', '_');
            if (await _sharedContext.AssistantTypes.AnyAsync(at => at.Name == name))
                return BadRequest(new { success = false, message = "סוג סייעת עם שם זה כבר קיים" });

            var type = new AssistantType
            {
                Name = name,
                DisplayName = request.DisplayName.Trim(),
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
                Level = levelResult.Code,
                PositionType = positionType,
                PositionHours = request.PositionHours,
                IsActive = true
            };

            _sharedContext.AssistantTypes.Add(type);
            await _sharedContext.SaveChangesAsync();

            return Ok(new { success = true, message = "סוג סייעת נוצר בהצלחה", data = new { type.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAssistantTypeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest(new { success = false, message = "שם תצוגה הוא שדה חובה" });

            if (!PositionTypeHelper.TryNormalize(request.PositionType, out var positionType, out var positionError))
                return BadRequest(new { success = false, message = positionError });

            var levelResult = await NormalizeLevelAsync(request.Level);
            if (!levelResult.Ok)
                return BadRequest(new { success = false, message = levelResult.Error });

            var type = await _sharedContext.AssistantTypes.FindAsync(id);
            if (type == null)
                return NotFound(new { success = false, message = "סוג סייעת לא נמצא" });

            type.DisplayName = request.DisplayName.Trim();
            type.Description = request.Description?.Trim();
            type.SortOrder = request.SortOrder;
            type.IsActive = request.IsActive;
            type.Level = levelResult.Code;
            type.PositionType = positionType;
            type.PositionHours = request.PositionHours;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "סוג סייעת עודכן בהצלחה" });
        }

        private async Task<(bool Ok, string? Code, string? Error)> NormalizeLevelAsync(string? level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return (true, null, null);

            var code = level.Trim().ToLowerInvariant();
            var exists = await _sharedContext.AssistantLevels
                .AsNoTracking()
                .AnyAsync(l => l.Code == code && l.IsActive);

            if (!exists)
                return (false, null, "רמת סייעת לא חוקית");

            return (true, code, null);
        }
    }
}
