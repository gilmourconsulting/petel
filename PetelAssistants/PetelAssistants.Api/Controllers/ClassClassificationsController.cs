using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/class-classifications")]
    public class ClassClassificationsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public ClassClassificationsController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<ClassClassificationsController> logger)
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

            var query = _sharedContext.ClassClassifications.AsNoTracking();
            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            var items = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.ForeignId, c.SortOrder, c.IsActive })
                .ToListAsync();

            var data = items.Select(c => new ClassClassificationDto
            {
                Id = c.Id,
                Name = $"{c.Id} - {c.Name}",
                ForeignId = c.ForeignId,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive
            }).ToList();

            return Ok(new { success = true, data });
        }
    }
}
