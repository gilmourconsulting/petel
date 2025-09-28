using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Services;
using PetelApp.Api.Session;
using PetelApp.Api.Models;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<BaseController> baseLogger,
            ILogger<StudentsController> logger)
            : base(userSessionService, baseLogger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("registration-summary")]
        public async Task<ActionResult<IEnumerable<StudentRegistrationSummaryDto>>> GetRegistrationSummary([FromQuery] int? schoolYearId = null)
        {
            try
            {
                // Get EntityId from session (Entity-Based Request Flow)
                var session = GetCurrentSession();
                if (session == null || string.IsNullOrEmpty(session.EntityId) || !int.TryParse(session.EntityId, out int schoolId))
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                if (!schoolYearId.HasValue)
                {
                    return BadRequest(new { message = "Missing schoolYearId" });
                }

                // Query registration summary scoped by EntityId (schoolId)
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

                return Ok(registrationSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration summary");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}