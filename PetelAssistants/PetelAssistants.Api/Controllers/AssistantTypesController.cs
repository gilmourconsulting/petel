using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
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

            var query = _sharedContext.AssistantTypes.AsNoTracking();
            if (!includeInactive)
                query = query.Where(at => at.IsActive);

            var types = await query
                .OrderBy(at => at.SortOrder)
                .ThenBy(at => at.DisplayName)
                .Select(at => new AssistantTypeDto
                {
                    Id = at.Id,
                    Name = at.Name,
                    DisplayName = at.DisplayName,
                    Description = at.Description,
                    SortOrder = at.SortOrder,
                    IsActive = at.IsActive
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

            var name = request.Name.Trim().ToLowerInvariant().Replace(' ', '_');
            if (await _sharedContext.AssistantTypes.AnyAsync(at => at.Name == name))
                return BadRequest(new { success = false, message = "סוג סייעת עם שם זה כבר קיים" });

            var type = new AssistantType
            {
                Name = name,
                DisplayName = request.DisplayName.Trim(),
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
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

            var type = await _sharedContext.AssistantTypes.FindAsync(id);
            if (type == null)
                return NotFound(new { success = false, message = "סוג סייעת לא נמצא" });

            type.DisplayName = request.DisplayName.Trim();
            type.Description = request.Description?.Trim();
            type.SortOrder = request.SortOrder;
            type.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "סוג סייעת עודכן בהצלחה" });
        }
    }
}
