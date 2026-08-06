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
    [Route("api/ministry-participation-options")]
    public class MinistryParticipationOptionsController : BaseController
    {
        private readonly SharedDbContext _shared;

        public MinistryParticipationOptionsController(
            SharedDbContext shared,
            UserSessionService userSessionService,
            ILogger<MinistryParticipationOptionsController> logger)
            : base(userSessionService, logger)
        {
            _shared = shared;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var query = _shared.MinistryParticipationOptions.AsNoTracking();
            if (!includeInactive)
                query = query.Where(o => o.IsActive);

            var options = await query
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new MinistryParticipationOptionDto
                {
                    Id = o.Id,
                    Percentage = o.Percentage,
                    DisplayOrder = o.DisplayOrder,
                    IsActive = o.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = options });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMinistryParticipationOptionRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.Percentage < 0 || request.Percentage > 100)
                return BadRequest(new { success = false, message = "אחוז חייב להיות בין 0 ל-100" });

            if (await _shared.MinistryParticipationOptions.AnyAsync(o => o.Percentage == request.Percentage))
                return BadRequest(new { success = false, message = "אחוז השתתפות זה כבר קיים" });

            var entity = new MinistryParticipationOption
            {
                Percentage = request.Percentage,
                DisplayOrder = request.DisplayOrder,
                IsActive = true
            };

            _shared.MinistryParticipationOptions.Add(entity);
            await _shared.SaveChangesAsync();

            return Ok(new { success = true, message = "אחוז השתתפות נוצר בהצלחה", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMinistryParticipationOptionRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.Percentage < 0 || request.Percentage > 100)
                return BadRequest(new { success = false, message = "אחוז חייב להיות בין 0 ל-100" });

            var entity = await _shared.MinistryParticipationOptions.FindAsync(id);
            if (entity == null)
                return NotFound(new { success = false, message = "אחוז השתתפות לא נמצא" });

            if (await _shared.MinistryParticipationOptions.AnyAsync(o => o.Percentage == request.Percentage && o.Id != id))
                return BadRequest(new { success = false, message = "אחוז השתתפות זה כבר קיים" });

            entity.Percentage = request.Percentage;
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;

            await _shared.SaveChangesAsync();
            return Ok(new { success = true, message = "אחוז השתתפות עודכן בהצלחה" });
        }
    }
}
