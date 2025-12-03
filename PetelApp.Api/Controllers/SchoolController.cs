using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;
using PetelApp.Api.Services;


namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Controller for managing school details and information
    /// Handles retrieval of school data with versioning support
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get school details by school year ID
        /// Returns the latest version (is_last_version = true) for the specified school year
        /// </summary>
        /// <param name="schoolYearId">School year ID from school_years table</param>
        /// <returns>School details with formatted address and person names</returns>
    
    
 [HttpGet("by-year/{schoolYearId}")]
public async Task<IActionResult> GetSchoolByYear(int schoolYearId)
{
    try
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            _logger.LogWarning("No valid session found for school details request");
            return Unauthorized(new { success = false, message = "Authentication required" });
        }

        if (!int.TryParse(session.EntityId, out int sessionEntityId))
        {
            _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
            return BadRequest(new { success = false, message = "Invalid session entity ID" });
        }

        _logger.LogInformation("Loading school details for school year {SchoolYearId}", schoolYearId);

        // ✅ Query with IsLastVersion = true to get current version
        var school = await _context.Schools
            .Include(s => s.PrincipalPerson)
            .Include(s => s.InspectorPerson)
            .Include(s => s.ContactPersonPerson)
            .Include(s => s.CouncilEntity)
            .Include(s => s.Characterization)
            .AsNoTracking()
            .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion) // ✅ Only last version
            .FirstOrDefaultAsync();

        if (school == null)
        {
            _logger.LogWarning("School not found for school year {SchoolYearId}", schoolYearId);
            return NotFound(new
            {
                success = false,
                message = "לא נמצאו פרטי בית ספר עבור שנת הלימודים המבוקשת"
            });
        }

        if (school.Owner != sessionEntityId)
        {
            _logger.LogWarning("Unauthorized access attempt");
            return Forbid();
        }

        // Build DTO
        var schoolDetails = new SchoolDetailsDto
        {
            Id = school.Id,
            EntityId = school.EntityId,
            SchoolYearId = school.SchoolYearId,
            Version = school.Version, // ✅ Include version number
            Name = school.Name,
            
            Address = FormatAddress(school.Street, school.HouseNumber, school.City, school.PostCode),
            Street = school.Street,
            HouseNumber = school.HouseNumber,
            City = school.City,
            PostCode = school.PostCode,
            
            Phone = school.Phone,
            Email = school.Email,
            
            PrincipalId = school.Principal,
            PrincipalName = GlobalFunctions.FormatPersonName(school.PrincipalPerson),
            
            InspectorId = school.Inspector,
            InspectorName = GlobalFunctions.FormatPersonName(school.InspectorPerson),
            
            ContactPersonId = school.ContactPerson,
            ContactPersonName = GlobalFunctions.FormatPersonName(school.ContactPersonPerson),
            
            CharacterizationId = school.CharacterizationId,
            CharacterizationName = school.Characterization?.Name,
            EducationStage = school.EducationStage,
            Symbol = school.Symbol,
            IsActive = school.IsActive,
            CouncilId = school.Council,
            CouncilName = school.CouncilEntity?.CouncilShortName
        };

        return Ok(new { success = true, data = schoolDetails });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading school details for school year {SchoolYearId}", schoolYearId);
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה בטעינת פרטי בית הספר",
            error = ex.Message
        });
    }
}

        /// <summary>
        /// Format address from components, excluding post code if null or all zeros
        /// </summary>
        private string FormatAddress(string? street, string? houseNumber, string? city, string? postCode)
        {
            var parts = new List<string>();

            // Street and house number
            if (!string.IsNullOrWhiteSpace(street))
            {
                var streetPart = street.Trim();
                if (!string.IsNullOrWhiteSpace(houseNumber))
                {
                    streetPart += " " + houseNumber.Trim();
                }
                parts.Add(streetPart);
            }

            // City
            if (!string.IsNullOrWhiteSpace(city))
            {
                parts.Add(city.Trim());
            }

            // Post code (only if not null and not all zeros)
            if (!string.IsNullOrWhiteSpace(postCode) && !IsAllZeros(postCode))
            {
                parts.Add(postCode.Trim());
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Check if string is all zeros
        /// </summary>
        private bool IsAllZeros(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim().All(c => c == '0');
        }


        /// <summary>
        /// Get all special needs characterizations
        /// </summary>
        [HttpGet("characterizations")]
        public async Task<IActionResult> GetCharacterizations()
        {
            try
            {
                var characterizations = await _context.SpecialNeedsCharacterizations
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = characterizations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading characterizations");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת אפיונים"
                });
            }
        }

 // <summary>
