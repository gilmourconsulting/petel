// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session; // Add this using statement

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserSessionService _userSessionService; // Add this field

        public EntitiesController(AppDbContext context, UserSessionService userSessionService) // Add parameter
        {
            _context = context;
            _userSessionService = userSessionService; // Assign the service
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
        /// Get all schools for a specific tenant following Multi-Tenant Request Flow
        /// </summary>
        [HttpGet("schools")]
        public async Task<IActionResult> GetSchoolsForTenant([FromQuery] int? tenantId = null)
        {
            try
            {
                // Get tenant ID from query parameter or session following Authentication & Session Management
                var currentTenantId = tenantId ?? _userSessionService.GetUserSession()?.TenantId;
                
                if (currentTenantId == null)
                {
                    return BadRequest(new { message = "מזהה גוף חינוכי חסר" });
                }

                // Load entities where owner equals current tenant ID following Database Conventions
                var schools = await _context.Entities
                    .Include(e => e.EntityType)
                    .Where(e => e.OwnerId == currentTenantId && e.IsActive)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        symbol = e.Symbol,
                        address = e.Address,
                        principalName = e.PrincipalName,
                        inspectorName = e.InspectorName,
                        characterization = e.Characterization,
                        contactPerson = e.ContactPerson,
                        educationStage = e.EducationStage,
                        ownerId = e.OwnerId,
                        entityTypeId = e.EntityTypeId,
                        entityTypeName = e.EntityType.Name,
                        isActive = e.IsActive
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                Console.WriteLine($"Loaded {schools.Count} schools from database for tenant {currentTenantId}");
                return Ok(schools);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading schools from database: {ex.Message}");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת בתי הספר מהמסד נתונים", error = ex.Message });
            }
        }
    }
}