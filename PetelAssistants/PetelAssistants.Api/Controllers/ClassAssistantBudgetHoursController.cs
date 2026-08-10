using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/class-assistant-budget-hours")]
    public class ClassAssistantBudgetHoursController : BaseController
    {
        private static readonly HashSet<string> AllowedSchoolLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            SchoolLevels.Elementary,
            SchoolLevels.HighSchool
        };

        private readonly SharedDbContext _sharedContext;

        public ClassAssistantBudgetHoursController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<ClassAssistantBudgetHoursController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetForYear([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var yearExists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == yearId);
            if (!yearExists)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            var items = await BuildMatrixAsync(yearId);
            return Ok(new { success = true, data = items });
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertClassAssistantBudgetHoursRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request == null || request.HebrewYearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var yearExists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == request.HebrewYearId);
            if (!yearExists)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            var activeClassificationIds = await _sharedContext.ClassClassifications.AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => c.Id)
                .ToListAsync();
            var activeSet = activeClassificationIds.ToHashSet();

            var normalized = new Dictionary<(string Level, int ClassificationId), decimal>();
            foreach (var line in request.Lines ?? new())
            {
                if (string.IsNullOrWhiteSpace(line.SchoolLevel) || !AllowedSchoolLevels.Contains(line.SchoolLevel))
                    return BadRequest(new { success = false, message = "רמת בית ספר לא תקינה" });

                if (!activeSet.Contains(line.ClassClassificationId))
                    return BadRequest(new { success = false, message = "סיווג כיתה לא תקין או לא פעיל" });

                if (line.Hours < 0)
                    return BadRequest(new { success = false, message = "שעות חייבות להיות אי-שליליות" });

                var level = line.SchoolLevel.Equals(SchoolLevels.HighSchool, StringComparison.OrdinalIgnoreCase)
                    ? SchoolLevels.HighSchool
                    : SchoolLevels.Elementary;

                normalized[(level, line.ClassClassificationId)] = Math.Round(line.Hours, 2, MidpointRounding.AwayFromZero);
            }

            var existing = await _sharedContext.ClassAssistantBudgetHours
                .Where(r => r.HebrewYearId == request.HebrewYearId)
                .ToListAsync();

            var existingByKey = existing.ToDictionary(
                r => (r.SchoolLevel, r.ClassClassificationId),
                r => r);

            var now = DateTime.UtcNow;
            foreach (var ((level, classificationId), hours) in normalized)
            {
                if (existingByKey.TryGetValue((level, classificationId), out var row))
                {
                    row.Hours = hours;
                    row.UpdatedAt = now;
                    row.UpdateUser = userId;
                }
                else
                {
                    _sharedContext.ClassAssistantBudgetHours.Add(new ClassAssistantBudgetHours
                    {
                        HebrewYearId = request.HebrewYearId,
                        SchoolLevel = level,
                        ClassClassificationId = classificationId,
                        Hours = hours,
                        UserId = userId,
                        CreatedAt = now,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }
            }

            await _sharedContext.SaveChangesAsync();

            var items = await BuildMatrixAsync(request.HebrewYearId);
            return Ok(new { success = true, message = "השעות נשמרו בהצלחה", data = items });
        }

        private async Task<List<ClassAssistantBudgetHoursDto>> BuildMatrixAsync(int yearId)
        {
            var classifications = await _sharedContext.ClassClassifications.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            var existing = await _sharedContext.ClassAssistantBudgetHours.AsNoTracking()
                .Where(r => r.HebrewYearId == yearId)
                .ToListAsync();

            var byKey = existing.ToDictionary(
                r => (r.SchoolLevel, r.ClassClassificationId),
                r => r);

            var items = new List<ClassAssistantBudgetHoursDto>();
            foreach (var classification in classifications)
            {
                foreach (var level in new[] { SchoolLevels.Elementary, SchoolLevels.HighSchool })
                {
                    byKey.TryGetValue((level, classification.Id), out var row);
                    items.Add(new ClassAssistantBudgetHoursDto
                    {
                        Id = row?.Id ?? 0,
                        HebrewYearId = yearId,
                        SchoolLevel = level,
                        ClassClassificationId = classification.Id,
                        ClassClassificationName = classification.Name,
                        Hours = row?.Hours ?? 0
                    });
                }
            }

            return items;
        }
    }
}
