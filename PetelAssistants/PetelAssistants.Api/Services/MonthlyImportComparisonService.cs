using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// Builds persisted month summaries (salary/Meitar vs last locked budget) and salary anomalies
    /// after an import or recheck. Salary summaries include every payment row; anomalies never
    /// exclude cash from the summary.
    /// </summary>
    public class MonthlyImportComparisonService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _shared;
        private readonly ILogger<MonthlyImportComparisonService> _logger;

        public MonthlyImportComparisonService(
            AssistDbContext context,
            SharedDbContext shared,
            ILogger<MonthlyImportComparisonService> logger)
        {
            _context = context;
            _shared = shared;
            _logger = logger;
        }

        public async Task RebuildSalaryProcessAsync(int processId, int? userId)
        {
            var process = await _context.SalaryUploadProcesses.FirstOrDefaultAsync(p => p.Id == processId);
            if (process == null)
            {
                _logger.LogWarning("Salary process {ProcessId} not found for monthly rebuild", processId);
                return;
            }

            await RebuildSalarySummariesAsync(process, userId);
            await RebuildSalaryAnomaliesAsync(process, userId);
            await RebuildComparisonsForPeriodAsync(process.PeriodYear, process.PeriodMonth, userId);
        }

        public async Task RebuildMeitarProcessAsync(int processId, int? userId)
        {
            var process = await _context.MeitarRetrieveProcesses.FirstOrDefaultAsync(p => p.Id == processId);
            if (process == null)
            {
                _logger.LogWarning("Meitar process {ProcessId} not found for monthly rebuild", processId);
                return;
            }

            await RebuildMeitarSummariesAsync(process, userId);
            await RebuildComparisonsForPeriodAsync(process.PeriodYear, process.PeriodMonth, userId);
        }

        /// <summary>
        /// Force-rebuilds salary and Meitar month summaries for every month in the budget's
        /// Hebrew year (latest process per month, current mappings), then rebuilds
        /// <c>yearly_budget_comparisons</c> for every non-deleted version of that year.
        /// </summary>
        public async Task RecalculateYearSummariesAsync(int yearlyBudgetId, int? userId)
        {
            var budget = await _context.YearlyBudgets.FirstOrDefaultAsync(b => b.Id == yearlyBudgetId);
            if (budget == null || budget.Status == YearlyBudgetStatuses.Deleted)
                throw new InvalidOperationException("תקציב לא נמצא");

            var year = await _shared.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == budget.HebrewYearId)
                ?? throw new InvalidOperationException("שנה לא נמצאה");

            var months = YearlyBudgetService.GetMonthsInYear(year);
            foreach (var (periodYear, periodMonth) in months)
                await EnsurePeriodSummariesAsync(periodYear, periodMonth, userId, force: true);

            var budgetIds = await _context.YearlyBudgets
                .Where(b => b.HebrewYearId == budget.HebrewYearId && b.Status != YearlyBudgetStatuses.Deleted)
                .Select(b => b.Id)
                .ToListAsync();

            foreach (var budgetId in budgetIds)
                await RebuildBudgetComparisonsAsync(budgetId, userId);
        }

        public async Task RebuildBudgetComparisonsAsync(int yearlyBudgetId, int? userId)
        {
            var budget = await _context.YearlyBudgets.FirstOrDefaultAsync(b => b.Id == yearlyBudgetId);
            if (budget == null || budget.Status == YearlyBudgetStatuses.Deleted)
            {
                _logger.LogWarning("Yearly budget {BudgetId} not found or deleted for comparison rebuild", yearlyBudgetId);
                return;
            }

            var year = await _shared.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == budget.HebrewYearId);
            if (year == null)
                return;

            var months = YearlyBudgetService.GetMonthsInYear(year);
            if (months.Count == 0)
                return;

            foreach (var (periodYear, periodMonth) in months)
                await EnsurePeriodSummariesAsync(periodYear, periodMonth, userId);

            var monthDetails = await _context.YearlyBudgetMonthDetails.AsNoTracking()
                .Where(m => m.YearlyBudgetId == yearlyBudgetId)
                .ToListAsync();

            var from = months[0];
            var to = months[^1];

            var salaryProcesses = await _context.SalaryUploadProcesses.AsNoTracking()
                .Where(p => (p.PeriodYear > from.Year || (p.PeriodYear == from.Year && p.PeriodMonth >= from.Month))
                         && (p.PeriodYear < to.Year || (p.PeriodYear == to.Year && p.PeriodMonth <= to.Month)))
                .Select(p => new { p.Id, p.PeriodYear, p.PeriodMonth })
                .ToListAsync();
            var latestSalaryByPeriod = salaryProcesses
                .GroupBy(p => new { p.PeriodYear, p.PeriodMonth })
                .ToDictionary(g => (g.Key.PeriodYear, g.Key.PeriodMonth), g => g.OrderByDescending(p => p.Id).First().Id);

            var meitarProcesses = await _context.MeitarRetrieveProcesses.AsNoTracking()
                .Where(p => (p.PeriodYear > from.Year || (p.PeriodYear == from.Year && p.PeriodMonth >= from.Month))
                         && (p.PeriodYear < to.Year || (p.PeriodYear == to.Year && p.PeriodMonth <= to.Month)))
                .Select(p => new { p.Id, p.PeriodYear, p.PeriodMonth })
                .ToListAsync();
            var latestMeitarByPeriod = meitarProcesses
                .GroupBy(p => new { p.PeriodYear, p.PeriodMonth })
                .ToDictionary(g => (g.Key.PeriodYear, g.Key.PeriodMonth), g => g.OrderByDescending(p => p.Id).First().Id);

            var salaryProcessIds = latestSalaryByPeriod.Values.ToList();
            var salarySummaries = salaryProcessIds.Count == 0
                ? new List<SalaryMonthSummary>()
                : await _context.SalaryMonthSummaries.AsNoTracking()
                    .Where(s => salaryProcessIds.Contains(s.ProcessId))
                    .ToListAsync();

            var meitarProcessIds = latestMeitarByPeriod.Values.ToList();
            var meitarSummaries = meitarProcessIds.Count == 0
                ? new List<MeitarMonthSummary>()
                : await _context.MeitarMonthSummaries.AsNoTracking()
                    .Where(s => meitarProcessIds.Contains(s.ProcessId))
                    .ToListAsync();

            var existing = await _context.YearlyBudgetComparisons
                .Where(c => c.YearlyBudgetId == yearlyBudgetId)
                .ToListAsync();
            if (existing.Count > 0)
                _context.YearlyBudgetComparisons.RemoveRange(existing);

            const int unmappedKey = -1;
            var now = DateTime.UtcNow;
            foreach (var (periodYear, periodMonth) in months)
            {
                var budgetByType = monthDetails
                    .Where(m => m.PeriodYear == periodYear && m.PeriodMonth == periodMonth)
                    .ToDictionary(m => m.AssistantTypeId, m => m);

                latestSalaryByPeriod.TryGetValue((periodYear, periodMonth), out var salaryProcessId);
                var salaryByType = salarySummaries
                    .Where(s => s.ProcessId == salaryProcessId)
                    .ToDictionary(s => s.AssistantTypeId ?? unmappedKey, s => s);

                latestMeitarByPeriod.TryGetValue((periodYear, periodMonth), out var meitarProcessId);
                var meitarByType = meitarSummaries
                    .Where(s => s.ProcessId == meitarProcessId)
                    .ToDictionary(s => s.AssistantTypeId ?? unmappedKey, s => s);

                var typeKeys = budgetByType.Keys
                    .Concat(salaryByType.Keys)
                    .Concat(meitarByType.Keys)
                    .Distinct()
                    .ToList();

                foreach (var typeKey in typeKeys)
                {
                    budgetByType.TryGetValue(typeKey, out var budgetLine);
                    salaryByType.TryGetValue(typeKey, out var salaryLine);
                    meitarByType.TryGetValue(typeKey, out var meitarLine);
                    var typeId = typeKey == unmappedKey ? (int?)null : typeKey;

                    _context.YearlyBudgetComparisons.Add(new YearlyBudgetComparison
                    {
                        EntityId = budget.EntityId,
                        YearlyBudgetId = yearlyBudgetId,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        AssistantTypeId = typeId,
                        BudgetFte = budgetLine?.Fte ?? 0,
                        BudgetHours = budgetLine?.Hours ?? 0,
                        BudgetAmount = budgetLine?.Amount ?? 0,
                        SalaryRowCount = salaryLine?.RowCount ?? 0,
                        SalaryFte = salaryLine?.Fte ?? 0,
                        SalaryHours = salaryLine?.Hours ?? 0,
                        SalaryAmount = salaryLine?.Amount ?? 0,
                        SalaryProcessId = salaryLine != null ? salaryProcessId : null,
                        MeitarRowCount = meitarLine?.RowCount ?? 0,
                        MeitarHours = meitarLine?.Hours ?? 0,
                        MeitarAmount = meitarLine?.Amount ?? 0,
                        MeitarProcessId = meitarLine != null ? meitarProcessId : null,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task RebuildComparisonsForPeriodAsync(int periodYear, int periodMonth, int? userId)
        {
            var years = await _shared.HebrewYears.AsNoTracking()
                .Where(y => y.StartDate != null && y.EndDate != null)
                .ToListAsync();

            var coveringYearIds = years
                .Where(y => YearlyBudgetService.GetMonthsInYear(y).Any(m => m.Year == periodYear && m.Month == periodMonth))
                .Select(y => y.Id)
                .ToList();
            if (coveringYearIds.Count == 0)
                return;

            var budgetIds = await _context.YearlyBudgets
                .Where(b => coveringYearIds.Contains(b.HebrewYearId) && b.Status != YearlyBudgetStatuses.Deleted)
                .Select(b => b.Id)
                .ToListAsync();

            foreach (var budgetId in budgetIds)
                await RebuildBudgetComparisonsAsync(budgetId, userId);
        }

        private async Task EnsurePeriodSummariesAsync(int periodYear, int periodMonth, int? userId, bool force = false)
        {
            var salaryProcess = await _context.SalaryUploadProcesses
                .Where(p => p.PeriodYear == periodYear && p.PeriodMonth == periodMonth)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            if (salaryProcess != null)
            {
                var hasSalary = !force && await _context.SalaryMonthSummaries.AnyAsync(s => s.ProcessId == salaryProcess.Id);
                if (!hasSalary)
                    await RebuildSalarySummariesAsync(salaryProcess, userId);
            }

            var meitarProcess = await _context.MeitarRetrieveProcesses
                .Where(p => p.PeriodYear == periodYear && p.PeriodMonth == periodMonth)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            if (meitarProcess != null)
            {
                var hasMeitar = !force && await _context.MeitarMonthSummaries.AnyAsync(s => s.ProcessId == meitarProcess.Id);
                if (!hasMeitar)
                    await RebuildMeitarSummariesAsync(meitarProcess, userId);
            }
        }

        private async Task RebuildSalarySummariesAsync(SalaryUploadProcess process, int? userId)
        {
            var existing = await _context.SalaryMonthSummaries
                .Where(s => s.ProcessId == process.Id)
                .ToListAsync();
            if (existing.Count > 0)
                _context.SalaryMonthSummaries.RemoveRange(existing);

            var salaries = await _context.Salaries
                .Where(s => s.ProcessId == process.Id)
                .ToListAsync();

            var mappings = await _context.SalaryDepartmentMappings
                .AsNoTracking()
                .Where(m => m.IsActive)
                .ToListAsync();
            var mapByDept = mappings
                .GroupBy(m => m.DepartmentId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().AssistantTypeId, StringComparer.Ordinal);

            var typeHours = await _shared.AssistantTypes.AsNoTracking()
                .Select(t => new { t.Id, t.PositionHours })
                .ToDictionaryAsync(t => t.Id, t => t.PositionHours);

            var budget = await ResolveLockedBudgetAsync(process.PeriodYear, process.PeriodMonth);

            var groups = salaries
                .GroupBy(s => mapByDept.TryGetValue(s.DepartmentId, out var typeId) ? typeId : (int?)null)
                .ToList();

            var now = DateTime.UtcNow;
            foreach (var group in groups)
            {
                var fte = Round2(group.Sum(s => s.PositionPercentage) / 100m);
                decimal hours = 0;
                if (group.Key.HasValue &&
                    typeHours.TryGetValue(group.Key.Value, out var positionHours) &&
                    positionHours.HasValue)
                {
                    hours = Round2(fte * positionHours.Value);
                }

                var (hasBudget, budgetFte, budgetHours, budgetAmount) =
                    SnapshotBudget(budget, group.Key);

                _context.SalaryMonthSummaries.Add(new SalaryMonthSummary
                {
                    EntityId = process.EntityId,
                    ProcessId = process.Id,
                    PeriodYear = process.PeriodYear,
                    PeriodMonth = process.PeriodMonth,
                    AssistantTypeId = group.Key,
                    RowCount = group.Count(),
                    Fte = fte,
                    Hours = hours,
                    Amount = Round2(group.Sum(s => s.TotalSalary)),
                    YearlyBudgetId = hasBudget ? budget!.BudgetId : null,
                    BudgetFte = budgetFte,
                    BudgetHours = budgetHours,
                    BudgetAmount = budgetAmount,
                    HasBudget = hasBudget,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task RebuildMeitarSummariesAsync(MeitarRetrieveProcess process, int? userId)
        {
            var existing = await _context.MeitarMonthSummaries
                .Where(s => s.ProcessId == process.Id)
                .ToListAsync();
            if (existing.Count > 0)
                _context.MeitarMonthSummaries.RemoveRange(existing);

            var rows = await _context.MeitarMutavim
                .Where(m => m.ProcessId == process.Id)
                .ToListAsync();

            var topics = await _shared.MeitarTopics.AsNoTracking()
                .Where(t => t.IsActive && t.AssistantTypeId != null)
                .ToListAsync();
            var typeByTopicCode = topics
                .GroupBy(t => t.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().AssistantTypeId, StringComparer.Ordinal);

            var budget = await ResolveLockedBudgetAsync(process.PeriodYear, process.PeriodMonth);

            var groups = rows
                .GroupBy(r =>
                {
                    if (string.IsNullOrWhiteSpace(r.TopicCode))
                        return (int?)null;
                    return typeByTopicCode.TryGetValue(r.TopicCode, out var typeId) ? typeId : null;
                })
                .ToList();

            var now = DateTime.UtcNow;
            foreach (var group in groups)
            {
                var (hasBudget, budgetFte, budgetHours, budgetAmount) =
                    SnapshotBudget(budget, group.Key);

                _context.MeitarMonthSummaries.Add(new MeitarMonthSummary
                {
                    EntityId = process.EntityId,
                    ProcessId = process.Id,
                    PeriodYear = process.PeriodYear,
                    PeriodMonth = process.PeriodMonth,
                    AssistantTypeId = group.Key,
                    RowCount = group.Count(),
                    Fte = 0,
                    Hours = Round4(group.Sum(r => r.UnitCount ?? 0)),
                    Amount = Round2(group.Sum(r => r.CalculatedAmount)),
                    YearlyBudgetId = hasBudget ? budget!.BudgetId : null,
                    BudgetFte = budgetFte,
                    BudgetHours = budgetHours,
                    BudgetAmount = budgetAmount,
                    HasBudget = hasBudget,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task RebuildSalaryAnomaliesAsync(SalaryUploadProcess process, int? userId)
        {
            var salaries = await _context.Salaries
                .Where(s => s.ProcessId == process.Id)
                .ToListAsync();

            var mappings = await _context.SalaryDepartmentMappings
                .AsNoTracking()
                .Where(m => m.IsActive)
                .ToListAsync();
            var mapByDept = mappings
                .GroupBy(m => m.DepartmentId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().AssistantTypeId, StringComparer.Ordinal);

            var allocationTypeById = new Dictionary<int, int>();
            var allocationIds = salaries
                .Where(s => s.MatchedAllocationId.HasValue)
                .Select(s => s.MatchedAllocationId!.Value)
                .Distinct()
                .ToList();
            if (allocationIds.Count > 0)
            {
                allocationTypeById = await (
                    from a in _context.EntitlementAllocations.AsNoTracking()
                    join e in _context.Entitlements.AsNoTracking() on a.EntitlementId equals e.Id
                    where allocationIds.Contains(a.Id)
                    select new { a.Id, e.AssistantTypeId }
                ).ToDictionaryAsync(x => x.Id, x => x.AssistantTypeId);
            }

            var typeNames = await _shared.AssistantTypes.AsNoTracking()
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var newStatusId = await _shared.Statuses.AsNoTracking()
                .Where(s => s.Object == StatusObjects.SalaryAnomaly && s.Code == SalaryAnomalyStatusCodes.New)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            if (newStatusId == 0)
                throw new InvalidOperationException("סטטוס חריגת שכר 'חדש' לא נמצא בטבלת הסטטוסים");

            var existing = await _context.SalaryAnomalies
                .Where(a => a.ProcessId == process.Id)
                .ToListAsync();
            var existingBySalaryId = existing
                .Where(a => a.SalaryId.HasValue)
                .GroupBy(a => a.SalaryId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var keepSalaryIds = new HashSet<int>();
            var now = DateTime.UtcNow;

            foreach (var salary in salaries)
            {
                var mappedTypeId = mapByDept.TryGetValue(salary.DepartmentId, out var typeId)
                    ? typeId
                    : (int?)null;
                int? allocationTypeId = null;
                if (salary.MatchedAllocationId.HasValue &&
                    allocationTypeById.TryGetValue(salary.MatchedAllocationId.Value, out var allocType))
                {
                    allocationTypeId = allocType;
                }

                var reason = ResolvePrimaryReason(salary, mappedTypeId, allocationTypeId);
                if (reason == null)
                    continue;

                keepSalaryIds.Add(salary.Id);
                var message = BuildReasonMessage(reason, salary, mappedTypeId, allocationTypeId, typeNames);

                if (existingBySalaryId.TryGetValue(salary.Id, out var row))
                {
                    row.NationalId = salary.NationalId;
                    row.DepartmentId = salary.DepartmentId;
                    row.DepartmentName = salary.DepartmentName;
                    row.PositionPercentage = salary.PositionPercentage;
                    row.TotalSalary = salary.TotalSalary;
                    row.MatchedPersonId = salary.MatchedPersonId;
                    row.MatchedAllocationId = salary.MatchedAllocationId;
                    row.MappedAssistantTypeId = mappedTypeId;
                    row.AllocationAssistantTypeId = allocationTypeId;
                    row.ReasonCode = reason;
                    row.Message = message;
                    row.UpdatedAt = now;
                    row.UpdateUser = userId;
                    continue;
                }

                _context.SalaryAnomalies.Add(new SalaryAnomaly
                {
                    EntityId = process.EntityId,
                    ProcessId = process.Id,
                    SalaryId = salary.Id,
                    NationalId = salary.NationalId,
                    DepartmentId = salary.DepartmentId,
                    DepartmentName = salary.DepartmentName,
                    PositionPercentage = salary.PositionPercentage,
                    TotalSalary = salary.TotalSalary,
                    MatchedPersonId = salary.MatchedPersonId,
                    MatchedAllocationId = salary.MatchedAllocationId,
                    MappedAssistantTypeId = mappedTypeId,
                    AllocationAssistantTypeId = allocationTypeId,
                    ReasonCode = reason,
                    Message = message,
                    StatusId = newStatusId,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
            }

            var stale = existing.Where(a => !a.SalaryId.HasValue || !keepSalaryIds.Contains(a.SalaryId.Value)).ToList();
            if (stale.Count > 0)
                _context.SalaryAnomalies.RemoveRange(stale);

            await _context.SaveChangesAsync();
        }

        private static string? ResolvePrimaryReason(Salary salary, int? mappedTypeId, int? allocationTypeId)
        {
            if (mappedTypeId == null)
                return SalaryAnomalyReasons.UnmappedDepartment;
            if (salary.MatchedPersonId == null)
                return SalaryAnomalyReasons.UnmatchedPerson;
            if (salary.MatchedAllocationId == null)
                return SalaryAnomalyReasons.NoAllocationForPeriod;
            if (allocationTypeId.HasValue && allocationTypeId.Value != mappedTypeId.Value)
                return SalaryAnomalyReasons.TypeMismatch;
            if (salary.HasIdWarning)
                return SalaryAnomalyReasons.InvalidIdChecksum;
            return null;
        }

        private static string BuildReasonMessage(
            string reason,
            Salary salary,
            int? mappedTypeId,
            int? allocationTypeId,
            Dictionary<int, string> typeNames)
        {
            return reason switch
            {
                SalaryAnomalyReasons.UnmappedDepartment =>
                    $"מחלקה לא ממופה לסוג סייעת: {salary.DepartmentId}",
                SalaryAnomalyReasons.UnmatchedPerson =>
                    "לא נמצאה סייעת עם תעודת זהות זו",
                SalaryAnomalyReasons.NoAllocationForPeriod =>
                    "נמצאה סייעת ללא הקצאה פעילה לתקופה",
                SalaryAnomalyReasons.TypeMismatch =>
                    $"סוג הסייעת במחלקה ({NameOf(mappedTypeId, typeNames)}) שונה מסוג ההקצאה ({NameOf(allocationTypeId, typeNames)})",
                SalaryAnomalyReasons.InvalidIdChecksum =>
                    "ספרת ביקורת שגויה בתעודת זהות",
                _ => reason
            };
        }

        private static string NameOf(int? typeId, Dictionary<int, string> typeNames)
        {
            if (!typeId.HasValue)
                return "—";
            return typeNames.TryGetValue(typeId.Value, out var name) ? name : typeId.Value.ToString();
        }

        private async Task<LockedBudgetSnapshot?> ResolveLockedBudgetAsync(int periodYear, int periodMonth)
        {
            var years = await _shared.HebrewYears.AsNoTracking()
                .Where(y => y.StartDate != null && y.EndDate != null)
                .ToListAsync();

            var covering = years
                .Where(y => YearlyBudgetService.GetMonthsInYear(y).Any(m => m.Year == periodYear && m.Month == periodMonth))
                .OrderByDescending(y => y.IsCurrent)
                .ThenByDescending(y => y.IsPrevious)
                .ThenByDescending(y => y.Id)
                .ToList();

            foreach (var year in covering)
            {
                var budget = await _context.YearlyBudgets.AsNoTracking()
                    .Where(b => b.HebrewYearId == year.Id && b.Status == YearlyBudgetStatuses.Locked)
                    .OrderByDescending(b => b.Version)
                    .ThenByDescending(b => b.Id)
                    .Select(b => new { b.Id })
                    .FirstOrDefaultAsync();

                if (budget == null)
                    continue;

                var lines = await _context.YearlyBudgetMonthDetails.AsNoTracking()
                    .Where(m => m.YearlyBudgetId == budget.Id &&
                                m.PeriodYear == periodYear &&
                                m.PeriodMonth == periodMonth)
                    .ToListAsync();

                return new LockedBudgetSnapshot(budget.Id, lines);
            }

            return null;
        }

        private static (bool HasBudget, decimal? Fte, decimal? Hours, decimal? Amount) SnapshotBudget(
            LockedBudgetSnapshot? budget,
            int? assistantTypeId)
        {
            if (budget == null)
                return (false, null, null, null);
            if (!assistantTypeId.HasValue)
                return (true, 0, 0, 0);

            var line = budget.Lines.FirstOrDefault(l => l.AssistantTypeId == assistantTypeId.Value);
            if (line == null)
                return (true, 0, 0, 0);

            return (true, line.Fte, line.Hours, line.Amount);
        }

        private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

        private sealed record LockedBudgetSnapshot(int BudgetId, List<YearlyBudgetMonthDetail> Lines);
    }
}
