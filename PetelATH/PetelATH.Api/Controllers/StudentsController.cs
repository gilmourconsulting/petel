using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PetelATH.Api.Data;
using PetelATH.Api.Services;
using PetelATH.Api.Session;
using System.Drawing;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly StudentService _studentService;

        public StudentsController(
            AppDbContext context,
            StudentService studentService,
            UserSessionService userSessionService,
            ILogger<BaseController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _studentService = studentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStudents([FromQuery] int? schoolYearId = null, [FromQuery] bool includeDeleted = false)
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

                _logger.LogInformation("Loading students for entity {EntityId}, includeDeleted={IncludeDeleted}", sessionEntityId, includeDeleted);

                // Query students with enriched council and class names
                // Following Entity-Based Request Flow from coding guidelines
                var students = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == schoolYearId.Value && s.IsLastVersion == true
                        && (includeDeleted || s.StatusId != 8))
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
                        StatusId = s.StatusId,
                        Status = s.Status != null ? s.Status.Name : null,
                        PreviousVersionStatusName = _context.SchoolStudents
                            .Where(prev => prev.MasterStudentId == s.MasterStudentId && !prev.IsLastVersion)
                            .OrderByDescending(prev => prev.Version)
                            .Select(prev => _context.Statuses
                                .Where(st => st.Id == prev.StatusId)
                                .Select(st => st.Name)
                                .FirstOrDefault())
                            .FirstOrDefault(),

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
        /// Soft-delete a student: sets their status to 8 (נמחק)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var student = await _context.SchoolStudents.FindAsync(id);
                if (student == null)
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });

                if (student.StatusId == 8)
                    return BadRequest(new { success = false, message = "התלמיד כבר מחוק" });

                student.StatusId = 8;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Student {StudentId} soft-deleted by user {UserId}", id, session.UserId);

                return Ok(new { success = true, message = "התלמיד נמחק בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {StudentId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת תלמיד",
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
                        statusId = s.StatusId,
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

                // Get "בסיסית" pricing element IDs for this year
                var basicElementIds = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(e => e.YearId == yearId.Value &&
                                (e.ElementName == "בסיסית" || e.Title == "בסיסית"))
                    .Select(e => e.Id)
                    .ToListAsync();

                // Get basic pricing amounts per student
                Dictionary<int, decimal> basicAmounts = new();
                if (basicElementIds.Any() && students.Any())
                {
                    var studentIds = students.Select(s => s.id).ToList();
                    basicAmounts = (await _context.SchoolStudentPricingElements
                        .AsNoTracking()
                        .Where(pe => studentIds.Contains(pe.StudentId) &&
                                     basicElementIds.Contains(pe.PricingElementId))
                        .GroupBy(pe => pe.StudentId)
                        .Select(g => new { g.Key, Total = g.Sum(x => x.Price) })
                        .ToListAsync())
                        .ToDictionary(x => x.Key, x => x.Total);
                }

                // Enrich students with basic amount
                var enrichedStudents = students.Select(s => new
                {
                    s.id,
                    s.idNumber,
                    s.firstName,
                    s.lastName,
                    s.gender,
                    s.street,
                    s.houseNumber,
                    s.city,
                    s.postCode,
                    s.classId,
                    s.className,
                    s.startDate,
                    s.endDate,
                    s.disabilityCategory,
                    s.cost,
                    s.sendingCouncil,
                    s.Status,
                    s.statusId,
                    s.CouncilName,
                    s.schoolYearId,
                    s.schoolName,
                    basicAmount = basicAmounts.TryGetValue(s.id, out var ba) ? (decimal?)ba : null
                }).ToList();

                _logger.LogInformation("Found {Count} students for council {CouncilId}", students.Count, councilId);
        
                return Ok(new
                {
                    success = true,
                    councilId = councilId,
                    yearId = yearId.Value,
                    data = enrichedStudents
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

        /// <summary>
        /// Toggle "no external permit" status (StatusId = 7) for a student.
        /// Setting: creates a new student version with StatusId = 7.
        /// Reverting: restores StatusId from the previous student version.
        /// </summary>
        [HttpPost("{id}/toggle-no-permit-status")]
        public async Task<IActionResult> ToggleNoPermitStatus(int id)
        {
            const int NoPermitStatusId = 7;

            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var student = await _context.SchoolStudents
                    .FirstOrDefaultAsync(s => s.Id == id && s.IsLastVersion);

                if (student == null)
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });

                int? newStatusId;
                string? previousVersionStatusName = null;

                if (student.StatusId != NoPermitStatusId)
                {
                    // Setting status to "no external permit"
                    newStatusId = NoPermitStatusId;
                    _logger.LogInformation("🚫 Setting no-permit status for student {StudentId}", id);
                }
                else
                {
                    // Reverting — restore StatusId from the previous version
                    var previousVersion = await _context.SchoolStudents
                        .Where(prev => prev.MasterStudentId == student.MasterStudentId && !prev.IsLastVersion)
                        .OrderByDescending(prev => prev.Version)
                        .FirstOrDefaultAsync();

                    newStatusId = previousVersion?.StatusId;

                    if (previousVersion?.StatusId != null)
                    {
                        previousVersionStatusName = await _context.Statuses
                            .Where(st => st.Id == previousVersion.StatusId)
                            .Select(st => st.Name)
                            .FirstOrDefaultAsync();
                    }

                    _logger.LogInformation("✅ Reverting no-permit status for student {StudentId}, restoring StatusId={StatusId}", id, newStatusId);
                }

                var newStudentId = await _studentService.CreateNewStudentVersionAsync(
                    id,
                    s => s.StatusId = newStatusId);

                if (!newStudentId.HasValue)
                    return StatusCode(500, new { success = false, message = "שגיאה בעדכון סטטוס התלמיד" });

                string? newStatusName = newStatusId.HasValue
                    ? await _context.Statuses
                        .Where(st => st.Id == newStatusId.Value)
                        .Select(st => st.Name)
                        .FirstOrDefaultAsync()
                    : null;

                return Ok(new
                {
                    success = true,
                    newStudentId = newStudentId.Value,
                    newStatusId,
                    newStatusName,
                    previousVersionStatusName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error toggling no-permit status for student {StudentId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון סטטוס התלמיד", error = ex.Message });
            }
        }

        /// <summary>
        /// Export the current student list to an Excel file.
        /// Accepts the same query parameters as GET /api/students.
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportStudents(
            [FromQuery] int? schoolYearId = null,
            [FromQuery] bool includeDeleted = false)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });

                if (!schoolYearId.HasValue)
                {
                    var sessionSchoolYearId = session.GetProperty("SelectedSchoolYearId");
                    if (string.IsNullOrEmpty(sessionSchoolYearId) || !int.TryParse(sessionSchoolYearId, out int parsedId))
                        return BadRequest(new { success = false, message = "לא נבחרה שנת לימודים" });
                    schoolYearId = parsedId;
                }

                var students = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == schoolYearId.Value && s.IsLastVersion == true
                        && (includeDeleted || s.StatusId != 8))
                    .Select(s => new
                    {
                        s.IdNumber,
                        s.FirstName,
                        s.LastName,
                        s.Gender,
                        s.StartDate,
                        s.EndDate,
                        s.City,
                        s.Street,
                        s.HouseNumber,
                        StatusName = s.Status != null ? s.Status.Name : null,
                        CouncilName = _context.Councils
                            .Where(c => c.Id == s.SendingCouncil)
                            .Select(c => c.Name)
                            .FirstOrDefault(),
                        ClassName = _context.SchoolClasses
                            .Where(sc => sc.Id == s.ClassId)
                            .Select(sc => sc.Name)
                            .FirstOrDefault()
                    })
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("תלמידים");
                ws.View.RightToLeft = true;

                // Headers
                var headers = new[]
                {
                    "ת.ז", "שם פרטי", "שם משפחה", "מגדר", "כיתה",
                    "סטטוס", "תאריך התחלה", "תאריך סיום", "כתובת", "רשות שולחת"
                };

                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = ws.Cells[1, col];
                    cell.Value = headers[col - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x21, 0x96, 0xF3)); // primary blue
                    cell.Style.Font.Color.SetColor(Color.White);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                // Rows
                for (int i = 0; i < students.Count; i++)
                {
                    var s = students[i];
                    int row = i + 2;
                    var address = string.Join(", ",
                        new[] { s.City, s.Street, s.HouseNumber }
                        .Where(p => !string.IsNullOrWhiteSpace(p)));

                    ws.Cells[row, 1].Value = s.IdNumber;
                    ws.Cells[row, 2].Value = s.FirstName;
                    ws.Cells[row, 3].Value = s.LastName;
                    ws.Cells[row, 4].Value = s.Gender switch { 1 => "זכר", 2 => "נקבה", _ => "" };
                    ws.Cells[row, 5].Value = s.ClassName;
                    ws.Cells[row, 6].Value = s.StatusName;
                    ws.Cells[row, 7].Value = s.StartDate.HasValue ? s.StartDate.Value.ToString("dd/MM/yyyy") : "";
                    ws.Cells[row, 8].Value = s.EndDate.HasValue ? s.EndDate.Value.ToString("dd/MM/yyyy") : "";
                    ws.Cells[row, 9].Value = address;
                    ws.Cells[row, 10].Value = s.CouncilName;

                    for (int col = 1; col <= headers.Length; col++)
                        ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                var yearName = session.GetProperty("SelectedYearValue") ?? schoolYearId.Value.ToString();
                var fileName = $"תלמידים_{yearName}_{DateTime.Now:yyyyMMdd}.xlsx";
                var bytes = package.GetAsByteArray();

                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting students to Excel");
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא לאקסל", error = ex.Message });
            }
        }
    }

    
}