using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

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
        public async Task<IActionResult> GetAll()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var options = await _shared.MinistryParticipationOptions
                .AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new MinistryParticipationOptionDto
                {
                    Id = o.Id,
                    Percentage = o.Percentage,
                    DisplayOrder = o.DisplayOrder
                })
                .ToListAsync();

            return Ok(new { success = true, data = options });
        }
    }
}
