// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : ControllerBase // Changed from BaseController since we're dropping tenant requirements
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EntitiesController> _logger;

        public EntitiesController(
            AppDbContext context,
            ILogger<EntitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active entities for the login dropdown
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> GetEntitiesForLogin()
        {
            try
            {
                // Load entities with id, name, and entity_type_id
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

                _logger.LogInformation("Loaded {Count} entities for login from database", entities.Count);
                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entities for login");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים", error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific entity by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntity(int id)
        {
            try
            {
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
                _logger.LogError(ex, "Error loading entity with ID {EntityId}", id);
                return StatusCode(500, new { message = "שגיאה בטעינת פרטי הגוף", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all schools
        /// </summary>
        [HttpGet("schools")]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                _logger.LogInformation("Loading all schools");

                // Load schools with eager loading for EntityType
                var schoolsQuery = _context.Entities
                    .Include(e => e.EntityType)
                    .AsNoTracking() // For better performance in read-only scenarios
                    .Where(e => e.IsActive);

                _logger.LogDebug("Executing schools query");

                // Execute query and project to anonymous type
                var schools = await schoolsQuery
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
                        ownerId = e.OwnerId, // Kept for backward compatibility
                        entityTypeId = e.EntityTypeId,
                        entityTypeName = e.EntityType.Name,
                        isActive = e.IsActive
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} schools", schools.Count);
                return Ok(schools);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error loading schools");
                return StatusCode(500, new { 
                    message = "שגיאה בטעינת רשימת בתי הספר - בעיה בבסיס הנתונים", 
                    error = dbEx.InnerException?.Message ?? dbEx.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schools");
                return StatusCode(500, new { 
                    message = "שגיאה בטעינת רשימת בתי הספר", 
                    error = ex.Message 
                });
            }
        }
    }
}