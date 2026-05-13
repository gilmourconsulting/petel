using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolClassesController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolClassesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolClassesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("by-school-year/{schoolYearId}")]
        public async Task<IActionResult> GetBySchoolYear(int schoolYearId)
        {
            try
            {
                var session = GetCurrentSession();

                if (session == null)
                {
                    _logger.LogWarning("GetClasses called with no valid session");
                    return Unauthorized(new { message = "No valid session found" });
                }
                _logger.LogInformation("Loading school classes for school year {SchoolYearId}, Entity: {EntityId}",
                    schoolYearId, session.EntityId);

                var classes = await _context.SchoolClasses
                    .Where(c => c.SchoolYearId == schoolYearId)
                    .OrderBy(c => c.Level)
                    .ThenBy(c => c.ClassNumber)
                    .Select(c => new SchoolClassDto
                    {
                        Id = c.Id,
                        SchoolYearId = c.SchoolYearId,
                        Name = c.Name,
                        Level = c.Level,
                        ClassNumber = c.ClassNumber,
                        EndHour = c.EndHour,
                        CharacterizationId = c.CharacterizationId,
                        CharacterizationName = c.Characterization != null
                            ? $"{c.Characterization.Name} [{c.CharacterizationId}]"
                            : null
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = classes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading school classes for school year {SchoolYearId}", schoolYearId);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת כיתות בית הספר" });
            }
        }

        // ✅ NEW: Check if class is in use
        [HttpGet("{classId}/in-use")]
        public async Task<IActionResult> CheckClassInUse(int classId)
        {
            try
            {
                var session = GetCurrentSession();

                if (session == null)
                {
                    _logger.LogWarning("GetClasses called with no valid session");
                    return Unauthorized(new { message = "No valid session found" });
                }
                _logger.LogInformation("Checking if class {ClassId} is in use, Entity: {EntityId}",
                    classId, session.EntityId);

                var studentCount = await _context.SchoolStudents
                    .CountAsync(s => s.ClassId == classId);

                var inUse = studentCount > 0;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        inUse = inUse,
                        studentCount = studentCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking class usage for {ClassId}", classId);
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת שימוש בכיתה" });
            }
        }

        // ✅ NEW: Add single class
        [HttpPost]
        public async Task<IActionResult> AddClass([FromBody] SchoolClassCreateDto request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "שגיאה בזיהוי המשתמש" });
                }

                _logger.LogInformation("Adding class for school year {SchoolYearId}, Entity: {EntityId}",
                    request.SchoolYearId, session.EntityId);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Level) ||
                    string.IsNullOrWhiteSpace(request.ClassNumber))
                {
                    return BadRequest(new { success = false, message = "שדות חובה חסרים" });
                }

                var levelTrimmed = request.Level.Trim();
                var numberTrimmed = request.ClassNumber.Trim();
                //var endHourTrimmed = request.EndHour?.Trim();

                // Check if class already exists
                var exists = await _context.SchoolClasses
                    .AnyAsync(c => c.SchoolYearId == request.SchoolYearId &&
                                  c.Level == levelTrimmed &&
                                  c.ClassNumber == numberTrimmed);

                if (exists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"כיתה {levelTrimmed} {numberTrimmed} כבר קיימת"
                    });
                }

                var newClass = new SchoolClass
                {
                    SchoolYearId = request.SchoolYearId,
                    Name = $"{levelTrimmed} {numberTrimmed}",
                    Level = levelTrimmed,
                    ClassNumber = numberTrimmed,
                    EndHour = request.EndHour,
                    CharacterizationId = request.CharacterizationId
                };

                _context.SchoolClasses.Add(newClass);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created class {ClassId}: {ClassName}",
                    newClass.Id, newClass.Name);

                var result = new SchoolClassDto
                {
                    Id = newClass.Id,
                    SchoolYearId = newClass.SchoolYearId,
                    Name = newClass.Name,
                    Level = newClass.Level,
                    ClassNumber = newClass.ClassNumber,
                    CharacterizationId = newClass.CharacterizationId
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding class");
                return StatusCode(500, new { success = false, message = "שגיאה בהוספת כיתה" });
            }
        }

        [HttpDelete("{classId}")]
        public async Task<IActionResult> DeleteClass(int classId)
        {
            try
            {
                var session = GetCurrentSession();

                if (session == null)
                {
                    _logger.LogWarning("GetClasses called with no valid session");
                    return Unauthorized(new { message = "No valid session found" });
                }
                _logger.LogInformation("Attempting to delete class {ClassId}, Entity: {EntityId}",
                    classId, session.EntityId);

                var classToDelete = await _context.SchoolClasses
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (classToDelete == null)
                {
                    return NotFound(new { success = false, message = "הכיתה לא נמצאה" });
                }

                // Check if class has students
                var hasStudents = await _context.SchoolStudents
                    .AnyAsync(s => s.ClassId == classId);

                if (hasStudents)
                {
                    var studentCount = await _context.SchoolStudents
                        .CountAsync(s => s.ClassId == classId);

                    return BadRequest(new
                    {
                        success = false,
                        message = $"לא ניתן למחוק כיתה זו. יש {studentCount} תלמידים המשויכים לכיתה."
                    });
                }

                _context.SchoolClasses.Remove(classToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted class {ClassId}: {ClassName}",
                    classId, classToDelete.Name);

                return Ok(new { success = true, message = "הכיתה נמחקה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId}", classId);
                return StatusCode(500, new { success = false, message = "שגיאה במחיקת הכיתה" });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateClasses([FromBody] SchoolClassBulkUpdateDto request)
        {
            try
            {
                var session = GetCurrentSession();

                if (session == null)
                {
                    _logger.LogWarning("GetClasses called with no valid session");
                    return Unauthorized(new { message = "No valid session found" });
                }

                _logger.LogInformation("Updating school classes for school year {SchoolYearId}, Entity: {EntityId}",
                    request.SchoolYearId, session.EntityId);

                var updatedClasses = new List<SchoolClassDto>();
                var updateCount = 0;

                foreach (var classUpdate in request.Classes)
                {
                    if (string.IsNullOrWhiteSpace(classUpdate.Level) ||
                        string.IsNullOrWhiteSpace(classUpdate.ClassNumber))
                    {
                        continue;
                    }

                    if (classUpdate.Id.HasValue && classUpdate.Id.Value > 0)
                    {
                        var existingClass = await _context.SchoolClasses
                            .FirstOrDefaultAsync(c => c.Id == classUpdate.Id.Value);

                        if (existingClass != null)
                        {
                            var levelTrimmed = classUpdate.Level?.Trim();
                            var numberTrimmed = classUpdate.ClassNumber?.Trim();

                            if (levelTrimmed == null || numberTrimmed == null)
                            {
                                _logger.LogWarning("Null value for level or class number in update for class ID {ClassId}",
                                    existingClass.Id);
                                return BadRequest(new { success = false, message = "שדות חובה חסרים בעדכון הכיתה" });
                            }

                            bool hasChanges = existingClass.Level != levelTrimmed ||
                                            existingClass.ClassNumber != numberTrimmed ||
                                            existingClass.EndHour != classUpdate.EndHour ||
                                            existingClass.CharacterizationId != classUpdate.CharacterizationId;

                            if (hasChanges)
                            {
                                _logger.LogInformation("Updating class {ClassId}: '{OldName}' → '{NewName}'",
                                    existingClass.Id, existingClass.Name, $"{levelTrimmed} {numberTrimmed}");

                                existingClass.Level = levelTrimmed;
                                existingClass.ClassNumber = numberTrimmed;
                                existingClass.Name = $"{levelTrimmed} {numberTrimmed}";
                                existingClass.EndHour = classUpdate.EndHour;
                                existingClass.CharacterizationId = classUpdate.CharacterizationId;

                                updateCount++;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated {Count} school classes for school year {SchoolYearId}",
                    updateCount, request.SchoolYearId);

                return Ok(new { success = true, data = updatedClasses });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating school classes");
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון כיתות בית הספר" });
            }
        }
    
       
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateClass(int id, [FromBody] SchoolClassUpdateDto request)
    {
        try
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                _logger.LogWarning("UpdateClass called with no valid session");
                return Unauthorized(new { message = "No valid session found" });
            }
    
            _logger.LogInformation("Updating class {ClassId}, Entity: {EntityId}", id, session.EntityId);
    
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Level) || string.IsNullOrWhiteSpace(request.ClassNumber))
            {
                return BadRequest(new { success = false, message = "שדות חובה חסרים" });
            }
    
            var classToUpdate = await _context.SchoolClasses.FirstOrDefaultAsync(c => c.Id == id);
    
            if (classToUpdate == null)
            {
                return NotFound(new { success = false, message = "הכיתה לא נמצאה" });
            }
    
            var levelTrimmed = request.Level.Trim();
            var numberTrimmed = request.ClassNumber.Trim();
    
            // Check if another class exists with same level and number (exclude current class)
            var exists = await _context.SchoolClasses
                .AnyAsync(c => c.Id != id &&
                              c.SchoolYearId == classToUpdate.SchoolYearId &&
                              c.Level == levelTrimmed &&
                              c.ClassNumber == numberTrimmed);
    
            if (exists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"כיתה {levelTrimmed} {numberTrimmed} כבר קיימת"
                });
            }
    
            classToUpdate.Level = levelTrimmed;
            classToUpdate.ClassNumber = numberTrimmed;
            classToUpdate.Name = $"{levelTrimmed} {numberTrimmed}";
            classToUpdate.EndHour = request.EndHour; // Can be null
            classToUpdate.CharacterizationId = request.CharacterizationId;
    
            await _context.SaveChangesAsync();
    
            _logger.LogInformation("Updated class {ClassId}: {ClassName}, EndHour: {EndHour}",
                classToUpdate.Id, classToUpdate.Name, classToUpdate.EndHour?.ToString() ?? "null");
    
            var result = new SchoolClassDto
            {
                Id = classToUpdate.Id,
                SchoolYearId = classToUpdate.SchoolYearId,
                Name = classToUpdate.Name,
                Level = classToUpdate.Level,
                ClassNumber = classToUpdate.ClassNumber,
                EndHour = classToUpdate.EndHour,
                CharacterizationId = classToUpdate.CharacterizationId
            };
    
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating class {ClassId}", id);
            return StatusCode(500, new { success = false, message = "שגיאה בעדכון הכיתה" });
        }
    }
    
    
}}