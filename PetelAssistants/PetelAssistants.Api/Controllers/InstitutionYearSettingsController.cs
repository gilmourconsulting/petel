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
    [Route("api/institution-year-settings")]
    public class InstitutionYearSettingsController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;

        public InstitutionYearSettingsController(
            AssistDbContext context,
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<InstitutionYearSettingsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
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

            var roster = await _context.InstitutionYearSettings.AsNoTracking()
                .Where(s => s.HebrewYearId == yearId)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var items = roster.Select(row => new InstitutionYearSettingsDto
            {
                Id = row.Id,
                InstitutionId = row.InstitutionId,
                InstitutionName = row.Name,
                InstitutionSymbol = row.Symbol,
                InstitutionType = row.InstitutionType,
                HebrewYearId = yearId,
                SchoolDaysPerWeek = row.SchoolDaysPerWeek,
                WeeklyHours = row.WeeklyHours,
                HasExtendedSchoolDay = row.HasExtendedSchoolDay
            }).ToList();

            return Ok(new { success = true, data = items });
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertInstitutionYearSettingsRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (request == null || request.YearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var yearExists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == request.YearId);
            if (!yearExists)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            var items = request.Items ?? new List<InstitutionYearSettingsItemRequest>();

            var existing = await _context.InstitutionYearSettings
                .Where(s => s.HebrewYearId == request.YearId)
                .ToDictionaryAsync(s => s.InstitutionId);

            foreach (var item in items)
            {
                if (!existing.ContainsKey(item.InstitutionId))
                    return BadRequest(new { success = false, message = $"מוסד לא נמצא ברשימת השנה: {item.InstitutionId}" });

                if (item.SchoolDaysPerWeek < 0 || item.SchoolDaysPerWeek > 7)
                    return BadRequest(new { success = false, message = "ימי לימוד בשבוע חייבים להיות בין 0 ל-7" });

                if (item.WeeklyHours < 0)
                    return BadRequest(new { success = false, message = "שעות שבועיות חייבות להיות אי-שליליות" });
            }

            var now = DateTime.UtcNow;
            var seen = new HashSet<int>();

            foreach (var item in items)
            {
                if (!seen.Add(item.InstitutionId))
                    continue;

                if (!existing.TryGetValue(item.InstitutionId, out var row))
                    continue;

                row.SchoolDaysPerWeek = Math.Round(item.SchoolDaysPerWeek, 2, MidpointRounding.AwayFromZero);
                row.WeeklyHours = Math.Round(item.WeeklyHours, 2, MidpointRounding.AwayFromZero);
                row.HasExtendedSchoolDay = item.HasExtendedSchoolDay;
                row.UpdatedAt = now;
                row.UpdateUser = userId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "הגדרות המוסדות נשמרו בהצלחה" });
        }
    }
}
