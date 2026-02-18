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
                        MasterStudentId = s.MasterStudentId,
                        Version = s.Version,
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
                        Status = s.Status != null ? s.Status.Name : null,

                        // LEFT JOIN with Councils - uses council_short_name, falls back to council_long_name
                        CouncilName = _context.Councils
                            .Where(c => c.Id == s.SendingCouncil)
                            .Select(c => c.Name)
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

        /// <summary>
        /// Get a single student by ID with school year information
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var student = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.Id == id)
                    .Select(s => new
                    {
                        Id = s.Id,
                        IdNumber = s.IdNumber,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        MasterStudentId = s.MasterStudentId,
                        Version = s.Version,
                        SchoolYearId = s.SchoolYearId,
                        ClassId = s.ClassId,
                        ClassName = _context.SchoolClasses
                            .Where(sc => sc.Id == s.ClassId)
                            .Select(sc => sc.Name)
                            .FirstOrDefault(),
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        Gender = s.Gender,
                        Street = s.Street,
                        HouseNumber = s.HouseNumber,
                        City = s.City,
                        PostCode = s.PostCode,
                        SendingCouncil = s.SendingCouncil,
                        CouncilName = _context.Councils
                            .Where(c => c.Id == s.SendingCouncil)
                            .Select(c => c.Name)
                            .FirstOrDefault(),
                        DisabilityCategory = s.DisabilityCategory,
                        Cost = s.Cost,
                        Status = s.Status != null ? s.Status.Name : null,
                        // Navigation context fields
                        SchoolId = _context.SchoolYears
                            .Where(sy => sy.Id == s.SchoolYearId)
                            .Select(sy => sy.SchoolId)
                            .FirstOrDefault(),
                        SchoolName = _context.SchoolYears
                            .Where(sy => sy.Id == s.SchoolYearId)
                            .Join(_context.Entities,
                                sy => sy.SchoolId,
                                e => e.Id,
                                (sy, e) => e.Name)
                            .FirstOrDefault(),
                        YearId = _context.SchoolYears
                            .Where(sy => sy.Id == s.SchoolYearId)
                            .Select(sy => sy.YearId)
                            .FirstOrDefault(),
                        YearValue = _context.SchoolYears
                            .Where(sy => sy.Id == s.SchoolYearId)
                            .Join(_context.HebrewYears,
                                sy => sy.YearId,
                                y => y.Id,
                                (sy, y) => y.HebrewYearText)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });
                }

                return Ok(student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student {StudentId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת פרטי תלמיד",
                    error = ex.Message
                });
            }
        }

                /// <summary>
        /// Get students by council - shows all students from a specific sending council
        /// </summary>
        [HttpGet("by-council")]
        public async Task<IActionResult> GetStudentsByCouncil([FromQuery] int councilId, [FromQuery] int? yearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for students by council request");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }
        
                // Get SelectedYearId from session if not provided
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
                    return BadRequest(new { success = false, message = "נדרש מזהה שנה" });
                }
        
                _logger.LogInformation("Loading students for council {CouncilId} and year {YearId}", councilId, yearId.Value);
        
                // Get all school_year IDs for the selected Hebrew year
                var schoolYearIds = await _context.SchoolYears
                    .AsNoTracking()
                    .Where(sy => sy.YearId == yearId.Value)
                    .Select(sy => sy.Id)
                    .ToListAsync();
        
                if (!schoolYearIds.Any())
                {
                    _logger.LogWarning("No school years found for year ID {YearId}", yearId.Value);
                    return Ok(new { success = true, data = new List<object>() });
                }
        
                // Query students filtered by council and year
                var students = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.SendingCouncil == councilId && 
                               schoolYearIds.Contains(s.SchoolYearId) &&
                               s.IsLastVersion)
                    .Select(s => new
                    {
                        id = s.Id,
                        idNumber = s.IdNumber,
                        firstName = s.FirstName,
                        lastName = s.LastName,
                        gender = s.Gender,
                        street = s.Street,
                        houseNumber = s.HouseNumber,
                        city = s.City,
                        postCode = s.PostCode,
                        classId = s.ClassId,
                        className = _context.SchoolClasses
                            .Where(c => c.Id == s.ClassId)
                            .Select(c => c.Name)
                            .FirstOrDefault(),
                        startDate = s.StartDate,
                        endDate = s.EndDate,
                        disabilityCategory = s.DisabilityCategory,
                        cost = s.Cost,
                        sendingCouncil = s.SendingCouncil,
                        Status = s.Status != null ? s.Status.Name : null,
                        CouncilName = _context.Councils
                            .Where(c => c.Id == s.SendingCouncil)
                            .Select(c => c.Name)
                            .FirstOrDefault(),
                        schoolYearId = s.SchoolYearId,
                        schoolName = _context.SchoolYears
                            .Where(sy => sy.Id == s.SchoolYearId)
                            .Join(_context.Entities,
                                sy => sy.SchoolId,
                                e => e.Id,
                                (sy, e) => e.Name)
                            .FirstOrDefault()
                    })
                    .ToListAsync();
        
                _logger.LogInformation("Found {Count} students for council {CouncilId}", students.Count, councilId);
        
                return Ok(new
                {
                    success = true,
                    councilId = councilId,
                    yearId = yearId.Value,
                    data = students
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students for council {CouncilId}", councilId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תלמידי הרשות",
                    error = ex.Message
                });
            }
        }


                /// <summary>
        /// Get all versions of a student by master student ID
        /// </summary>
        [HttpGet("history/{masterStudentId}")]
        public async Task<IActionResult> GetStudentHistory(int masterStudentId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }
        
                var versions = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.MasterStudentId == masterStudentId)
                    .OrderBy(s => s.Version)
                    .Select(s => new
                    {
                        Id = s.Id,
                        MasterStudentId = s.MasterStudentId,
                        Version = s.Version,
                        IdNumber = s.IdNumber,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        ClassId = s.ClassId,
                        ClassName = _context.SchoolClasses
                            .Where(sc => sc.Id == s.ClassId)
                            .Select(sc => sc.Name)
                            .FirstOrDefault(),
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        IsLastVersion = s.IsLastVersion,
                    // CreatedAt = s.CreatedAt
                    })
                    .ToListAsync();
        
                _logger.LogInformation("Loaded {Count} versions for master student ID {MasterStudentId}", 
                    versions.Count, masterStudentId);
        
                return Ok(new { success = true, data = versions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student history for master ID {MasterStudentId}", masterStudentId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת היסטוריית תלמיד",
                    error = ex.Message
                });
            }
        }

                [HttpGet("count-by-class/{classId}")]
        public async Task<IActionResult> GetStudentCountByClass(int classId)
        {
            try
            {
                var count = await _context.SchoolStudents
                    .Where(s => s.ClassId == classId && s.IsLastVersion)
                    .CountAsync();
                
                return Ok(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting students for class {ClassId}", classId);
                return StatusCode(500, new { success = false, message = "Error counting students" });
            }
        }
    }

    
}