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
        // ✅ Get session from BaseController helper
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

        // ✅ Get SelectedYearId from session if not provided in query
        if (!yearId.HasValue)
        {
            var selectedYearIdStr = session.GetProperty("SelectedYearId");
            if (!string.IsNullOrEmpty(selectedYearIdStr) && int.TryParse(selectedYearIdStr, out int selectedYearId))
            {
                yearId = selectedYearId;
            }
        }

        if (!yearId.HasValue)
        {
            _logger.LogError("No year ID provided or found in session");
            return BadRequest(new { success = false, message = "Year ID required" });
        }

        _logger.LogInformation("Loading schools from schools table for owner {EntityId} and year {YearId}",
            sessionEntityId, yearId.Value);

         // ✅ STEP 1: Get all school_year IDs for the selected Hebrew year
        var schoolYearIds = await _context.SchoolYears
            .AsNoTracking()
            .Where(sy => sy.YearId == yearId.Value)
            .Select(sy => sy.Id)
            .ToListAsync();

        _logger.LogInformation("Found {Count} school years for year ID {YearId}", 
            schoolYearIds.Count, yearId.Value);

        if (!schoolYearIds.Any())
        {
            _logger.LogWarning("No school years found for year ID {YearId}", yearId.Value);
            return Ok(new List<SchoolDto>()); // Return empty list
        }

        // ✅ STEP 2: Query schools table - filter by school_year IDs, owner, and is_last_version
        var schoolsQuery = await _context.Schools
            .AsNoTracking()
            .Where(s => schoolYearIds.Contains(s.SchoolYearId) &&
                       s.Owner == sessionEntityId && 
                       s.IsLastVersion &&
                       s.IsActive)
            .Select(s => new 
            {
                s.EntityId,
                s.Name,
                s.Symbol,
                s.Street,
                s.HouseNumber,
                s.City,
                s.PostCode,
                PrincipalFirstName = s.PrincipalPerson != null ? s.PrincipalPerson.FirstName : null,
                PrincipalLastName = s.PrincipalPerson != null ? s.PrincipalPerson.LastName : null,
                InspectorFirstName = s.InspectorPerson != null ? s.InspectorPerson.FirstName : null,
                InspectorLastName = s.InspectorPerson != null ? s.InspectorPerson.LastName : null,
                ContactFirstName = s.ContactPersonPerson != null ? s.ContactPersonPerson.FirstName : null,
                ContactLastName = s.ContactPersonPerson != null ? s.ContactPersonPerson.LastName : null,
                CharacterizationName = s.Characterization != null ? s.Characterization.Name : null,
                s.EducationStage,
                s.IsActive,
                s.SchoolYearId
            })
            .OrderBy(s => s.Name)
            .ToListAsync();

        // ✅ Format data in memory (after database query)
        var schools = schoolsQuery.Select(s => new SchoolDto
        {
            Id = s.EntityId,
            Name = s.Name ?? string.Empty,
            Symbol = s.Symbol,
            Address = FormatSchoolAddress(s.Street, s.HouseNumber, s.City, s.PostCode),
            PrincipalName = FormatPersonName(s.PrincipalFirstName, s.PrincipalLastName),
            InspectorName = FormatPersonName(s.InspectorFirstName, s.InspectorLastName),
            ContactPerson = FormatPersonName(s.ContactFirstName, s.ContactLastName),
            CharacterizationName = s.CharacterizationName,
            EducationStage = s.EducationStage,
            IsActive = s.IsActive,
            SchoolYearId = s.SchoolYearId
        }).ToList();

        _logger.LogInformation("Loaded {Count} schools from schools table for owner {EntityId} and year {YearId}", 
            schools.Count, sessionEntityId, yearId.Value);
        
        return Ok(schools);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading schools from schools table");
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה בטעינת רשימת בתי הספר",
            error = ex.Message
        });
    }
}


// ✅ Helper method to format address
private static string FormatSchoolAddress(string? street, string? houseNumber, string? city, string? postCode)
{
    var parts = new List<string>();

    if (!string.IsNullOrWhiteSpace(street))
    {
        var streetPart = street.Trim();
        if (!string.IsNullOrWhiteSpace(houseNumber))
        {
            streetPart += " " + houseNumber.Trim();
        }
        parts.Add(streetPart);
    }

    if (!string.IsNullOrWhiteSpace(city))
    {
        parts.Add(city.Trim());
    }

    if (!string.IsNullOrWhiteSpace(postCode) && !IsAllZeros(postCode))
    {
        parts.Add(postCode.Trim());
    }

    return string.Join(", ", parts);
}

// ✅ Helper method to format person name
private static string FormatPersonName(string? firstName, string? lastName)
{
    var first = firstName?.Trim() ?? string.Empty;
    var last = lastName?.Trim() ?? string.Empty;

    return $"{first} {last}".Trim();
}

// ✅ Helper method to check if string is all zeros
private static bool IsAllZeros(string value)
{
    return !string.IsNullOrWhiteSpace(value) && value.Trim().All(c => c == '0');
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