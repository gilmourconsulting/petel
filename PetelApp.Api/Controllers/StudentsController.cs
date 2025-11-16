using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : BaseController
    {
        private readonly AppDbContext _context;

        public StudentsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<BaseController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStudents([FromQuery] int? schoolYearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for students request");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    _logger.LogError("Invalid EntityId in session: '{EntityId}'", session.EntityId);
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });
                }
                // ✅ Get SelectedSchoolYearId from session if not provided in query
                if (!schoolYearId.HasValue)
                {
                    var sessionSchoolYearId = session.GetProperty("SelectedSchoolYearId");
                    if (string.IsNullOrEmpty(sessionSchoolYearId) || !int.TryParse(sessionSchoolYearId, out int parsedSchoolYearId))
                    {
                        _logger.LogWarning("No valid SelectedSchoolYearId found in session");
                        return BadRequest(new { success = false, message = "לא נבחרה שנת לימודים" });
                    }
                    schoolYearId = parsedSchoolYearId;
                }

                _logger.LogInformation("Loading students for entity {EntityId}", sessionEntityId);

                // Query students with enriched council and class names
                // Following Entity-Based Request Flow from coding guidelines
                var students = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == schoolYearId.Value && s.IsLastVersion == true)
                    .Select(s => new
                    {
                        Id = s.Id,
                        IdNumber = s.IdNumber,
                        ClassId = s.ClassId,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        Gender = s.Gender,
                        Street = s.Street,
                        HouseNumber = s.HouseNumber,
                        City = s.City,
                        PostCode = s.PostCode,
                        SendingCouncil = s.SendingCouncil,
                        DisabilityCategory = s.DisabilityCategory,
                        Cost = s.Cost,

                        // LEFT JOIN with Councils - uses council_short_name, falls back to council_long_name
                        CouncilShortName = _context.Councils
                            .Where(c => c.Id == s.SendingCouncil)
                            .Select(c => c.CouncilShortName ?? c.CouncilLongName)
                            .FirstOrDefault(),

                        // LEFT JOIN with SchoolClasses - uses name column
                        ClassName = _context.SchoolClasses
                            .Where(sc => sc.Id == s.ClassId)
                            .Select(sc => sc.Name)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} students with enriched data", students.Count);

                return Ok(new { success = true, data = students });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת נתוני תלמידים",
                    error = ex.Message
                });
            }
        }
    }
}