using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolYearsController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolYearsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolYearsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get school_years.id by year_id and school_id
        /// Used to resolve SelectedSchoolYearId from SelectedYearId + SelectedSchoolId
        /// </summary>
        [HttpGet("by-year-and-school")]
        public async Task<IActionResult> GetSchoolYearByYearAndSchool(
            [FromQuery] int yearId, 
            [FromQuery] int schoolId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for school year lookup");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation(
                    "Looking up school_year for yearId={YearId}, schoolId={SchoolId}", 
                    yearId, schoolId);

                var schoolYear = await _context.SchoolYears
                    .AsNoTracking()
                    .Where(sy => sy.YearId == yearId && sy.SchoolId == schoolId)
                    .Select(sy => new { sy.Id })
                    .FirstOrDefaultAsync();

                if (schoolYear == null)
                {
                    _logger.LogWarning(
                        "No school_year found for yearId={YearId}, schoolId={SchoolId}", 
                        yearId, schoolId);
                    // Return 200 OK with null id instead of 404
                    // This allows the client to handle gracefully when school year doesn't exist yet
                    return Ok(new 
                    { 
                        id = (int?)null,
                        success = false, 
                        message = "לא נמצאה שנת לימודים עבור בית הספר והשנה הנבחרים" 
                    });
                }

                _logger.LogInformation(
                    "Found school_year id={Id} for yearId={YearId}, schoolId={SchoolId}", 
                    schoolYear.Id, yearId, schoolId);

                return Ok(new { id = schoolYear.Id, success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error looking up school_year for yearId={YearId}, schoolId={SchoolId}", 
                    yearId, schoolId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בחיפוש שנת לימודים",
                    error = ex.Message
                });
            }
        }
    }
}