/// Update school details
/// </summary>
[HttpPut("update-details")]
public async Task<IActionResult> UpdateSchoolDetails([FromBody] UpdateSchoolDetailsDto dto)
{
    try
    {
        var session = GetCurrentSession();
        
        if (session == null)
        {
            _logger.LogWarning("No valid session found for school update request");
            return Unauthorized(new { success = false, message = "Authentication required" });
        }

        if (!int.TryParse(session.EntityId, out int sessionEntityId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid session entity ID"
            });
        }

        _logger.LogInformation("Updating school details for school year {SchoolYearId} with versioning", dto.SchoolYearId);

        // ✅ Get current last version record
        var currentSchool = await _context.Schools
            .Where(s => s.SchoolYearId == dto.SchoolYearId && s.IsLastVersion)
            .FirstOrDefaultAsync();

        if (currentSchool == null)
        {
            return NotFound(new
            {
                success = false,
                message = "לא נמצא רשומת בית ספר עבור שנת הלימודים"
            });
        }

        if (currentSchool.Owner != sessionEntityId)
        {
            _logger.LogWarning("Unauthorized update attempt");
            return Forbid();
        }

        // ✅ VERSIONING STEP 1: Mark current record as NOT last version
        currentSchool.IsLastVersion = false;


        // ✅ VERSIONING STEP 2: Create new record with incremented version
        var newSchool = new School
        {
            EntityId = currentSchool.EntityId,
            SchoolYearId = currentSchool.SchoolYearId,
            Version = currentSchool.Version + 1, // ✅ Increment version
            EntityTypeId = currentSchool.EntityTypeId,
            Name = currentSchool.Name,
            
            // Address fields - keep from dto
            Street = dto.Street,
            HouseNumber = dto.HouseNumber,
            City = dto.City,
            PostCode = dto.PostCode,

            // Contact fields - keep from dto
            Principal = dto.PrincipalId,
            Inspector = dto.InspectorId ?? currentSchool.Inspector,
            ContactPerson = dto.ContactPersonId,

            // ✅ Updated fields from DTO
            Symbol = dto.Symbol,
            CharacterizationId = dto.CharacterizationId,
            EducationStage = dto.EducationStage,
            Council = dto.CouncilId ?? currentSchool.Council,
            Phone = dto.Phone,
            Email = dto.Email,
            
            // Keep other fields from current record
            ApiConnectionId = currentSchool.ApiConnectionId,
            SchoolLogo = currentSchool.SchoolLogo,
            Owner = currentSchool.Owner,
            
            // ✅ Mark as last version
            IsLastVersion = true,
            IsActive = currentSchool.IsActive
        };

        // ✅ Add new version to context
        _context.Schools.Add(newSchool);
        
        // ✅ Save both changes (update + insert)
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "School details updated successfully. Old version {OldVersion} marked as not last, new version {NewVersion} created",
            currentSchool.Version, 
            newSchool.Version
        );

        return Ok(new
        {
            success = true,
            message = "פרטי בית הספר עודכנו בהצלחה",
            data = new
            {
                oldVersion = currentSchool.Version,
                newVersion = newSchool.Version,
                newRecordId = newSchool.Id
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating school details");
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה בעדכון פרטי בית הספר",
            error = ex.Message
        });
    }
}

        // Add DTO class
    /*    public class UpdateSchoolDetailsDto
        {
            public int SchoolYearId { get; set; }
            public string Symbol { get; set; } = string.Empty;
            public int? CharacterizationId { get; set; }
            public string? EducationStage { get; set; }
            public int? CouncilId { get; set; } 
            public string? Phone { get; set; }
            public string? Email { get; set; }
        }*/
    }
}