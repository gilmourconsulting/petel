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
                        CharacterizationId = e.CharacterizationId,
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

/// <summary>
/// Create a new school entity with all related records (entity, school_year, school)
/// </summary>
[HttpPost("create-school")]
public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolDto dto)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized(new { success = false, message = "Authentication required" });
        }

        if (!int.TryParse(session.EntityId, out int sessionEntityId))
        {
            return BadRequest(new { success = false, message = "Invalid session entity ID" });
        }

        if (!int.TryParse(session.UserId, out int userId))
        {
            return BadRequest(new { success = false, message = "Invalid user ID" });
        }

        _logger.LogInformation("Creating new school: {Name} for owner {OwnerId}", dto.Name, dto.OwnerId);

        // Verify owner exists
        var ownerEntity = await _context.Entities.FindAsync(dto.OwnerId);
        if (ownerEntity == null)
        {
            return BadRequest(new { success = false, message = "גוף בעלים לא נמצא" });
        }

        // Get selected year details from session
        var selectedYearId = session.GetProperty("SelectedYearId");
        var selectedYearValue = session.GetProperty("SelectedYearValue");

        if (string.IsNullOrEmpty(selectedYearId) || string.IsNullOrEmpty(selectedYearValue))
        {
            return BadRequest(new { success = false, message = "לא נבחרה שנת לימודים" });
        }

        if (!int.TryParse(selectedYearId, out int yearId))
        {
            return BadRequest(new { success = false, message = "Invalid year ID" });
        }

        // STEP 1: Create Entity record
        var newEntity = new Entity
        {
            Name = dto.Name,
            EntityTypeId = dto.EntityTypeId,
            OwnerId = dto.OwnerId,
            IsActive = true
        };

        _context.Entities.Add(newEntity);
        await _context.SaveChangesAsync(); // Save to get generated ID

        _logger.LogInformation("Created entity with ID {EntityId}", newEntity.Id);

        // STEP 2: Create SchoolYear record
        var newSchoolYear = new SchoolYear
        {
            SchoolId = newEntity.Id,
            YearId = yearId,
            YearName = selectedYearValue,
            IsCurrent = true,
            Status = 1,
            StartDate = DateTime.UtcNow, // TODO: Get actual dates from hebrew_years table
            EndDate = DateTime.UtcNow.AddYears(1)
        };

        _context.SchoolYears.Add(newSchoolYear);
        await _context.SaveChangesAsync(); // Save to get generated ID

        _logger.LogInformation("Created school year with ID {SchoolYearId}", newSchoolYear.Id);

        // STEP 3: Create School record (version 1)
        var newSchool = new School
        {
            EntityId = newEntity.Id,
            SchoolYearId = newSchoolYear.Id,
            EntityTypeId = dto.EntityTypeId,
            Name = dto.Name,
            Owner = dto.OwnerId,
            IsActive = true,
            IsLastVersion = true,
            Version = 1
        };

        _context.Schools.Add(newSchool);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created school record with ID {SchoolId}", newSchool.Id);

        await transaction.CommitAsync();

        return Ok(new
        {
            success = true,
            message = "בית הספר נוצר בהצלחה",
            data = new
            {
                entityId = newEntity.Id,
                schoolYearId = newSchoolYear.Id,
                schoolId = newSchool.Id,
                name = newEntity.Name
            }
        });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error creating school");
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה ביצירת בית הספר",
            error = ex.Message
        });
    }
}

        /// <summary>
        /// Get all entity types for dropdowns
        /// Used by school creation and entity management forms
        /// </summary>
        [HttpGet("entity-types")]
        public async Task<IActionResult> GetEntityTypes()
        {
            try
            {
                _logger.LogInformation("🔍 GetEntityTypes endpoint called");
                
                // Session check using BaseController method
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("⚠️ No session found for GetEntityTypes request");
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }
                
                _logger.LogInformation("✅ Session found: UserId={UserId}, EntityId={EntityId}", 
                    session.UserId, session.EntityId);
        
                var entityTypes = await _context.EntityTypes
                    .AsNoTracking()
                    .OrderBy(et => et.Name)
                    .Select(et => new
                    {
                        id = et.Id,
                        name = et.Name
                    })
                    .ToListAsync();
        
                _logger.LogInformation("✅ Loaded {Count} entity types", entityTypes.Count);
                
                return Ok(entityTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading entity types");
                return StatusCode(500, new 
                { 
                    success = false,
                    message = "שגיאה בטעינת סוגי גופים", 
                    error = ex.Message 
                });
            }
        }
        

    }
}