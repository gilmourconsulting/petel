using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Controllers;
using PetelATH.Api.Data;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
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
                        NumberOfSessions = p.NumberOfSessions,
                        ApprovalStatus = p.ApprovalStatus,
                        CalculateByHourlyCost = p.CalculateByHourlyCost,
                        Version = p.Version,
                        IsLastVersion = p.IsLastVersion,
                        MasterId = p.MasterId,
                        Cost = p.Cost,
                        ApprovedAmount = p.ApprovedAmount,
                        HourlyCost = p.HourlyCost,
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
                        NumberOfSessions = p.NumberOfSessions,
                        ApprovalStatus = p.ApprovalStatus,
                        CalculateByHourlyCost = p.CalculateByHourlyCost,
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
                    NumberOfSessions = dto.NumberOfSessions,
                    ApprovalStatus = dto.ApprovalStatus,
                    CalculateByHourlyCost = dto.CalculateByHourlyCost,
                    Cost = dto.Cost,
                    ApprovedAmount = dto.ApprovedAmount,
                    HourlyCost = dto.HourlyCost,
                    UserId = int.Parse(session.UserId),
                    Version = 1,
                    IsLastVersion = true,
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
                    NumberOfSessions = program.NumberOfSessions,
                    ApprovalStatus = program.ApprovalStatus,
                    CalculateByHourlyCost = program.CalculateByHourlyCost,
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
                    currentProgram.NumberOfSessions != dto.NumberOfSessions ||
                    currentProgram.ApprovalStatus != dto.ApprovalStatus ||
                    currentProgram.CalculateByHourlyCost != dto.CalculateByHourlyCost ||
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
                        NumberOfSessions = currentProgram.NumberOfSessions,
                        ApprovalStatus = currentProgram.ApprovalStatus,
                        CalculateByHourlyCost = currentProgram.CalculateByHourlyCost,
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
                    NumberOfSessions = dto.NumberOfSessions,
                    ApprovalStatus = dto.ApprovalStatus,
                    CalculateByHourlyCost = dto.CalculateByHourlyCost,
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
                    NumberOfSessions = newVersion.NumberOfSessions,
                    ApprovalStatus = newVersion.ApprovalStatus,
                    CalculateByHourlyCost = newVersion.CalculateByHourlyCost,
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
    /// Get maximum allowed price for a program based on year and number of students
    /// If exact match not found:
    /// - If students < lowest tier: use lowest tier price
    /// - If students > highest tier: use highest tier price
    /// - Otherwise: use the tier for that student count (assumes continuity)
    /// </summary>
    [HttpGet("max-price")]
    public async Task<IActionResult> GetMaxPrice([FromQuery] int yearId, [FromQuery] int students)
    {
        try
        {
            _logger.LogInformation(
                "Getting max price for yearId {YearId} and {Students} students",
                yearId,
                students
            );
    
            // Try to find exact match first
            var exactMatch = await _context.AdditionalStudyProgramsPricing
                .AsNoTracking()
                .Where(p => p.YearId == yearId && p.Students == students && p.Price != null)
                .FirstOrDefaultAsync();
    
            if (exactMatch != null)
            {
                _logger.LogInformation(
                    "Found exact match: price {Price} for yearId {YearId} and {Students} students",
                    exactMatch.Price,
                    yearId,
                    students
                );
    
                return Ok(new
                {
                    success = true,
                    maxPrice = exactMatch.Price,
                    studentCount = exactMatch.Students,
                    message = "מחיר מקסימלי נטען בהצלחה"
                });
            }
    
            // No exact match - get all pricing tiers for this year
            var allPricing = await _context.AdditionalStudyProgramsPricing
                .AsNoTracking()
                .Where(p => p.YearId == yearId && p.Price != null)
                .OrderBy(p => p.Students)
                .ToListAsync();
    
            if (!allPricing.Any())
            {
                _logger.LogWarning(
                    "No pricing found for yearId {YearId}",
                    yearId
                );
                
                return Ok(new
                {
                    success = true,
                    maxPrice = (decimal?)null,
                    studentCount = 0,
                    message = "לא נמצא מחיר מקסימלי מוגדר"
                });
            }
    
            var lowestTier = allPricing.First();
            var highestTier = allPricing.Last();
    
            AdditionalStudyProgramsPricing selectedTier;
    
            if (students < lowestTier.Students)
            {
                // Student count is lower than lowest tier - use lowest tier
                selectedTier = lowestTier;
                _logger.LogInformation(
                    "Student count {Students} is below lowest tier ({LowestTier}). Using lowest tier price {Price}",
                    students,
                    lowestTier.Students,
                    lowestTier.Price
                );
            }
            else if (students > highestTier.Students)
            {
                // Student count is higher than highest tier - use highest tier
                selectedTier = highestTier;
                _logger.LogInformation(
                    "Student count {Students} is above highest tier ({HighestTier}). Using highest tier price {Price}",
                    students,
                    highestTier.Students,
                    highestTier.Price
                );
            }
            else
            {
                // Student count is between lowest and highest - find the appropriate tier
                // Assuming continuity, use the tier at or below the student count
                selectedTier = allPricing
                    .Where(p => p.Students <= students)
                    .OrderByDescending(p => p.Students)
                    .First();
                
                _logger.LogInformation(
                    "Student count {Students} falls within range. Using tier for {TierStudents} students with price {Price}",
                    students,
                    selectedTier.Students,
                    selectedTier.Price
                );
            }
    
            return Ok(new
            {
                success = true,
                maxPrice = selectedTier.Price,
                studentCount = selectedTier.Students,
                message = "מחיר מקסימלי נטען בהצלחה"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting max price for yearId {YearId} and {Students} students", yearId, students);
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בטעינת מחיר מקסימלי",
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