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

            var institutions = await _context.Institutions.AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name)
                .ToListAsync();

            var settings = await _context.InstitutionYearSettings.AsNoTracking()
                .Where(s => s.HebrewYearId == yearId)
                .ToDictionaryAsync(s => s.InstitutionId);

            var items = institutions.Select(inst =>
            {
                settings.TryGetValue(inst.Id, out var row);
                return new InstitutionYearSettingsDto
                {
                    Id = row?.Id ?? 0,
                    InstitutionId = inst.Id,
                    InstitutionName = inst.Name,
                    InstitutionSymbol = inst.Symbol,
                    InstitutionType = inst.InstitutionType,
                    HebrewYearId = yearId,
                    SchoolDaysPerWeek = row?.SchoolDaysPerWeek ?? 0,
                    WeeklyHours = row?.WeeklyHours ?? 0,
                    HasExtendedSchoolDay = row?.HasExtendedSchoolDay ?? false
                };
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

            var activeInstitutionIds = await _context.Institutions.AsNoTracking()
                .Where(i => i.IsActive)
                .Select(i => i.Id)
                .ToListAsync();
            var activeSet = activeInstitutionIds.ToHashSet();

            foreach (var item in items)
            {
                if (!activeSet.Contains(item.InstitutionId))
                    return BadRequest(new { success = false, message = $"מוסד לא תקין: {item.InstitutionId}" });

                if (item.SchoolDaysPerWeek < 0 || item.SchoolDaysPerWeek > 7)
                    return BadRequest(new { success = false, message = "ימי לימוד בשבוע חייבים להיות בין 0 ל-7" });

                if (item.WeeklyHours < 0)
                    return BadRequest(new { success = false, message = "שעות שבועיות חייבות להיות אי-שליליות" });
            }

            var existing = await _context.InstitutionYearSettings
                .Where(s => s.HebrewYearId == request.YearId)
                .ToDictionaryAsync(s => s.InstitutionId);

            var now = DateTime.UtcNow;
            var seen = new HashSet<int>();

            foreach (var item in items)
            {
                if (!seen.Add(item.InstitutionId))
                    continue;

                var days = Math.Round(item.SchoolDaysPerWeek, 2, MidpointRounding.AwayFromZero);
                var hours = Math.Round(item.WeeklyHours, 2, MidpointRounding.AwayFromZero);

                if (existing.TryGetValue(item.InstitutionId, out var row))
                {
                    row.SchoolDaysPerWeek = days;
                    row.WeeklyHours = hours;
                    row.HasExtendedSchoolDay = item.HasExtendedSchoolDay;
                    row.UpdatedAt = now;
                    row.UpdateUser = userId;
                }
                else
                {
                    _context.InstitutionYearSettings.Add(new InstitutionYearSettings
                    {
                        EntityId = entityId,
                        InstitutionId = item.InstitutionId,
                        HebrewYearId = request.YearId,
                        SchoolDaysPerWeek = days,
                        WeeklyHours = hours,
                        HasExtendedSchoolDay = item.HasExtendedSchoolDay,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "הגדרות המוסדות נשמרו בהצלחה" });
        }
    }
}
