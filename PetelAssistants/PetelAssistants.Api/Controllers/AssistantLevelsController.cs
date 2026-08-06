using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/assistant-levels")]
    public class AssistantLevelsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public AssistantLevelsController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<AssistantLevelsController> logger)
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

            var query = _sharedContext.AssistantLevels.AsNoTracking();
            if (!includeInactive)
                query = query.Where(l => l.IsActive);

            var levels = await query
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.DisplayName)
                .Select(l => new AssistantLevelDto
                {
                    Id = l.Id,
                    Code = l.Code,
                    DisplayName = l.DisplayName,
                    SortOrder = l.SortOrder,
                    IsActive = l.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = levels });
        }
    }
}
