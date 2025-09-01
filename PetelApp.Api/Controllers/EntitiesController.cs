// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EntitiesController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all active entities for the login dropdown
        /// This endpoint is public and doesn't require tenant context
        /// since users need to see available entities before selecting one
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> GetEntitiesForLogin()
        {
            try
            {
                // Load entities with id, name, and entity_type_id following authentication & session management
                var entities = await _context.Entities
                    .Where(e => e.IsActive)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entity_type_id = e.EntityTypeId
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                Console.WriteLine($"Loaded {entities.Count} entities for login from database");
                return Ok(entities);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading entities: {ex.Message}");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים", error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific entity by ID
        /// Requires tenant context for security
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntity(int id)
        {
            try
            {
                // Full entity details for post-login operations
                var entity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (entity == null)
                {
                    return NotFound(new { message = "גוף לא נמצא" });
                }

                return Ok(entity);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת פרטי הגוף", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all schools
        /// This endpoint provides a list of schools with additional details
        /// </summary>
        [HttpGet("schools")]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                // For school list page - load schools with additional details following multi-tenant request flow
                var schools = await _context.Entities
                    .Where(e => e.EntityTypeId != 5) // Exclude multi-school networks
                    .Select(e => new
                    {
                        id = e.Id,
                        schoolName = e.Name,
                        institutionSymbol = e.Symbol ,
                        address = e.Address ?? "לא זמין",
                        principalName = e.PrincipalName ?? "לא מוגדר",
                        inspectorName = e.inspector_name ?? "לא מוגדר",
                        institutionCharacterization = e.characterization ?? "לא מוגדר",
                        contactPerson = e.contact_person ?? "לא מוגדר",
                        educationStage = e.education_stage ?? "לא מוגדר",
                        phone = e.Phone,
                        email = e.Email,

                        isActive = e.IsActive
                    })
                    .OrderBy(e => e.schoolName)
                    .ToListAsync();

                return Ok(schools);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת בתי הספר", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all entities
        /// This endpoint provides a full list of entities with detailed information
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEntities()
        {
            try
            {
                // Full entity list with details following multi-tenant request flow
                var entities = await _context.Entities
                    .Include(e => e.EntityType)
                    .Where(e => e.IsActive)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entity_type_id = e.EntityTypeId,
                        entity_type_name = e.EntityType.Name,
                        address = e.Address,
                        phone = e.Phone,
                        email = e.Email,
                        principal_name = e.PrincipalName,
                        is_active = e.IsActive
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                return Ok(entities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת הגופים", error = ex.Message });
            }
        }
    }
}