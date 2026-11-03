using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/hebrew-year-month-study-days")]
    public class HebrewYearMonthStudyDaysController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public HebrewYearMonthStudyDaysController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<HebrewYearMonthStudyDaysController> logger)
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

            var year = await _sharedContext.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId);
            if (year == null)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            // Auto-generate Gregorian months covering the school year (typically Sep Y – Aug Y+1).
            var months = YearlyBudgetService.GetMonthsInYear(year);
            if (months.Count == 0)
                return BadRequest(new { success = false, message = "לתאריכי השנה חסרים ערכים" });

            var existing = await _sharedContext.HebrewYearMonthStudyDays.AsNoTracking()
                .Where(r => r.HebrewYearId == yearId)
                .ToDictionaryAsync(r => (r.PeriodYear, r.PeriodMonth));

            var items = months.Select(m =>
            {
                existing.TryGetValue((m.Year, m.Month), out var row);
                return new HebrewYearMonthStudyDaysDto
                {
                    Id = row?.Id ?? 0,
                    HebrewYearId = yearId,
                    PeriodYear = m.Year,
                    PeriodMonth = m.Month,
                    StudyDays = row?.StudyDays
                };
            }).ToList();

            return Ok(new { success = true, data = items });
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertHebrewYearMonthStudyDaysRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request == null || request.HebrewYearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var year = await _sharedContext.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == request.HebrewYearId);
            if (year == null)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            var allowedMonths = YearlyBudgetService.GetMonthsInYear(year).ToHashSet();
            if (allowedMonths.Count == 0)
                return BadRequest(new { success = false, message = "לתאריכי השנה חסרים ערכים" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            var items = request.Items ?? new List<HebrewYearMonthStudyDaysItemRequest>();

            foreach (var item in items)
            {
                if (!allowedMonths.Contains((item.PeriodYear, item.PeriodMonth)))
                    return BadRequest(new { success = false, message = $"חודש מחוץ לשנת הלימודים: {item.PeriodMonth}/{item.PeriodYear}" });

                if (item.StudyDays is < 0 or > 31)
                    return BadRequest(new { success = false, message = "ימי לימוד בחודש חייבים להיות בין 0 ל-31" });
            }

            var existing = await _sharedContext.HebrewYearMonthStudyDays
                .Where(r => r.HebrewYearId == request.HebrewYearId)
                .ToDictionaryAsync(r => (r.PeriodYear, r.PeriodMonth));

            var now = DateTime.UtcNow;
            var seen = new HashSet<(int, int)>();

            foreach (var item in items)
            {
                var key = (item.PeriodYear, item.PeriodMonth);
                if (!seen.Add(key))
                    continue;

                if (item.StudyDays is null)
                {
                    if (existing.TryGetValue(key, out var toRemove))
                        _sharedContext.HebrewYearMonthStudyDays.Remove(toRemove);
                    continue;
                }

                var studyDays = Math.Round(item.StudyDays.Value, 2, MidpointRounding.AwayFromZero);

                if (existing.TryGetValue(key, out var row))
                {
                    row.StudyDays = studyDays;
                    row.UpdatedAt = now;
                    row.UpdateUser = userId;
                }
                else
                {
                    _sharedContext.HebrewYearMonthStudyDays.Add(new HebrewYearMonthStudyDays
                    {
                        HebrewYearId = request.HebrewYearId,
                        PeriodYear = item.PeriodYear,
                        PeriodMonth = item.PeriodMonth,
                        StudyDays = studyDays,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }
            }

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "ימי הלימוד בחודש נשמרו בהצלחה" });
        }
    }
}
