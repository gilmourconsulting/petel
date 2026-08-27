using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/statuses")]
    public class StatusesController : BaseController
    {
        private readonly SharedDbContext _shared;

        public StatusesController(
            SharedDbContext shared,
            UserSessionService sessionService,
            ILogger<StatusesController> logger)
            : base(sessionService, logger)
        {
            _shared = shared;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? @object)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var query = _shared.Statuses.AsNoTracking().Where(s => s.IsActive);
            if (!string.IsNullOrWhiteSpace(@object))
                query = query.Where(s => s.Object == @object);

            var items = await query
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Select(s => new StatusDto
                {
                    Id = s.Id,
                    Object = s.Object,
                    Code = s.Code,
                    Name = s.Name,
                    SortOrder = s.SortOrder,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }
    }
}
