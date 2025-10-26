// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : BaseController
    {
        private readonly AppDbContext _context;
      //  private readonly ILogger<EntitiesController> _logger;

   
        public EntitiesController(
            AppDbContext context,
            UserSessionService userSessionService,  
            ILogger<EntitiesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
          //  _logger = logger;
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

                _logger.LogInformation("Loaded {Count} entities for login dropdown", entities.Count);
                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entities for login");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים", error = ex.Message });
            }
        }

        [HttpGet("schools")]
        public async Task<IActionResult> GetSchools([FromQuery] int? yearId = null)
        {
            try
            {
                // ✅ CORRECT: Get session from BaseController helper
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for schools request");
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
                    return BadRequest(new { success = false, message = "Invalid session entity ID" });
                }

                _logger.LogInformation("Loading schools for entity {EntityId} (User: {UserId}) with year filter: {YearId}",
                    sessionEntityId, session.UserId, yearId);

                var query = _context.Entities
                    .Include(e => e.EntityType)
                    .AsNoTracking()
                    .Where(e => e.IsActive && e.OwnerId == sessionEntityId);

                // ✅ Filter by school_years.year_id if yearId is provided
                if (yearId.HasValue)
                {
                    query = query.Where(e => _context.SchoolYears
                        .Any(sy => sy.SchoolId == e.Id && sy.YearId == yearId.Value));
                }

                var schools = await query
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

                _logger.LogInformation("Loaded {Count} schools for entity {EntityId} with year filter: {YearId}", 
                    schools.Count, sessionEntityId, yearId);
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