using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class GregorianYearService
    {
        private readonly AssistDbContext _assistContext;
        private readonly SharedDbContext _sharedContext;
        private readonly EntitlementService _entitlementService;
        private readonly PersonService _personService;

        public GregorianYearService(
            AssistDbContext assistContext,
            SharedDbContext sharedContext,
            EntitlementService entitlementService,
            PersonService personService)
        {
            _assistContext = assistContext;
            _sharedContext = sharedContext;
            _entitlementService = entitlementService;
            _personService = personService;
        }

        public async Task<GregorianYearContextDto> GetContextAsync()
        {
            var todayYear = DateTime.Today.Year;
            var previousYear = todayYear - 1;

            var hebrewYears = await _sharedContext.HebrewYears.AsNoTracking()
                .Where(y => y.IsActive && y.StartDate != null && y.EndDate != null)
                .Select(y => new { y.StartDate, y.EndDate })
                .ToListAsync();

            var years = new HashSet<int> { todayYear, previousYear };
            foreach (var year in hebrewYears)
            {
                for (var y = year.StartDate!.Value.Year; y <= year.EndDate!.Value.Year; y++)
                    years.Add(y);
            }

            var all = years
                .OrderByDescending(y => y)
                .Select(y => new GregorianYearDto
                {
                    Year = y,
                    IsCurrent = y == todayYear,
                    IsPrevious = y == previousYear
                })
                .ToList();

            return new GregorianYearContextDto
            {
                CurrentYear = all.FirstOrDefault(y => y.IsCurrent),
                PreviousYear = all.FirstOrDefault(y => y.IsPrevious),
                AllYears = all
            };
        }

        public async Task<GregorianHubSummaryDto> GetHubSummaryAsync(int calendarYear)
        {
            ValidateYear(calendarYear);
            var months = YearlyBudgetService.GetCalendarMonths(calendarYear);
            var ytdMonths = GetYtdMonths(months);
            var budget = await GetBudgetAsync(calendarYear);

            var summary = new GregorianHubSummaryDto
            {
                CalendarYear = calendarYear,
                Sources = budget.Sources,
                BudgetTotal = budget.Details.Sum(d => d.Amount),
                BudgetHours = budget.Details.Sum(d => d.Hours),
                BudgetYtd = budget.MonthDetails
                    .Where(m => ytdMonths.Contains((m.PeriodYear, m.PeriodMonth)))
                    .Sum(m => m.Amount)
            };

            if (ytdMonths.Count > 0)
            {
                var ytdFrom = ytdMonths[0];
                var ytdTo = ytdMonths[^1];
                summary.SalaryYtdTotal = await SumSalariesAsync(ytdFrom, ytdTo);
                summary.MeitarYtdTotal = await SumMeitarAsync(ytdFrom, ytdTo);
            }

            summary.NetMunicipal = summary.SalaryYtdTotal - summary.MeitarYtdTotal;
            summary.Variance = summary.SalaryYtdTotal - summary.BudgetYtd;

            var from = months[0];
            var to = months[^1];
            var lastSalary = await _assistContext.Salaries.AsNoTracking()
                .Where(s => (s.PeriodYear > from.Year || (s.PeriodYear == from.Year && s.PeriodMonth >= from.Month))
                         && (s.PeriodYear < to.Year || (s.PeriodYear == to.Year && s.PeriodMonth <= to.Month)))
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

            var yearStart = YearlyBudgetService.CalendarYearStart(calendarYear);
            var yearEnd = YearlyBudgetService.CalendarYearEnd(calendarYear);
            var entitlements = await _entitlementService.ListEntitlementsOverlappingAsync(yearStart, yearEnd);
            var active = entitlements.Where(e => e.IsActive && !e.IsCancelled).ToList();
            summary.EntitlementCount = active.Count;
            var totalHours = active.Sum(e => e.Hours);
            if (totalHours > 0)
            {
                var allocated = active.Sum(e => Math.Min(e.AllocatedHours, e.Hours));
                summary.EntitlementAllocatedPercent = Math.Round(allocated / totalHours * 100, 1);
            }

            summary.AssistantCount = await CountOverlappingAssistantsAsync(yearStart, yearEnd);
            return summary;
        }

        public async Task<GregorianBudgetDto> GetBudgetAsync(int calendarYear)
        {
            ValidateYear(calendarYear);

            var hebrewYears = await _sharedContext.HebrewYears.AsNoTracking()
                .Where(y => y.IsActive)
                .ToListAsync();
            var covering = YearlyBudgetService.GetHebrewYearsCoveringCalendarYear(hebrewYears, calendarYear);
            var months = YearlyBudgetService.GetCalendarMonths(calendarYear);

            var sources = new List<GregorianBudgetSourceDto>();
            foreach (var year in covering)
            {
                var yearMonths = months
                    .Where(m => YearlyBudgetService.YearCoversMonth(year, m.Year, m.Month))
                    .ToList();
                if (yearMonths.Count == 0)
                    continue;

                var lastBudget = await _assistContext.YearlyBudgets.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.HebrewYearId == year.Id
                                              && b.IsLastVersion
                                              && b.Status != YearlyBudgetStatuses.Deleted);

                sources.Add(new GregorianBudgetSourceDto
                {
                    HebrewYearId = year.Id,
                    HebrewYearName = year.YearName,
                    FromMonth = yearMonths[0].Month,
                    ToMonth = yearMonths[^1].Month,
                    YearlyBudgetId = lastBudget?.Id,
                    Version = lastBudget?.Version,
                    Status = lastBudget?.Status ?? "none",
                    HasBudget = lastBudget != null,
                    IsLocked = lastBudget?.Status == YearlyBudgetStatuses.Locked
                });
            }

            var budgetIds = sources
                .Where(s => s.YearlyBudgetId.HasValue)
                .Select(s => s.YearlyBudgetId!.Value)
                .ToList();

            var monthDetails = budgetIds.Count == 0
                ? new List<YearlyBudgetMonthDetail>()
                : await _assistContext.YearlyBudgetMonthDetails.AsNoTracking()
                    .Where(m => budgetIds.Contains(m.YearlyBudgetId) && m.PeriodYear == calendarYear)
                    .ToListAsync();

            var comparisons = budgetIds.Count == 0
                ? new List<YearlyBudgetComparison>()
                : await _assistContext.YearlyBudgetComparisons.AsNoTracking()
                    .Where(c => budgetIds.Contains(c.YearlyBudgetId) && c.PeriodYear == calendarYear)
                    .ToListAsync();

            var typeIds = monthDetails.Select(m => m.AssistantTypeId)
                .Concat(comparisons.Where(c => c.AssistantTypeId.HasValue).Select(c => c.AssistantTypeId!.Value))
                .Distinct()
                .ToList();

            var types = typeIds.Count == 0
                ? new List<AssistantType>()
                : await _sharedContext.AssistantTypes.AsNoTracking()
                    .Where(t => typeIds.Contains(t.Id))
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.DisplayName)
                    .ToListAsync();
            var typeNames = types.ToDictionary(t => t.Id, t => t.DisplayName);
            var typeOrder = types.Select(t => t.Id).ToList();

            var details = monthDetails
                .GroupBy(m => m.AssistantTypeId)
                .OrderBy(g =>
                {
                    var idx = typeOrder.IndexOf(g.Key);
                    return idx < 0 ? int.MaxValue : idx;
                })
                .Select(g => new YearlyBudgetDetailDto
                {
                    AssistantTypeId = g.Key,
                    AssistantTypeName = typeNames.GetValueOrDefault(g.Key, string.Empty),
                    Fte = g.Sum(x => x.Fte),
                    Hours = g.Sum(x => x.Hours),
                    Amount = g.Sum(x => x.Amount)
                })
                .ToList();

            return new GregorianBudgetDto
            {
                CalendarYear = calendarYear,
                Sources = sources,
                Details = details,
                MonthDetails = monthDetails
                    .OrderBy(m => m.PeriodMonth)
                    .ThenBy(m =>
                    {
                        var idx = typeOrder.IndexOf(m.AssistantTypeId);
                        return idx < 0 ? int.MaxValue : idx;
                    })
                    .Select(m => new YearlyBudgetMonthDetailDto
                    {
                        Id = m.Id,
                        AssistantTypeId = m.AssistantTypeId,
                        AssistantTypeName = typeNames.GetValueOrDefault(m.AssistantTypeId, string.Empty),
                        PeriodYear = m.PeriodYear,
                        PeriodMonth = m.PeriodMonth,
                        Fte = m.Fte,
                        Hours = m.Hours,
                        Amount = m.Amount,
                        Remarks = m.Remarks
                    })
                    .ToList(),
                Comparisons = comparisons
                    .OrderBy(c => c.PeriodMonth)
                    .ThenBy(c => c.AssistantTypeId == null)
                    .ThenBy(c => c.AssistantTypeId.HasValue
                        ? typeOrder.IndexOf(c.AssistantTypeId.Value)
                        : int.MaxValue)
                    .Select(c => new YearlyBudgetComparisonDto
                    {
                        Id = c.Id,
                        PeriodYear = c.PeriodYear,
                        PeriodMonth = c.PeriodMonth,
                        AssistantTypeId = c.AssistantTypeId,
                        AssistantTypeName = c.AssistantTypeId.HasValue
                            ? typeNames.GetValueOrDefault(c.AssistantTypeId.Value, c.AssistantTypeId.Value.ToString())
                            : "לא ממופה",
                        BudgetAmount = c.BudgetAmount,
                        BudgetFte = c.BudgetFte,
                        BudgetHours = c.BudgetHours,
                        SalaryAmount = c.SalaryAmount,
                        SalaryFte = c.SalaryFte,
                        SalaryHours = c.SalaryHours,
                        SalaryRowCount = c.SalaryRowCount,
                        MeitarAmount = c.MeitarAmount,
                        MeitarHours = c.MeitarHours,
                        MeitarRowCount = c.MeitarRowCount
                    })
                    .ToList()
            };
        }

        public async Task<List<EntitlementListItemDto>> GetEntitlementsAsync(int calendarYear)
        {
            ValidateYear(calendarYear);
            var from = YearlyBudgetService.CalendarYearStart(calendarYear);
            var to = YearlyBudgetService.CalendarYearEnd(calendarYear);
            var items = await _entitlementService.ListEntitlementsOverlappingAsync(from, to);
            return items.Where(e => !e.IsCancelled).ToList();
        }

        public async Task<List<GregorianAssistantDto>> GetAssistantsAsync(int calendarYear)
        {
            ValidateYear(calendarYear);
            var yearStart = YearlyBudgetService.CalendarYearStart(calendarYear);
            var yearEnd = YearlyBudgetService.CalendarYearEnd(calendarYear);

            var rows = await (
                from a in _assistContext.EntitlementAllocations.AsNoTracking()
                join e in _assistContext.Entitlements.AsNoTracking() on a.EntitlementId equals e.Id
                where a.IsActive && a.StartDate <= yearEnd && a.EndDate >= yearStart
                select new { a.PersonId, e.HebrewYearId, a.EndDate }
            ).ToListAsync();

            if (rows.Count == 0)
                return new List<GregorianAssistantDto>();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var yearIds = rows.Select(r => r.HebrewYearId).Distinct().ToList();
            var hebrewYears = await _sharedContext.HebrewYears.AsNoTracking()
                .Where(y => yearIds.Contains(y.Id))
                .ToDictionaryAsync(y => y.Id);

            var personYear = rows
                .GroupBy(r => r.PersonId)
                .ToDictionary(
                    g => g.Key,
                    g => PickDrilldownYear(g.Select(x => (x.HebrewYearId, x.EndDate)).ToList(), hebrewYears, today));

            var persons = await _personService.ListPersonsAsync();
            var personIds = personYear.Keys.ToHashSet();

            return persons
                .Where(p => personIds.Contains(p.Id))
                .Select(p =>
                {
                    var yearId = personYear[p.Id];
                    hebrewYears.TryGetValue(yearId, out var year);
                    return new GregorianAssistantDto
                    {
                        Id = p.Id,
                        IdNumber = p.IdNumber,
                        FullName = p.FullName,
                        PhoneSummary = p.PhoneSummary,
                        HebrewYearId = yearId,
                        HebrewYearName = year?.YearName ?? string.Empty
                    };
                })
                .ToList();
        }

        private async Task<int> CountOverlappingAssistantsAsync(DateOnly yearStart, DateOnly yearEnd)
        {
            return await _assistContext.EntitlementAllocations.AsNoTracking()
                .Where(a => a.IsActive && a.StartDate <= yearEnd && a.EndDate >= yearStart)
                .Select(a => a.PersonId)
                .Distinct()
                .CountAsync();
        }

        private async Task<decimal> SumSalariesAsync((int Year, int Month) from, (int Year, int Month) to)
        {
            return await _assistContext.Salaries.AsNoTracking()
                .Where(s => (s.PeriodYear > from.Year || (s.PeriodYear == from.Year && s.PeriodMonth >= from.Month))
                         && (s.PeriodYear < to.Year || (s.PeriodYear == to.Year && s.PeriodMonth <= to.Month)))
                .SumAsync(s => (decimal?)s.TotalSalary) ?? 0;
        }

        private async Task<decimal> SumMeitarAsync((int Year, int Month) from, (int Year, int Month) to)
        {
            return await _assistContext.MeitarMutavim.AsNoTracking()
                .Where(m => (m.PeriodYear > from.Year || (m.PeriodYear == from.Year && m.PeriodMonth >= from.Month))
                         && (m.PeriodYear < to.Year || (m.PeriodYear == to.Year && m.PeriodMonth <= to.Month)))
                .SumAsync(m => (decimal?)m.CalculatedAmount) ?? 0;
        }

        private static List<(int Year, int Month)> GetYtdMonths(List<(int Year, int Month)> months)
        {
            var nowKey = DateTime.Today.Year * 12 + DateTime.Today.Month;
            return months.Where(m => m.Year * 12 + m.Month <= nowKey).ToList();
        }

        private static int PickDrilldownYear(
            List<(int HebrewYearId, DateOnly EndDate)> allocations,
            Dictionary<int, HebrewYear> years,
            DateOnly today)
        {
            var coveringToday = allocations
                .Select(a => a.HebrewYearId)
                .Distinct()
                .FirstOrDefault(id =>
                    years.TryGetValue(id, out var y)
                    && y.StartDate is not null && y.EndDate is not null
                    && y.StartDate.Value <= today && y.EndDate.Value >= today);
            if (coveringToday != 0)
                return coveringToday;

            return allocations.OrderByDescending(a => a.EndDate).First().HebrewYearId;
        }

        private static void ValidateYear(int calendarYear)
        {
            if (calendarYear < 2000 || calendarYear > 2100)
                throw new InvalidOperationException("שנה לועזית לא תקינה");
        }
    }
}
