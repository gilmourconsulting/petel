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
        public async Task<IActionResult> GetSchools([FromQuery] int? entityId = null)
        {
            try
            {
                // Get current session following Authentication & Session Management
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("No valid authorization header found");
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }

                var sessionToken = authHeader.Substring("Bearer ".Length).Trim();
                
                // For now, use the entityId from query parameter if provided
                // In a full implementation, you would validate the session token and get the user's entity ID
                var filterEntityId = entityId;
                
                if (!filterEntityId.HasValue)
                {
                    _logger.LogWarning("No entity ID provided for schools filter");
                    return BadRequest(new { success = false, message = "Entity ID is required" });
                }

                _logger.LogInformation("Loading schools for entity ID: {EntityId}", filterEntityId);

                // Query schools where OwnerId equals the user's entity ID following Entity-Based Request Flow
                var schoolsQuery = _context.Entities
                    .Include(e => e.EntityType)
                    .AsNoTracking()
                    .Where(e => e.IsActive && e.OwnerId == filterEntityId.Value); // Filter by owner entity ID

                _logger.LogDebug("Executing filtered schools query for owner: {OwnerId}", filterEntityId);

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
                        ownerId = e.OwnerId,
                        entityTypeId = e.EntityTypeId,
                        entityTypeName = e.EntityType != null ? e.EntityType.Name : "Unknown",
                        isActive = e.IsActive
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} schools for owner entity {EntityId}", schools.Count, filterEntityId);
                return Ok(schools);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error loading schools");
                return StatusCode(500, new { 
                    success = false,
                    message = "שגיאה בטעינת רשימת בתי הספר - בעיה בבסיס הנתונים", 
                    error = dbEx.InnerException?.Message ?? dbEx.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schools");
                return StatusCode(500, new { 
                    success = false,
                    message = "שגיאה בטעינת רשימת בתי הספר", 
                    error = ex.Message 
                });
            }
        }
    }
}