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
        public async Task<IActionResult> GetStudents()
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

                _logger.LogInformation("Loading students for entity {EntityId}", sessionEntityId);

                // Query students filtered by school_year_id = 4 and is_last_version = true
                // Following Entity-Based Request Flow
                var students = await _context.Set<SchoolStudent>()
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == 4 && s.IsLastVersion == true)
                    .Select(s => new
                    {
                        Id = s.Id,
                        IdNumber = s.IdNumber,
                        ClassId = s.ClassId,
                      //  StartDate = s.StartDate,
                      //  EndDate = s.EndDate,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        Gender = s.Gender,
                        Street = s.Street,
                        HouseNumber = s.HouseNumber,
                        City = s.City,
                        PostCode = s.PostCode,
                       // SendingCouncil = s.SendingCouncil,
                        DisabilityCategory = s.DisabilityCategory
                    })
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} students", students.Count);
                
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