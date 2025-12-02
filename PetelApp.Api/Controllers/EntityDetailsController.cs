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
            var session = GetCurrentSession();
            if (session == null) return Unauthorized("Session not found.");
            
            if (session.EntityId != id.ToString())
            {
                // Authorization logic here
            }

            var entity = await _context.Entities
                .Include(e => e.EntityType)
                .Include(e => e.Owner)
                .Include(e => e.ContactPerson)  // Person entity
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null) return NotFound();

            // ✅ Build contact person name from Person entity (like SchoolController pattern)
            string? contactPersonName = null;
            if (entity.ContactPerson != null)
            {
                contactPersonName = GlobalFunctions.FormatPersonName(entity.ContactPerson);
                if (string.IsNullOrWhiteSpace(contactPersonName))
                {
                    contactPersonName = null;
                }
            }

            var dto = new
            {
                entity.Id,
                entity.Name,
                entity.Address,
                entity.Street,
                entity.HouseNumber,
                entity.City,
                entity.PostCode,
                entity.Phone,
                entity.Email,
                entity.IsActive,
                entity.TaxNumber,
                EntityTypeId = entity.EntityTypeId,
                EntityTypeDescription = entity.EntityType?.Name ?? "לא ידוע",
                OwnerId = entity.OwnerId,
                OwnerName = entity.Owner?.Name ?? "-",
                // ✅ Return person ID and formatted name only (like SchoolController)
                ContactPersonId = entity.ContactPersonId,
                ContactPersonName = contactPersonName  // Combined first + last name
            };

            return Ok(dto);
        }

  
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEntityDetails(int id, [FromBody] EntityUpdateDto dto)
        {
            var session = GetCurrentSession();
            if (session == null) return Unauthorized("Session not found.");

            var entity = await _context.Entities.FindAsync(id);
            if (entity == null) return NotFound();

            // Update basic fields
            entity.Name = dto.Name;
            entity.EntityTypeId = dto.EntityTypeId;
            entity.OwnerId = dto.OwnerId;
            entity.IsActive = dto.IsActive;
            
            // Update optional fields
            entity.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone;
            entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email;
            entity.TaxNumber = string.IsNullOrWhiteSpace(dto.TaxNumber) ? null : dto.TaxNumber;
            
            // ✅ Update contact person ID only (not name)
            entity.ContactPersonId = dto.ContactPersonId;  // Can be null
            
            // ✅ Build formatted address from components
            if (!string.IsNullOrWhiteSpace(dto.Street) || !string.IsNullOrWhiteSpace(dto.City))
            {
                var addressParts = new List<string>();
                
                if (!string.IsNullOrWhiteSpace(dto.Street))
                {
                    var streetPart = dto.Street.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.HouseNumber))
                    {
                        streetPart += " " + dto.HouseNumber.Trim();
                    }
                    addressParts.Add(streetPart);
                }
                
                if (!string.IsNullOrWhiteSpace(dto.City))
                {
                    addressParts.Add(dto.City.Trim());
                }
                
                if (!string.IsNullOrWhiteSpace(dto.PostCode) && 
                    dto.PostCode != "0" && 
                    dto.PostCode != "0000000")
                {
                    addressParts.Add(dto.PostCode.Trim());
                }
                
                entity.Address = string.Join(", ", addressParts);
            }
            else
            {
                entity.Address = null;
            }
            
            // ✅ Store address components in separate fields if they exist
            entity.Street = string.IsNullOrWhiteSpace(dto.Street) ? null : dto.Street.Trim();
            entity.HouseNumber = string.IsNullOrWhiteSpace(dto.HouseNumber) ? null : dto.HouseNumber.Trim();
            entity.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
            entity.PostCode = string.IsNullOrWhiteSpace(dto.PostCode) ? null : dto.PostCode.Trim();

            await _context.SaveChangesAsync();
            
            return Ok(new { success = true, message = "Entity updated successfully" });
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