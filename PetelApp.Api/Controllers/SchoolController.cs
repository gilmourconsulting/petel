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

                /*   if (school.Owner != sessionEntityId)
                   {
                       _logger.LogWarning("Unauthorized access attempt");
                       return Forbid();
                   }*/

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
                    CouncilName = school.CouncilEntity?.Name
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

                // Check if user has permission to update this school
                if (!await CanUserUpdateSchool(sessionEntityId, currentSchool.EntityId, session.Roles ?? new List<int>()))
                {
                    _logger.LogWarning("Unauthorized update attempt - User entity {UserEntityId} cannot update school entity {SchoolEntityId}",
                        sessionEntityId, currentSchool.EntityId);
                    return Unauthorized(new { success = false, message = "אין הרשאה לעדכון בית ספר זה" });
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

        /// <summary>
        /// Check if user has permission to update a school/entity
        /// Rules:
        /// 1. User's entity is the same as the school's entity
        /// 2. User's entity owns the school (direct owner)
        /// 3. User's entity owns the school's owner (grandparent owner)
        /// 4. User is an admin (has admin role)
        /// </summary>
        private async Task<bool> CanUserUpdateSchool(int userEntityId, int schoolEntityId, List<int> userRoles)
        {
            // Check if user is admin (assuming role ID 1 is admin - adjust if needed)
            const int ADMIN_ROLE_ID = 1;
            if (userRoles.Contains(ADMIN_ROLE_ID))
            {
                _logger.LogDebug("User has admin role - access granted");
                return true;
            }

            // Rule 1: User's entity is the same as school's entity
            if (userEntityId == schoolEntityId)
            {
                _logger.LogDebug("User entity matches school entity - access granted");
                return true;
            }

            // Get the school's entity to check ownership hierarchy
            var schoolEntity = await _context.Entities
                .Include(e => e.Owner)
                .ThenInclude(o => o.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == schoolEntityId);

            if (schoolEntity == null)
            {
                _logger.LogWarning("School entity {EntityId} not found", schoolEntityId);
                return false;
            }

            // Rule 2: User's entity directly owns the school
            if (schoolEntity.OwnerId == userEntityId)
            {
                _logger.LogDebug("User entity owns school entity - access granted");
                return true;
            }

            // Rule 3: User's entity owns the school's owner (grandparent)
            if (schoolEntity.Owner?.OwnerId == userEntityId)
            {
                _logger.LogDebug("User entity owns school's owner - access granted");
                return true;
            }

            _logger.LogDebug("User does not have permission to update school");
            return false;
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