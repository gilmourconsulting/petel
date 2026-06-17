using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : BaseController
    {
        private readonly AppDbContext _context;

        public EntitiesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<EntitiesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("login")]
        public async Task<IActionResult> GetEntitiesForLogin()
        {
            try
            {
                var entities = await _context.Entities
                    .AsNoTracking()
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.Name)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entityTypeId = e.EntityTypeId
                    })
                    .ToListAsync();

                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entities for login");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים" });
            }
        }
    }
}
