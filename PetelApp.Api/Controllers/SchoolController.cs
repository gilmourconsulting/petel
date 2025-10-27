using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;

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
                // ✅ Get session from BaseController helper
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

                _logger.LogInformation("Loading school details for school year {SchoolYearId} (User: {UserId}, Entity: {EntityId})",
                    schoolYearId, session.UserId, sessionEntityId);

                // Query school with navigation properties
                var school = await _context.Schools
                    .Include(s => s.PrincipalPerson)
                    .Include(s => s.InspectorPerson)
                    .Include(s => s.ContactPersonPerson)
                    .Include(s => s.CouncilEntity)
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion)
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

                // ✅ Security check: Verify school belongs to user's entity
                if (school.Owner != sessionEntityId)
                {
                    _logger.LogWarning("Unauthorized access attempt: User entity {UserEntity} tried to access school entity {SchoolEntity}",
                        sessionEntityId, school.EntityId);
                    return Forbid();
                }

                // Build DTO with formatted data
                var schoolDetails = new SchoolDetailsDto
                {
                    Id = school.Id,
                    EntityId = school.EntityId,
                    SchoolYearId = school.SchoolYearId,
                    Version = school.Version,
                    Name = school.Name,
                    Address = FormatAddress(school.Street, school.HouseNumber, school.City, school.PostCode),
                    Phone = school.Phone,
                    Email = school.Email,
                    PrincipalName = FormatPersonName(school.PrincipalPerson),
                    InspectorName = FormatPersonName(school.InspectorPerson),
                    ContactPersonName = FormatPersonName(school.ContactPersonPerson),
                    Characterization = school.Characterization,
                    EducationStage = school.EducationStage,
                    Symbol = school.Symbol,
                    IsActive = school.IsActive,
                    CouncilName = school.CouncilEntity?.CouncilShortName,
                    CreatedAt = school.CreatedAt,
                    UpdatedAt = school.UpdatedAt
                };

                _logger.LogInformation("Successfully loaded school details for school year {SchoolYearId}: {SchoolName}",
                    schoolYearId, school.Name);

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
        /// Format person name from Person entity
        /// </summary>
        private string FormatPersonName(Person? person)
        {
            if (person == null)
            {
                return string.Empty;
            }

            var firstName = person.FirstName?.Trim() ?? string.Empty;
            var lastName = person.LastName?.Trim() ?? string.Empty;

            return $"{firstName} {lastName}".Trim();
        }
    }
}