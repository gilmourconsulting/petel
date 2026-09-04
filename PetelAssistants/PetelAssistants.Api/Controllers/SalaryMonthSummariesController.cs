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
    [Route("api/salary-month-summaries")]
    public class SalaryMonthSummariesController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _shared;
        private readonly MonthlyImportComparisonService _comparisonService;

        public SalaryMonthSummariesController(
            AssistDbContext context,
            SharedDbContext shared,
            MonthlyImportComparisonService comparisonService,
            UserSessionService sessionService,
            ILogger<SalaryMonthSummariesController> logger)
            : base(sessionService, logger)
        {
            _context = context;
            _shared = shared;
            _comparisonService = comparisonService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int year, [FromQuery] int month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var processId = await _context.SalaryUploadProcesses
                .Where(p => p.PeriodYear == year && p.PeriodMonth == month)
                .OrderByDescending(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            if (processId == null)
                return Ok(new { success = true, data = new MonthSummaryResponse() });

            var hasRows = await _context.SalaryMonthSummaries.AnyAsync(s => s.ProcessId == processId.Value);
            if (!hasRows)
                await _comparisonService.RebuildSalaryProcessAsync(processId.Value, userId);

            var lines = await _context.SalaryMonthSummaries
                .AsNoTracking()
                .Where(s => s.ProcessId == processId.Value)
                .ToListAsync();

            var typeNames = await _shared.AssistantTypes.AsNoTracking()
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var dto = new MonthSummaryResponse
            {
                ProcessId = processId,
                HasBudget = lines.Any(l => l.HasBudget),
                Lines = MapLines(lines, typeNames)
            };

            return Ok(new { success = true, data = dto });
        }

        [HttpGet("for-year")]
        public async Task<IActionResult> GetForHebrewYear([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var year = await _shared.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId);
            if (year == null)
                return NotFound(new { success = false, message = "שנה לא נמצאה" });

            var months = YearlyBudgetService.GetMonthsInYear(year);
            if (months.Count == 0)
                return Ok(new { success = true, data = new YearMonthSummariesResponse() });

            return await GetForMonthRangeAsync(months);
        }

        [HttpGet("for-gregorian-year")]
        public async Task<IActionResult> GetForGregorianYear([FromQuery] int year)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (year < 2000 || year > 2100)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var months = YearlyBudgetService.GetCalendarMonths(year);
            return await GetForMonthRangeAsync(months);
        }

        private async Task<IActionResult> GetForMonthRangeAsync(List<(int Year, int Month)> months)
        {
            if (months.Count == 0)
                return Ok(new { success = true, data = new YearMonthSummariesResponse() });

            int? userId = int.TryParse(GetCurrentSession()?.UserId, out int uid) ? uid : null;

            var from = months[0];
            var to = months[^1];
            var processes = await _context.SalaryUploadProcesses.AsNoTracking()
                .Where(p => (p.PeriodYear > from.Year || (p.PeriodYear == from.Year && p.PeriodMonth >= from.Month))
                         && (p.PeriodYear < to.Year || (p.PeriodYear == to.Year && p.PeriodMonth <= to.Month)))
                .Select(p => new { p.Id, p.PeriodYear, p.PeriodMonth })
                .ToListAsync();

            var latestProcessIds = processes
                .GroupBy(p => new { p.PeriodYear, p.PeriodMonth })
                .Select(g => g.OrderByDescending(p => p.Id).First().Id)
                .ToList();

            if (latestProcessIds.Count == 0)
                return Ok(new { success = true, data = new YearMonthSummariesResponse() });

            var summarizedIds = await _context.SalaryMonthSummaries
                .AsNoTracking()
                .Where(s => latestProcessIds.Contains(s.ProcessId))
                .Select(s => s.ProcessId)
                .Distinct()
                .ToListAsync();
            var summarizedSet = summarizedIds.ToHashSet();

            foreach (var processId in latestProcessIds.Where(id => !summarizedSet.Contains(id)))
                await _comparisonService.RebuildSalaryProcessAsync(processId, userId);

            var lines = await _context.SalaryMonthSummaries
                .AsNoTracking()
                .Where(s => latestProcessIds.Contains(s.ProcessId))
                .ToListAsync();

            var typeNames = await _shared.AssistantTypes.AsNoTracking()
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var dto = new YearMonthSummariesResponse
            {
                Lines = MapLines(lines, typeNames)
            };

            return Ok(new { success = true, data = dto });
        }

        private static List<MonthSummaryLineDto> MapLines(
            List<SalaryMonthSummary> lines,
            Dictionary<int, string> typeNames)
        {
            return lines
                .OrderBy(l => l.PeriodYear)
                .ThenBy(l => l.PeriodMonth)
                .ThenBy(l => l.AssistantTypeId == null)
                .ThenBy(l => l.AssistantTypeId)
                .Select(l => new MonthSummaryLineDto
                {
                    Id = l.Id,
                    ProcessId = l.ProcessId,
                    PeriodYear = l.PeriodYear,
                    PeriodMonth = l.PeriodMonth,
                    AssistantTypeId = l.AssistantTypeId,
                    AssistantTypeName = l.AssistantTypeId.HasValue
                        ? typeNames.GetValueOrDefault(l.AssistantTypeId.Value, l.AssistantTypeId.Value.ToString())
                        : "לא ממופה",
                    RowCount = l.RowCount,
                    Fte = l.Fte,
                    Hours = l.Hours,
                    Amount = l.Amount,
                    YearlyBudgetId = l.YearlyBudgetId,
                    BudgetFte = l.BudgetFte,
                    BudgetHours = l.BudgetHours,
                    BudgetAmount = l.BudgetAmount,
                    HasBudget = l.HasBudget
                })
                .ToList();
        }
    }
}
