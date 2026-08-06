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
    [Route("api/meitar-topics")]
    public class MeitarTopicsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public MeitarTopicsController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<MeitarTopicsController> logger)
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

            var query = _sharedContext.MeitarTopics.AsNoTracking();
            if (!includeInactive)
                query = query.Where(t => t.IsActive);

            var items = await query
                .OrderBy(t => t.Code)
                .Select(t => new MeitarTopicDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    Description = t.Description,
                    PositionType = t.PositionType,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMeitarTopicRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "קוד ושם הם שדות חובה" });

            if (!PositionTypeHelper.TryNormalize(request.PositionType, out var positionType, out var positionError))
                return BadRequest(new { success = false, message = positionError });

            var code = request.Code.Trim();
            if (await _sharedContext.MeitarTopics.AnyAsync(t => t.Code == code))
                return BadRequest(new { success = false, message = "נושא עם קוד זה כבר קיים" });

            var entity = new MeitarTopic
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                PositionType = positionType,
                IsActive = true
            };

            _sharedContext.MeitarTopics.Add(entity);
            await _sharedContext.SaveChangesAsync();

            return Ok(new { success = true, message = "נושא מיתר נוצר בהצלחה", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMeitarTopicRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "קוד ושם הם שדות חובה" });

            if (!PositionTypeHelper.TryNormalize(request.PositionType, out var positionType, out var positionError))
                return BadRequest(new { success = false, message = positionError });

            var entity = await _sharedContext.MeitarTopics.FindAsync(id);
            if (entity == null)
                return NotFound(new { success = false, message = "נושא מיתר לא נמצא" });

            var code = request.Code.Trim();
            if (await _sharedContext.MeitarTopics.AnyAsync(t => t.Code == code && t.Id != id))
                return BadRequest(new { success = false, message = "נושא עם קוד זה כבר קיים" });

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = request.Description?.Trim();
            entity.PositionType = positionType;
            entity.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "נושא מיתר עודכן בהצלחה" });
        }
    }
}
