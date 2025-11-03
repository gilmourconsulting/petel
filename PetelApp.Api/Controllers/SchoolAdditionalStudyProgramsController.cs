using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Models;
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
        /// Get school additional study programs for a specific school year
        /// NO AUTHENTICATION REQUIRED - Similar to school tracks pattern
        /// </summary>
        [HttpGet("by-school-year/{schoolYearId}")]
        public async Task<IActionResult> GetBySchoolYear(int schoolYearId)
        {
            try
            {
                _logger.LogInformation(
                    "Loading additional study programs for school year {SchoolYearId}",
                    schoolYearId
                );

                var programs = await _context.SchoolAdditionalStudyPrograms
                    .AsNoTracking()
                    .Include(p => p.SchoolClass)
                    .Where(p => p.SchoolYearId == schoolYearId)
                    .Select(p => new
                    {
                        id = p.Id,
                        schoolYearId = p.SchoolYearId,
                        classId = p.ClassId,
                        className = p.SchoolClass != null ?
                            p.SchoolClass.Level + " " + p.SchoolClass.ClassNumber : "",
                        name = p.Name,
                        weeklyHours = p.WeeklyHours,
                        numberOfStudents = p.NumberOfStudents
                    })
                    .OrderBy(p => p.className)
                    .ThenBy(p => p.name)
                    .ToListAsync();

                _logger.LogInformation(
                    "Found {Count} additional study programs for school year {SchoolYearId}",
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
        /// Create new additional study program with validation
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProgram([FromBody] CreateSchoolAdditionalStudyProgramDto dto)
        {
            try
            {
                var session = GetCurrentSession();

                _logger.LogInformation("Creating additional study program for school year {SchoolYearId}", dto.SchoolYearId);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "שם התל\"ן הוא שדה חובה"
                    });
                }

                if (dto.WeeklyHours < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "מספר השעות חייב להיות חיובי"
                    });
                }

                if (dto.NumberOfStudents < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "מספר התלמידים חייב להיות חיובי"
                    });
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

                // Check for duplicate (same name/class combination)
                var exists = await _context.SchoolAdditionalStudyPrograms
                    .AnyAsync(p => p.SchoolYearId == dto.SchoolYearId &&
                                  p.ClassId == dto.ClassId &&
                                  p.Name == dto.Name);

                if (exists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "תל\"ן זה כבר קיים עבור כיתה זו"
                    });
                }

                var program = new SchoolAdditionalStudyProgram
                {
                    SchoolYearId = dto.SchoolYearId,
                    ClassId = dto.ClassId,
                    Name = dto.Name,
                    WeeklyHours = dto.WeeklyHours,
                    NumberOfStudents = dto.NumberOfStudents,
                    UserId = int.Parse(session.UserId),
                    CreatedAt = DateTime.UtcNow
                };

                _context.SchoolAdditionalStudyPrograms.Add(program);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Additional study program created successfully with ID {Id}", program.Id);

                return Ok(new
                {
                    success = true,
                    data = new { id = program.Id },
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
        /// Update existing additional study program with validation
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProgram(int id, [FromBody] UpdateSchoolAdditionalStudyProgramDto dto)
        {
            try
            {
                var session = GetCurrentSession();

                _logger.LogInformation("Updating additional study program {Id}", id);

                var program = await _context.SchoolAdditionalStudyPrograms.FindAsync(id);

                if (program == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "תל\"ן לא נמצא"
                    });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "שם התל\"ן הוא שדה חובה"
                    });
                }

                if (dto.WeeklyHours < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "מספר השעות חייב להיות חיובי"
                    });
                }

                if (dto.NumberOfStudents < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "מספר התלמידים חייב להיות חיובי"
                    });
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

                // Check for duplicate (excluding current record)
                var duplicateExists = await _context.SchoolAdditionalStudyPrograms
                    .AnyAsync(p => p.Id != id &&
                                  p.SchoolYearId == dto.SchoolYearId &&
                                  p.ClassId == dto.ClassId &&
                                  p.Name == dto.Name);

                if (duplicateExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "תל\"ן זה כבר קיים עבור כיתה זו"
                    });
                }

                // Update fields
                program.ClassId = dto.ClassId;
                program.Name = dto.Name;
                program.WeeklyHours = dto.WeeklyHours;
                program.NumberOfStudents = dto.NumberOfStudents;
                program.UserId = int.Parse(session.UserId);
                // CreatedAt will be updated automatically by database timestamp

                await _context.SaveChangesAsync();

                _logger.LogInformation("Additional study program {Id} updated successfully", id);

                return Ok(new
                {
                    success = true,
                    data = new { id = program.Id },
                    message = "תל\"ן עודכן בהצלחה"
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
        /// Delete additional study program
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProgram(int id)
        {
            try
            {
                _logger.LogInformation("Deleting additional study program {Id}", id);

                var program = await _context.SchoolAdditionalStudyPrograms.FindAsync(id);

                if (program == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "תל\"ן לא נמצא"
                    });
                }

                _context.SchoolAdditionalStudyPrograms.Remove(program);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Additional study program {Id} deleted successfully", id);

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