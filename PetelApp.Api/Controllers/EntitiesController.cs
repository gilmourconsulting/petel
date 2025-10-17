// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class EntitiesController : BaseController
    {
        private readonly AppDbContext _context;

        public EntitiesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<BaseController> baseLogger)
            : base(userSessionService, baseLogger)
        {
            _context = context;
        }

        /// <summary>
        /// Get all active entities for the login dropdown - NO session filtering
        /// Used by login.html to populate entity selection before authentication
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> GetEntitiesForLogin()
        {
            try
            {
                // NO session filtering - this is called BEFORE login
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

                _logger.LogInformation("Loaded {Count} entities for login dropdown from database", entities.Count);
                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entities for login");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים", error = ex.Message });
            }
        }

        /// <summary>
        /// Get schools owned by current user's session entity following Entity-Based Request Flow
        /// Used by schoollist.html after authentication
        /// </summary>
        [HttpGet("schools")]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                // Get current session following Authentication & Session Management
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for schools request");
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }

                // Convert EntityId from session (string) to int following Entity-Based Request Flow
                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
                    return BadRequest(new { success = false, message = "Invalid session entity ID" });
                }

                _logger.LogInformation("Loading schools for session entity ID: {EntityId} (User: {UserId})",
                    sessionEntityId, session.UserId);

                // Query schools owned by the session entity following Entity-Based Request Flow

 
                var schoolsQuery = _context.Entities
                    .Include(e => e.EntityType)
                    .AsNoTracking()
                    .Where(e => e.IsActive && e.OwnerId == sessionEntityId);

                _logger.LogDebug("Executing schools query for owner: {OwnerId}", sessionEntityId);

                var schools = await schoolsQuery
                    .Select(e => new SchoolDto
                    {
                        Id = e.Id,
                        Name = e.Name ?? string.Empty,
                        Symbol = e.Symbol,
                        Address = e.Address,
                        PrincipalName = e.PrincipalName,
                        InspectorName = e.InspectorName,
                        Characterization = e.Characterization,
                        ContactPerson = e.ContactPerson,
                        EducationStage = e.EducationStage,
                        OwnerId = e.OwnerId,
                        EntityTypeId = e.EntityTypeId,
                        EntityTypeName = e.EntityType != null ? e.EntityType.Name : "Unknown",
                        IsActive = e.IsActive
                    })
                    .OrderBy(e => e.Name)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} schools for entity {EntityId}", schools.Count, sessionEntityId);
                return Ok(schools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schools");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשימת בתי הספר",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get specific entity by ID (with session validation)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntity(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "Authentication required" });
                }

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
    }
}