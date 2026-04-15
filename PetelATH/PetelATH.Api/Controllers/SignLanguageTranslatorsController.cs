using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SignLanguageTranslatorsController : BaseController
    {
        private readonly AppDbContext _context;

        public SignLanguageTranslatorsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SignLanguageTranslatorsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get all sign language translators for a school year
        /// </summary>
        [HttpGet("by-school-year/{schoolYearId}")]
        public async Task<IActionResult> GetBySchoolYear(int schoolYearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Getting sign language translators for school year: {SchoolYearId}", schoolYearId);

                var translators = await _context.SignLanguageTranslators
                    .AsNoTracking()
                    .Include(t => t.Person)
                    .Where(t => t.SchoolYearId == schoolYearId)
                    .Select(t => new SignLanguageTranslatorDto
                    {
                        Id = t.Id,
                        SchoolYearId = t.SchoolYearId,
                        PersonId = t.PersonId,
                        FirstName = t.Person.FirstName ?? "",
                        LastName = t.Person.LastName ?? "",
                        NationalId = t.Person.IdNumber ?? "לא צוין",
                        HoursEmployed = t.HoursEmployed
                    })
                    .OrderBy(t => t.LastName)
                    .ThenBy(t => t.FirstName)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = translators
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sign language translators for school year {SchoolYearId}", schoolYearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מתורגמני שפת סימנים"
                });
            }
        }

        /// <summary>
        /// Create new sign language translator assignment
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSignLanguageTranslatorDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate person exists
                var personExists = await _context.Persons.AnyAsync(p => p.Id == dto.PersonId);
                if (!personExists)
                {
                    return BadRequest(new { success = false, message = "איש הקשר לא נמצא" });
                }

                // Check for duplicate
                var exists = await _context.SignLanguageTranslators
                    .AnyAsync(t => t.SchoolYearId == dto.SchoolYearId && t.PersonId == dto.PersonId);

                if (exists)
                {
                    return BadRequest(new { success = false, message = "מתורגמן זה כבר קיים עבור שנה זו" });
                }

                // Validate hours
                if (dto.HoursEmployed <= 0)
                {
                    return BadRequest(new { success = false, message = "מספר שעות חייב להיות גדול מאפס" });
                }

                var translator = new SignLanguageTranslator
                {
                    SchoolYearId = dto.SchoolYearId,
                    PersonId = dto.PersonId,
                    HoursEmployed = dto.HoursEmployed,
                    UserId = int.Parse(session.UserId),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.SignLanguageTranslators.Add(translator);
                await _context.SaveChangesAsync();

                // Load person details for response
                var person = await _context.Persons.FindAsync(dto.PersonId);

                return Ok(new
                {
                    success = true,
                    message = "מתורגמן נוסף בהצלחה",
                    data = new SignLanguageTranslatorDto
                    {
                        Id = translator.Id,
                        SchoolYearId = translator.SchoolYearId,
                        PersonId = translator.PersonId,
                        FirstName = person?.FirstName ?? "",
                        LastName = person?.LastName ?? "",
                        NationalId = person?.IdNumber ?? "לא צוין", 
                        HoursEmployed = translator.HoursEmployed
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sign language translator");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת מתורגמן"
                });
            }
        }

        /// <summary>
        /// Update translator hours
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSignLanguageTranslatorDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var translator = await _context.SignLanguageTranslators.FindAsync(id);
                if (translator == null)
                {
                    return NotFound(new { success = false, message = "מתורגמן לא נמצא" });
                }

                if (dto.HoursEmployed <= 0)
                {
                    return BadRequest(new { success = false, message = "מספר שעות חייב להיות גדול מאפס" });
                }

                translator.HoursEmployed = dto.HoursEmployed;
                translator.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "שעות עודכנו בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sign language translator {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון שעות"
                });
            }
        }

        /// <summary>
        /// Delete translator assignment
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var translator = await _context.SignLanguageTranslators.FindAsync(id);
                if (translator == null)
                {
                    return NotFound(new { success = false, message = "מתורגמן לא נמצא" });
                }

                _context.SignLanguageTranslators.Remove(translator);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "מתורגמן הוסר בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sign language translator {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהסרת מתורגמן"
                });
            }
        }
    }
}