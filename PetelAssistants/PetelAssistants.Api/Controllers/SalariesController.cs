using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalariesController : BaseController
    {
        private readonly AssistDbContext _context;

        public SalariesController(
            AssistDbContext context,
            UserSessionService sessionService,
            ILogger<SalariesController> logger)
            : base(sessionService, logger)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? year, [FromQuery] int? month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month.HasValue && (month.Value < 1 || month.Value > 12))
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var query = _context.Salaries.AsNoTracking();

            if (year.HasValue)
                query = query.Where(s => s.PeriodYear == year.Value);

            if (month.HasValue)
                query = query.Where(s => s.PeriodMonth == month.Value);

            var items = await query
                .Select(s => new SalaryListItemDto
                {
                    Id = s.Id,
                    PeriodYear = s.PeriodYear,
                    PeriodMonth = s.PeriodMonth,
                    NationalId = s.NationalId,
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.DepartmentName,
                    PositionPercentage = s.PositionPercentage,
                    TotalSalary = s.TotalSalary,
                    MatchedPersonId = s.MatchedPersonId,
                    MatchedPersonName = s.MatchedPerson != null
                        ? s.MatchedPerson.Details
                            .Where(d => d.IsLastVersion)
                            .Select(d => (d.FirstName + " " + d.LastName).Trim())
                            .FirstOrDefault()
                        : null,
                    HasIdWarning = s.HasIdWarning,
                    ProcessId = s.ProcessId
                })
                .ToListAsync();

            await ApplyAllocationFlagsAsync(items);

            // Sort in memory — national_id is encrypted at rest, so DB ORDER BY is not meaningful.
            items = items
                .OrderBy(s => s.NationalId)
                .ThenBy(s => s.DepartmentId)
                .ToList();

            return Ok(new { success = true, data = items });
        }

        /// <summary>
        /// Sets HasAllocationForPeriod per row: the matched person has at least one active
        /// allocation whose date range overlaps the row's salary month.
        /// </summary>
        private async Task ApplyAllocationFlagsAsync(List<SalaryListItemDto> items)
        {
            var personIds = items
                .Where(i => i.MatchedPersonId.HasValue)
                .Select(i => i.MatchedPersonId!.Value)
                .Distinct()
                .ToList();

            if (personIds.Count == 0)
                return;

            var allocations = await _context.EntitlementAllocations
                .AsNoTracking()
                .Where(a => a.IsActive && personIds.Contains(a.PersonId))
                .Select(a => new { a.PersonId, a.StartDate, a.EndDate })
                .ToListAsync();

            var allocationsByPerson = allocations
                .GroupBy(a => a.PersonId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var item in items)
            {
                if (!item.MatchedPersonId.HasValue ||
                    !allocationsByPerson.TryGetValue(item.MatchedPersonId.Value, out var personAllocations))
                    continue;

                var monthStart = new DateOnly(item.PeriodYear, item.PeriodMonth, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                item.HasAllocationForPeriod = personAllocations
                    .Any(a => a.StartDate <= monthEnd && a.EndDate >= monthStart);
            }
        }
    }
}
