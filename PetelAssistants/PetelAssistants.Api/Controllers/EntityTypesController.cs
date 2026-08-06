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
    [Route("api/entity-types")]
    public class EntityTypesController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public EntityTypesController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<EntityTypesController> logger)
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

            var query = _sharedContext.EntityTypes.AsNoTracking();
            if (!includeInactive)
                query = query.Where(t => t.IsActive);

            var types = await query
                .OrderBy(t => t.Name)
                .Select(t => new EntityTypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = types });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEntityTypeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "שם הוא שדה חובה" });

            var name = request.Name.Trim();
            if (await _sharedContext.EntityTypes.AnyAsync(t => t.Name == name))
                return BadRequest(new { success = false, message = "סוג רשות עם שם זה כבר קיים" });

            var entity = new EntityType
            {
                Name = name,
                Description = request.Description?.Trim(),
                IsActive = true
            };

            _sharedContext.EntityTypes.Add(entity);
            await _sharedContext.SaveChangesAsync();

            return Ok(new { success = true, message = "סוג רשות נוצר בהצלחה", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEntityTypeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "שם הוא שדה חובה" });

            var entity = await _sharedContext.EntityTypes.FindAsync(id);
            if (entity == null)
                return NotFound(new { success = false, message = "סוג רשות לא נמצא" });

            var name = request.Name.Trim();
            if (await _sharedContext.EntityTypes.AnyAsync(t => t.Name == name && t.Id != id))
                return BadRequest(new { success = false, message = "סוג רשות עם שם זה כבר קיים" });

            entity.Name = name;
            entity.Description = request.Description?.Trim();
            entity.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "סוג רשות עודכן בהצלחה" });
        }
    }
}
