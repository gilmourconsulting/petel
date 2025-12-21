using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolAdditionalStudyProgramsController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolAdditionalStudyProgramsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolAdditionalStudyProgramsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get ONLY latest versions for a school year
        /// </summary>
        [HttpGet("by-school-year/{schoolYearId}")]
        public async Task<IActionResult> GetBySchoolYear(int schoolYearId)
        {
            try
            {
                _logger.LogInformation(
                    "Loading latest additional study programs for school year {SchoolYearId}",
                    schoolYearId
                );

                // ✅ Query only IsLastVersion = true records
                var programs = await _context.SchoolAdditionalStudyPrograms
                    .AsNoTracking()
                    .Include(p => p.SchoolClass)
                    .Where(p => p.SchoolYearId == schoolYearId && p.IsLastVersion) // ✅ Filter by last version
                    .Select(p => new SchoolAdditionalStudyProgramDto
                    {
                        Id = p.Id,
                        SchoolYearId = p.SchoolYearId,
                        ClassId = p.ClassId,
                        ClassName = p.SchoolClass != null ?
                            p.SchoolClass.Level + " " + p.SchoolClass.ClassNumber : "",
                        Name = p.Name,
                        WeeklyHours = p.WeeklyHours,
                        NumberOfStudents = p.NumberOfStudents,
                        Version = p.Version,              // ✅ Include version
                        IsLastVersion = p.IsLastVersion,  // ✅ Include flag
                        MasterId = p.MasterId,          // ✅ Include MasterId
                        Cost = p.Cost,                    // ✅ Include cost
                        ApprovedAmount = p.ApprovedAmount, // ✅ Include approved amount
                        HourlyCost = p.HourlyCost,         // ✅ Include hourly cost
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        UserId = p.UserId,

                        UserName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.FullName ?? u.Username)
                            .FirstOrDefault() ?? "לא ידוע"
                    })
                    .OrderBy(p => p.ClassName)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                _logger.LogInformation(
                    "Found {Count} latest additional study programs for school year {SchoolYearId}",
                    programs.Count,
                    schoolYearId
                );

                return Ok(new
                {
                    success = true,
                    data = programs,
                    message = "Additional study programs retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting additional study programs for school year {SchoolYearId}", schoolYearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תל\"ן",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// ✅ NEW: Get version history for a program
        /// </summary>
        [HttpGet("{masterId}/history")]
        public async Task<IActionResult> GetVersionHistory(int masterId)
        {
            try
            {
                _logger.LogInformation("Loading version history for master {MasterId}", masterId);

                var history = await _context.SchoolAdditionalStudyPrograms
                    .AsNoTracking()
                    .Include(p => p.SchoolClass)
                    .Where(p => p.MasterId == masterId)
                    .OrderByDescending(p => p.Version)
                    .Select(p => new SchoolAdditionalStudyProgramDto
                    {
                        Id = p.Id,
                        SchoolYearId = p.SchoolYearId,
                        ClassId = p.ClassId,
                        ClassName = p.SchoolClass != null ?
                            p.SchoolClass.Level + " " + p.SchoolClass.ClassNumber : "",
                        Name = p.Name,
                        WeeklyHours = p.WeeklyHours,
                        NumberOfStudents = p.NumberOfStudents,
                        Version = p.Version,
                        IsLastVersion = p.IsLastVersion,
                        Cost = p.Cost,
                        HourlyCost = p.HourlyCost,
                        ApprovedAmount = p.ApprovedAmount,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        UserId = p.UserId,

                        UserName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.FullName ?? u.Username)
                            .FirstOrDefault() ?? "לא ידוע"
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading version history for master {MasterId}", masterId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת היסטוריית גרסאות"
                });
            }
        }

        /// <summary>
        /// Create new program - Version 1 with MasterId = own ID
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProgram([FromBody] CreateSchoolAdditionalStudyProgramDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "שגיאה בזיהוי המשתמש" });
                }

                _logger.LogInformation("Creating additional study program for school year {SchoolYearId}", dto.SchoolYearId);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new { success = false, message = "שם התל\"ן הוא שדה חובה" });
                }

                if (dto.WeeklyHours < 0)
                {
                    return BadRequest(new { success = false, message = "מספר השעות חייב להיות חיובי" });
                }

                if (dto.NumberOfStudents < 0)
                {
                    return BadRequest(new { success = false, message = "מספר התלמידים חייב להיות חיובי" });
                }

                // Verify class exists and belongs to school year
                var classExists = await _context.SchoolClasses
                    .AnyAsync(sc => sc.Id == dto.ClassId && sc.SchoolYearId == dto.SchoolYearId);

                if (!classExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "כיתה לא נמצאה או לא שייכת לשנת הלימודים"
                    });
                }

                // ✅ Create version 1 with temporary MasterId
                var program = new SchoolAdditionalStudyProgram
                {
                    SchoolYearId = dto.SchoolYearId,
                    ClassId = dto.ClassId,
                    Name = dto.Name.Trim(),
                    WeeklyHours = dto.WeeklyHours,
                    NumberOfStudents = dto.NumberOfStudents,
                    Cost = dto.Cost,              // ✅ Include cost
                    ApprovedAmount = dto.ApprovedAmount, // ✅ Include approved amount
                    HourlyCost = dto.HourlyCost,         // ✅ Include hourly cost
                    UserId = int.Parse(session.UserId),
                    Version = 1,                   // ✅ First version
                    IsLastVersion = true,          // ✅ Latest version
                    //MasterId = 0,                  // ✅ Temporary - will update after save
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.SchoolAdditionalStudyPrograms.Add(program);
                await _context.SaveChangesAsync();

                // ✅ CRITICAL: Set MasterId to own ID for first version
                program.MasterId = program.Id;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Additional study program created with ID {Id} (v1, MasterId: {MasterId})",
                    program.Id,
                    program.MasterId
                );

                // ✅ Return full DTO
                var result = new SchoolAdditionalStudyProgramDto
                {
                    Id = program.Id,
                    SchoolYearId = program.SchoolYearId,
                    ClassId = program.ClassId,
                    ClassName = (await _context.SchoolClasses.FindAsync(program.ClassId))?.Name ?? "",
                    Name = program.Name,
                    WeeklyHours = program.WeeklyHours,
                    NumberOfStudents = program.NumberOfStudents,
                    Version = program.Version,
                    IsLastVersion = program.IsLastVersion,
                    MasterId = program.MasterId ?? program.Id,
                    Cost = program.Cost,
                    ApprovedAmount = program.ApprovedAmount,
                    HourlyCost = program.HourlyCost,
                    CreatedAt = program.CreatedAt,
                    UpdatedAt = program.UpdatedAt
                };

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "תל\"ן נוסף בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating additional study program");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת תל\"ן",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// ✅ UPDATE: Creates new version instead of modifying existing record
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProgram(int id, [FromBody] UpdateSchoolAdditionalStudyProgramDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                _logger.LogInformation("Updating additional study program {Id}", id);

                // ✅ Find current version
                var currentProgram = await _context.SchoolAdditionalStudyPrograms
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsLastVersion);

                if (currentProgram == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "תל\"ן לא נמצא או שאינו הגרסה האחרונה"
                    });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new { success = false, message = "שם התל\"ן הוא שדה חובה" });
                }

                if (dto.WeeklyHours < 0)
                {
                    return BadRequest(new { success = false, message = "מספר השעות חייב להיות חיובי" });
                }

                if (dto.NumberOfStudents < 0)
                {
                    return BadRequest(new { success = false, message = "מספר התלמידים חייב להיות חיובי" });
                }

                // ✅ Check if any data actually changed
                bool hasChanges =
                    currentProgram.ClassId != dto.ClassId ||
                    currentProgram.Name != dto.Name.Trim() ||
                    currentProgram.WeeklyHours != dto.WeeklyHours ||
                    currentProgram.NumberOfStudents != dto.NumberOfStudents ||
                    currentProgram.Cost != dto.Cost ||
                    currentProgram.ApprovedAmount != dto.ApprovedAmount ||
                    currentProgram.HourlyCost != dto.HourlyCost;

                if (!hasChanges)
                {
                    _logger.LogInformation("No changes detected for program {Id} - skipping version creation", id);

                    var unchangedResult = new SchoolAdditionalStudyProgramDto
                    {
                        Id = currentProgram.Id,
                        SchoolYearId = currentProgram.SchoolYearId,
                        ClassId = currentProgram.ClassId,
                        ClassName = (await _context.SchoolClasses.FindAsync(currentProgram.ClassId))?.Name ?? "",
                        Name = currentProgram.Name,
                        WeeklyHours = currentProgram.WeeklyHours,
                        NumberOfStudents = currentProgram.NumberOfStudents,
                        Version = currentProgram.Version,
                        IsLastVersion = currentProgram.IsLastVersion,
                        Cost = currentProgram.Cost,
                        ApprovedAmount = currentProgram.ApprovedAmount,
                        HourlyCost = currentProgram.HourlyCost,
                        CreatedAt = currentProgram.CreatedAt,
                        UpdatedAt = currentProgram.UpdatedAt
                    };

                    return Ok(new
                    {
                        success = true,
                        data = unchangedResult,
                        message = "אין שינויים לשמירה"
                    });
                }

                // ✅ VERSIONING STRATEGY: Mark current as not last version
                currentProgram.IsLastVersion = false;
                currentProgram.UpdatedAt = DateTime.UtcNow;

                // ✅ Create new version with incremented version number
                var newVersion = new SchoolAdditionalStudyProgram
                {
                    SchoolYearId = currentProgram.SchoolYearId,
                    ClassId = dto.ClassId,
                    Name = dto.Name.Trim(),
                    WeeklyHours = dto.WeeklyHours,
                    NumberOfStudents = dto.NumberOfStudents,
                    Cost = dto.Cost,
                    ApprovedAmount = dto.ApprovedAmount,
                    HourlyCost = dto.HourlyCost,
                    UserId = int.Parse(session.UserId),
                    Version = currentProgram.Version + 1,  // ✅ Increment version
                    IsLastVersion = true,                  // ✅ New latest version
                    MasterId = currentProgram.MasterId,    // ✅ Same master for version chain
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.SchoolAdditionalStudyPrograms.Add(newVersion);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created new version {Version} (ID: {NewId}) for program master {MasterId}",
                    newVersion.Version,
                    newVersion.Id,
                    newVersion.MasterId
                );

                var result = new SchoolAdditionalStudyProgramDto
                {
                    Id = newVersion.Id,
                    SchoolYearId = newVersion.SchoolYearId,
                    ClassId = newVersion.ClassId,
                    ClassName = (await _context.SchoolClasses.FindAsync(newVersion.ClassId))?.Name ?? "",
                    Name = newVersion.Name,
                    WeeklyHours = newVersion.WeeklyHours,
                    NumberOfStudents = newVersion.NumberOfStudents,
                    Version = newVersion.Version,
                    IsLastVersion = newVersion.IsLastVersion,
                    Cost = newVersion.Cost,
                    ApprovedAmount = newVersion.ApprovedAmount,
                    HourlyCost = newVersion.HourlyCost,
                    CreatedAt = newVersion.CreatedAt,
                    UpdatedAt = newVersion.UpdatedAt
                };

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = $"תל\"ן עודכן בהצלחה (גרסה {newVersion.Version})"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating additional study program {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון תל\"ן",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// ✅ UPDATE: Logical delete - mark as not last version (keeps history)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProgram(int id)
        {
            try
            {
                _logger.LogInformation("Deleting additional study program {Id}", id);

                var program = await _context.SchoolAdditionalStudyPrograms
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsLastVersion);

                if (program == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "תל\"ן לא נמצא או כבר נמחק"
                    });
                }

                // ✅ LOGICAL DELETE: Mark as not last version
                program.IsLastVersion = false;
                program.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Logically deleted additional study program {Id} (master {MasterId})",
                    program.Id,
                    program.MasterId
                );

                return Ok(new
                {
                    success = true,
                    message = "תל\"ן נמחק בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting additional study program {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת תל\"ן",
                    error = ex.Message
                });
            }
        }
    }
}