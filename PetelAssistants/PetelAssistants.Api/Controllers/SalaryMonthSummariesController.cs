using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
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
                Lines = lines
                    .OrderBy(l => l.AssistantTypeId == null)
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
                    .ToList()
            };

            return Ok(new { success = true, data = dto });
        }
    }
}
