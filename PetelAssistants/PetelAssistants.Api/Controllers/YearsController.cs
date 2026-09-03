using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YearsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;
        private readonly AssistDbContext _assistContext;

        public YearsController(
            SharedDbContext sharedContext,
            AssistDbContext assistContext,
            UserSessionService userSessionService,
            ILogger<YearsController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
            _assistContext = assistContext;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetYear(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var year = await _sharedContext.HebrewYears
                    .AsNoTracking()
                    .Where(y => y.Id == id)
                    .Select(y => new YearDetailDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        StartDate = y.StartDate,
                        EndDate = y.EndDate,
                        IsCurrent = y.IsCurrent,
                        IsPrevious = y.IsPrevious,
                        IsActive = y.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (year == null)
                    return NotFound(new { success = false, message = "שנה לא נמצאה" });

                return Ok(new
                {
                    id = year.Id,
                    yearName = year.YearName,
                    startDate = year.StartDate,
                    endDate = year.EndDate,
                    isCurrent = year.IsCurrent,
                    isPrevious = year.IsPrevious,
                    isActive = year.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading year {Id}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת השנה" });
            }
        }

        [HttpGet("{id:int}/hub-summary")]
        public async Task<IActionResult> GetHubSummary(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var year = await _sharedContext.HebrewYears.AsNoTracking()
                    .FirstOrDefaultAsync(y => y.Id == id);
                if (year == null)
                    return NotFound(new { success = false, message = "שנה לא נמצאה" });

                var months = YearlyBudgetService.GetMonthsInYear(year);
                var summary = new YearHubSummaryDto
                {
                    AssistantCount = await _assistContext.Persons.AsNoTracking().CountAsync()
                };

                var entitlements = await _assistContext.Entitlements.AsNoTracking()
                    .Where(e => e.HebrewYearId == id && e.IsLastVersion && e.IsActive && !e.IsCancelled)
                    .Select(e => new { e.Id, e.MasterEntitlementId, e.Hours })
                    .ToListAsync();

                summary.EntitlementCount = entitlements.Count;
                var totalHours = entitlements.Sum(e => e.Hours);
                if (totalHours > 0 && entitlements.Count > 0)
                {
                    var masterIds = entitlements.Select(e => e.MasterEntitlementId).Distinct().ToList();
                    var versions = await _assistContext.Entitlements.AsNoTracking()
                        .Where(e => masterIds.Contains(e.MasterEntitlementId))
                        .Select(e => new { e.Id, e.MasterEntitlementId })
                        .ToListAsync();
                    var versionIds = versions.Select(v => v.Id).ToList();
                    var hoursByVersion = versionIds.Count == 0
                        ? new Dictionary<int, decimal>()
                        : await _assistContext.EntitlementAllocations.AsNoTracking()
                            .Where(a => a.IsActive && versionIds.Contains(a.EntitlementId))
                            .GroupBy(a => a.EntitlementId)
                            .Select(g => new { EntitlementId = g.Key, TotalHours = g.Sum(a => a.Hours) })
                            .ToDictionaryAsync(x => x.EntitlementId, x => x.TotalHours);

                    var hoursByMaster = versions
                        .GroupBy(v => v.MasterEntitlementId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Sum(v => hoursByVersion.TryGetValue(v.Id, out var h) ? h : 0m));

                    var allocatedHours = entitlements.Sum(e =>
                    {
                        var allocated = hoursByMaster.TryGetValue(e.MasterEntitlementId, out var h) ? h : 0m;
                        return Math.Min(allocated, e.Hours);
                    });
                    summary.EntitlementAllocatedPercent = Math.Round(allocatedHours / totalHours * 100, 1);
                }

                var lastBudget = await _assistContext.YearlyBudgets.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.HebrewYearId == id
                                              && b.IsLastVersion
                                              && b.Status != YearlyBudgetStatuses.Deleted);
                if (lastBudget != null)
                {
                    var budgetTotals = await _assistContext.YearlyBudgetDetails.AsNoTracking()
                        .Where(d => d.YearlyBudgetId == lastBudget.Id)
                        .GroupBy(_ => 1)
                        .Select(g => new { Hours = g.Sum(d => d.Hours), Amount = g.Sum(d => d.Amount) })
                        .FirstOrDefaultAsync();

                    summary.Budget = new YearHubBudgetSummaryDto
                    {
                        Version = lastBudget.Version,
                        Status = lastBudget.Status,
                        TotalHours = budgetTotals?.Hours ?? 0,
                        TotalAmount = budgetTotals?.Amount ?? 0
                    };
                }

                if (months.Count > 0)
                {
                    var from = months[0];
                    var to = months[^1];
                    var inYear = _assistContext.Salaries.AsNoTracking()
                        .Where(s => (s.PeriodYear > from.Year || (s.PeriodYear == from.Year && s.PeriodMonth >= from.Month))
                                 && (s.PeriodYear < to.Year || (s.PeriodYear == to.Year && s.PeriodMonth <= to.Month)));

                    var today = DateTime.Today;
                    var nowKey = today.Year * 12 + today.Month;
                    var ytdMonths = months.Where(m => m.Year * 12 + m.Month <= nowKey).ToList();
                    if (ytdMonths.Count > 0)
                    {
                        var ytdFrom = ytdMonths[0];
                        var ytdTo = ytdMonths[^1];
                        summary.SalaryYtdTotal = await _assistContext.Salaries.AsNoTracking()
                            .Where(s => (s.PeriodYear > ytdFrom.Year || (s.PeriodYear == ytdFrom.Year && s.PeriodMonth >= ytdFrom.Month))
                                     && (s.PeriodYear < ytdTo.Year || (s.PeriodYear == ytdTo.Year && s.PeriodMonth <= ytdTo.Month)))
                            .SumAsync(s => (decimal?)s.TotalSalary) ?? 0;
                    }

                    var lastSalary = await inYear
                        .OrderByDescending(s => s.PeriodYear)
                        .ThenByDescending(s => s.PeriodMonth)
                        .Select(s => new { s.PeriodYear, s.PeriodMonth })
                        .FirstOrDefaultAsync();
                    if (lastSalary != null)
                    {
                        summary.LastSalaryPeriodYear = lastSalary.PeriodYear;
                        summary.LastSalaryPeriodMonth = lastSalary.PeriodMonth;
                    }

                    summary.MeitarMonthCount = await _assistContext.MeitarRetrieveProcesses.AsNoTracking()
                        .Where(p => (p.PeriodYear > from.Year || (p.PeriodYear == from.Year && p.PeriodMonth >= from.Month))
                                 && (p.PeriodYear < to.Year || (p.PeriodYear == to.Year && p.PeriodMonth <= to.Month)))
                        .Select(p => new { p.PeriodYear, p.PeriodMonth })
                        .Distinct()
                        .CountAsync();
                }

                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading year hub summary {Id}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סיכום השנה" });
            }
        }

        [HttpGet("context")]
        public async Task<IActionResult> GetYearContext()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var years = await _sharedContext.HebrewYears
                    .AsNoTracking()
                    .Where(y => y.IsActive)
                    .OrderByDescending(y => y.Id)
                    .Select(y => new YearDetailDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        StartDate = y.StartDate,
                        EndDate = y.EndDate,
                        IsCurrent = y.IsCurrent,
                        IsPrevious = y.IsPrevious,
                        IsActive = y.IsActive
                    })
                    .ToListAsync();

                var currentYear = years.FirstOrDefault(y => y.IsCurrent) ?? years.FirstOrDefault();
                var previousYear = years.FirstOrDefault(y => y.IsPrevious)
                    ?? years.Where(y => currentYear == null || y.Id != currentYear.Id).Skip(1).FirstOrDefault();

                return Ok(new
                {
                    currentYear,
                    previousYear,
                    allYears = years
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "hebrew_years table not found; run add-years-and-menu.sql");
                return Ok(new { currentYear = (object?)null, previousYear = (object?)null, allYears = Array.Empty<object>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading year context");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת שנים" });
            }
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetAllYearsAdmin()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var years = await _sharedContext.HebrewYears
                .AsNoTracking()
                .OrderByDescending(y => y.Id)
                .Select(y => new YearDetailDto
                {
                    Id = y.Id,
                    YearName = y.YearName,
                    StartDate = y.StartDate,
                    EndDate = y.EndDate,
                    IsCurrent = y.IsCurrent,
                    IsPrevious = y.IsPrevious,
                    IsActive = y.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = years });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateYear(int id, [FromBody] UpdateHebrewYearRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var year = await _sharedContext.HebrewYears.FirstOrDefaultAsync(y => y.Id == id);
            if (year == null)
                return NotFound(new { success = false, message = "שנה לא נמצאה" });

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
                return BadRequest(new { success = false, message = "תאריך סיום חייב להיות אחרי תאריך התחלה" });

            if (request.IsCurrent)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.Id != id && y.IsCurrent).ToListAsync();
                foreach (var other in others)
                    other.IsCurrent = false;
            }

            if (request.IsPrevious)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.Id != id && y.IsPrevious).ToListAsync();
                foreach (var other in others)
                    other.IsPrevious = false;
            }

            year.StartDate = request.StartDate;
            year.EndDate = request.EndDate;
            year.IsCurrent = request.IsCurrent;
            year.IsPrevious = request.IsPrevious;
            year.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "שנת לימודים עודכנה בהצלחה" });
        }

        [HttpPost]
        public async Task<IActionResult> CreateYear([FromBody] CreateHebrewYearRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.YearName))
                return BadRequest(new { success = false, message = "שם השנה הוא שדה חובה" });

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
                return BadRequest(new { success = false, message = "תאריך סיום חייב להיות אחרי תאריך התחלה" });

            var yearName = request.YearName.Trim();
            if (await _sharedContext.HebrewYears.AnyAsync(y => y.YearName == yearName))
                return BadRequest(new { success = false, message = "שנה עם שם זה כבר קיימת" });

            if (request.IsCurrent)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.IsCurrent).ToListAsync();
                foreach (var other in others)
                    other.IsCurrent = false;
            }

            if (request.IsPrevious)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.IsPrevious).ToListAsync();
                foreach (var other in others)
                    other.IsPrevious = false;
            }

            var year = new Models.HebrewYear
            {
                YearName = yearName,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsCurrent = request.IsCurrent,
                IsPrevious = request.IsPrevious,
                IsActive = request.IsActive
            };

            _sharedContext.HebrewYears.Add(year);
            await _sharedContext.SaveChangesAsync();

            return Ok(new { success = true, message = "שנת לימודים נוצרה בהצלחה", data = new { year.Id } });
        }
    }
}
