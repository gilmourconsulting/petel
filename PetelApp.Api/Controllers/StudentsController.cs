using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(
            AppDbContext context,
            ITenantService tenantService,
            ILogger<StudentsController> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
        }

        [HttpGet("registration-summary")]
        public async Task<ActionResult<IEnumerable<StudentRegistrationSummaryDto>>> GetRegistrationSummary([FromQuery] int? schoolYearId = null)
        {
            try
            {
                // Multi-tenant pattern: get school ID (tenant) from TenantMiddleware context
                var tenantIdString = _tenantService.GetCurrentTenantId();
                if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out int schoolId))
                {
                    return Unauthorized("No valid school context found");
                }

                // Validate school exists and is active
                if (!await _tenantService.TenantExistsAsync(schoolId))
                {
                    return Unauthorized("Invalid school");
                }

                if (!schoolYearId.HasValue)
                {
                    return BadRequest("School year ID is required");
                }

                // Validate that the school year belongs to this school (tenant)
                var schoolYear = await _context.SchoolYears
                    .Where(sy => sy.Id == schoolYearId.Value && sy.SchoolId == schoolId)
                    .FirstOrDefaultAsync();

                if (schoolYear == null)
                {
                    return NotFound("School year not found for this school");
                }

                // Query the view with school (tenant) filtering
                var registrationSummary = await _context.StudentSchoolYearsRegistrationSummaryVw
                    .Where(s => s.SchoolId == schoolId && s.SchoolYearId == schoolYearId.Value)
                    .Select(s => new StudentRegistrationSummaryDto
                    {
                        SchoolGrade = s.SchoolGrade,
                        SchoolTrack = s.SchoolTrack,
                        Registered = s.Registered
                    })
                    .OrderBy(s => s.SchoolGrade)
                    .ThenBy(s => s.SchoolTrack)
                    .ToListAsync();

                // Log with school context following coding guide patterns
                _logger.LogInformation("Registration summary requested for school {SchoolId}, school year {SchoolYearId}, returned {Count} records", 
                    schoolId, schoolYearId, registrationSummary.Count);

                return Ok(registrationSummary);
            }
            catch (Exception ex)
            {
                var schoolId = _tenantService.GetCurrentTenantId();
                _logger.LogError(ex, "Error getting registration summary for school {SchoolId}, school year {SchoolYearId}", 
                    schoolId, schoolYearId);
                return StatusCode(500, "An error occurred while retrieving registration summary");
            }
        }
    }
}