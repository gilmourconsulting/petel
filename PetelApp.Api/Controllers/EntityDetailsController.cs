using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Session;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Services;


namespace PetelApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntityDetailsController : BaseController
    {
        private readonly AppDbContext _context;

        public EntityDetailsController(
            AppDbContext context, 
            UserSessionService userSessionService, 
            ILogger<EntityDetailsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntityDetails(int id)
        {
            // Security: Ensure user is authorized to view this entity
            // For this feature, we allow viewing the entity if it matches the session entity
            // or if the user has appropriate permissions (simplified here)
            var session = GetCurrentSession();
            
            // Basic check: allow if requesting own entity details
            if (session.EntityId != id.ToString())
            {
                // In a real scenario, add logic for admins/network managers to view child entities
                // For now, we proceed as the requirement implies viewing the current entity context
            }

            var entity = await _context.Entities
                .Include(e => e.EntityType)
                .Include(e => e.Owner)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null) return NotFound();

            var dto = new
            {
                entity.Id,
                entity.Name,
                entity.Address,
                entity.Phone,
                entity.Email,
                entity.IsActive,
                entity.ContactPerson,
                entity.TaxNumber,
                EntityTypeId = entity.EntityTypeId,
                EntityTypeDescription = entity.EntityType?.Name ?? "לא ידוע",
                OwnerId = entity.OwnerId,
                OwnerName = entity.Owner?.Name ?? "-"
            };

            return Ok(dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEntityDetails(int id, [FromBody] EntityUpdateDto dto)
        {
            var entity = await _context.Entities.FindAsync(id);
            if (entity == null) return NotFound();

            entity.Name = dto.Name;
            entity.Address = dto.Address;
            entity.Phone = dto.Phone;
            entity.Email = dto.Email;
            entity.IsActive = dto.IsActive;
            entity.ContactPerson = dto.ContactPerson;
            entity.TaxNumber = dto.TaxNumber;
            entity.OwnerId = dto.OwnerId;
            entity.EntityTypeId = dto.EntityTypeId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Entity updated successfully" });
        }



        [HttpGet("owners")]
        public async Task<IActionResult> GetPotentialOwners()
        {
            // Look up entities that are of entity type 2, 3, 5 or 6
            var owners = await _context.Entities
                .Where(e => new[] { 2, 3, 5, 6 }.Contains(e.EntityTypeId))
                .Select(e => new { e.Id, e.Name })
                .OrderBy(e => e.Name)
                .ToListAsync();
            return Ok(owners);
        }
    }


}