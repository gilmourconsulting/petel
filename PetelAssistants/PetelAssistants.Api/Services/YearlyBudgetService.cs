using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class YearlyBudgetService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly MonthlyImportComparisonService _comparisonService;
        private readonly ILogger<YearlyBudgetService> _logger;

        public YearlyBudgetService(
            AssistDbContext context,
            SharedDbContext sharedContext,
            MonthlyImportComparisonService comparisonService,
            ILogger<YearlyBudgetService> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _comparisonService = comparisonService;
            _logger = logger;
        }

        public async Task<YearlyBudgetDto> GetForYearAsync(int yearId)
        {
            var year = await LoadHebrewYearAsync(yearId);

            var last = await _context.YearlyBudgets
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.HebrewYearId == yearId
                                          && b.IsLastVersion
                                          && b.Status != YearlyBudgetStatuses.Deleted);

            if (last == null)
                return MapEmptyDto(year);

            return await MapDtoAsync(last.Id, year);
        }

        public async Task<YearlyBudgetDto?> GetByIdAsync(int id)
        {
            var budget = await _context.YearlyBudgets.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
            if (budget == null)
                return null;

            var year = await LoadHebrewYearAsync(budget.HebrewYearId, requireActive: false);
            return await MapDtoAsync(id, year);
        }

        /// <summary>
        /// Calculates yearly hours for class_help (rate matrix) and personal-level types
        /// (entitlement hours × participation %), then sets Amount = Hours × shared year hour value.
        /// Failures on class_help rows do not block successful rows. Missing hour value fails the whole calculate.
        /// </summary>
        public async Task<CalculateYearlyBudgetResultDto> CalculateAsync(int entityId, int? userId, int id)
        {
            var budget = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            EnsureEditable(budget);

            var year = await LoadHebrewYearAsync(budget.HebrewYearId, requireActive: false);
            var months = GetMonthsInYear(year);
            if (months.Count == 0)
                throw new InvalidOperationException("טווח תאריכי השנה אינו תקין");

            var hourValueRow = await _sharedContext.BudgetHourValues.AsNoTracking()
                .FirstOrDefaultAsync(r => r.HebrewYearId == budget.HebrewYearId);
            if (hourValueRow == null)
                throw new InvalidOperationException("לא הוגדר ערך שעה לשנה זו בניהול שנה");

            var hourValue = hourValueRow.HourValue;
            var failures = new List<CalculateBudgetFailureDto>();
            var entitlementCount = 0;
            var successCount = 0;
            decimal totalHours = 0;

            // ── class_help: rate matrix hours × entitlement participation ──
            var classHelpType = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == "class_help")
                ?? throw new InvalidOperationException("סוג סייעת כיתתית לא נמצא");

            var rates = await _sharedContext.ClassAssistantBudgetHours.AsNoTracking()
                .Where(r => r.HebrewYearId == budget.HebrewYearId)
                .ToListAsync();
            var rateLookup = rates.ToDictionary(
                r => (r.SchoolLevel, r.ClassClassificationId),
                r => r);

            var classEntitlements = await _context.Entitlements
                .AsNoTracking()
                .Include(e => e.Institution)
                .Where(e => e.HebrewYearId == budget.HebrewYearId
                            && e.AssistantTypeId == classHelpType.Id
                            && e.IsLastVersion
                            && !e.IsCancelled
                            && e.IsValid)
            .ToListAsync();

            entitlementCount += classEntitlements.Count;
            decimal classHelpHours = 0;

            foreach (var entitlement in classEntitlements)
            {
                var institutionName = entitlement.Institution?.Name;
                var className = entitlement.ClassName;

                if (entitlement.Institution == null || string.IsNullOrWhiteSpace(entitlement.Institution.SchoolLevel))
                {
                    failures.Add(MakeFailure(entitlement, institutionName, className, "חסרה רמת בית ספר במוסד"));
                    continue;
                }

                if (entitlement.ClassClassificationId is null or <= 0)
                {
                    failures.Add(MakeFailure(entitlement, institutionName, className, "חסר סיווג כיתה בזכאות"));
                    continue;
                }

                var schoolLevel = entitlement.Institution.SchoolLevel;
                if (!string.Equals(schoolLevel, SchoolLevels.Elementary, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(schoolLevel, SchoolLevels.HighSchool, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(MakeFailure(entitlement, institutionName, className, "רמת בית ספר במוסד אינה תקינה"));
                    continue;
                }

                var normalizedLevel = schoolLevel.Equals(SchoolLevels.HighSchool, StringComparison.OrdinalIgnoreCase)
                    ? SchoolLevels.HighSchool
                    : SchoolLevels.Elementary;

                if (!rateLookup.TryGetValue(
                        (normalizedLevel, entitlement.ClassClassificationId.Value),
                        out var rate))
                {
                    failures.Add(MakeFailure(entitlement, institutionName, className,
                        "לא הוגדרו שעות תקציב לרמת בית ספר ולסיווג כיתה אלה בניהול שנה"));
                    continue;
                }

                var effectiveHours = Round2(rate.Hours * entitlement.MinistryParticipationPct / 100m);
                classHelpHours += effectiveHours;
                successCount++;
            }

            classHelpHours = Round2(classHelpHours);
            var calculatedDetails = new Dictionary<int, YearlyBudgetDetail>();
            calculatedDetails[classHelpType.Id] =
                await UpsertDetailHoursAsync(entityId, userId, budget.Id, classHelpType.Id, classHelpHours);
            totalHours += classHelpHours;

            // ── personal types: entitlement Hours × participation, per type ──
            var personalTypes = await _sharedContext.AssistantTypes.AsNoTracking()
                .Where(t => t.Level == "personal")
                .ToListAsync();
            var personalTypeIds = personalTypes.Select(t => t.Id).ToList();

            if (personalTypeIds.Count > 0)
            {
                var personalEntitlements = await _context.Entitlements
                    .AsNoTracking()
                    .Where(e => e.HebrewYearId == budget.HebrewYearId
                                && personalTypeIds.Contains(e.AssistantTypeId)
                                && e.IsLastVersion
                                && !e.IsCancelled
                                && e.IsValid)
                    .ToListAsync();

                entitlementCount += personalEntitlements.Count;

                var hoursByType = personalTypeIds.ToDictionary(id => id, _ => 0m);
                foreach (var entitlement in personalEntitlements)
                {
                    var effectiveHours = Round2(entitlement.Hours * entitlement.MinistryParticipationPct / 100m);
                    hoursByType[entitlement.AssistantTypeId] =
                        Round2(hoursByType[entitlement.AssistantTypeId] + effectiveHours);
                    successCount++;
                }

                foreach (var typeId in personalTypeIds)
                {
                    var typeHours = Round2(hoursByType[typeId]);
                    calculatedDetails[typeId] =
                        await UpsertDetailHoursAsync(entityId, userId, budget.Id, typeId, typeHours);
                    totalHours += typeHours;
                }
            }

            totalHours = Round2(totalHours);
            var calculatedTypeIds = calculatedDetails.Keys.ToHashSet();

            // ── amounts + month rebuild for all calculated types ──
            decimal totalAmount = 0;

            var oldMonths = await _context.YearlyBudgetMonthDetails
                .Where(m => m.YearlyBudgetId == budget.Id && calculatedTypeIds.Contains(m.AssistantTypeId))
                .ToListAsync();
            _context.YearlyBudgetMonthDetails.RemoveRange(oldMonths);

            var now = DateTime.UtcNow;
            foreach (var detail in calculatedDetails.Values)
            {
                detail.Amount = Round2(detail.Hours * hourValue);
                detail.UpdatedAt = now;
                detail.UpdateUser = userId;
                totalAmount += detail.Amount;

                foreach (var (periodYear, periodMonth) in months)
                {
                    _context.YearlyBudgetMonthDetails.Add(new YearlyBudgetMonthDetail
                    {
                        EntityId = entityId,
                        YearlyBudgetId = budget.Id,
                        AssistantTypeId = detail.AssistantTypeId,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        Fte = Round2(detail.Fte / months.Count),
                        Hours = Round2(detail.Hours / months.Count),
                        Amount = Round2(detail.Amount / months.Count),
                        Remarks = detail.Remarks,
                        UserId = userId,
                        CreatedAt = now,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }
            }

            totalAmount = Round2(totalAmount);

            budget.UpdatedAt = now;
            budget.UpdateUser = userId;
            await _context.SaveChangesAsync();
            await _comparisonService.RebuildBudgetComparisonsAsync(budget.Id, userId);

            _logger.LogInformation(
                "Calculated budget {BudgetId}: totalHours={TotalHours}, totalAmount={TotalAmount}, success={Success}, failures={Failures}",
                budget.Id, totalHours, totalAmount, successCount, failures.Count);

            var budgetDto = await MapDtoAsync(budget.Id, year);
            return new CalculateYearlyBudgetResultDto
            {
                Budget = budgetDto,
                TotalHours = totalHours,
                TotalAmount = totalAmount,
                EntitlementCount = entitlementCount,
                SuccessCount = successCount,
                Failures = failures
            };
        }

        private async Task<YearlyBudgetDetail> UpsertDetailHoursAsync(
            int entityId, int? userId, int budgetId, int assistantTypeId, decimal hours)
        {
            var detail = await _context.YearlyBudgetDetails
                .FirstOrDefaultAsync(d => d.YearlyBudgetId == budgetId && d.AssistantTypeId == assistantTypeId);

            if (detail == null)
            {
                detail = new YearlyBudgetDetail
                {
                    EntityId = entityId,
                    YearlyBudgetId = budgetId,
                    AssistantTypeId = assistantTypeId,
                    Fte = 0,
                    Amount = 0,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.YearlyBudgetDetails.Add(detail);
            }

            detail.Hours = hours;
            detail.UpdatedAt = DateTime.UtcNow;
            detail.UpdateUser = userId;
            return detail;
        }

        private static CalculateBudgetFailureDto MakeFailure(
            Entitlement entitlement, string? institutionName, string? className, string reason)
            => new()
            {
                EntitlementId = entitlement.Id,
                MasterEntitlementId = entitlement.MasterEntitlementId,
                InstitutionName = institutionName,
                ClassName = className,
                Reason = reason
            };

        public async Task<YearlyBudgetDto> SaveAsync(int entityId, int? userId, int id, UpdateYearlyBudgetRequest request)
        {
            var budget = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            EnsureEditable(budget);

            var year = await LoadHebrewYearAsync(budget.HebrewYearId, requireActive: false);
            var months = GetMonthsInYear(year);
            if (months.Count == 0)
                throw new InvalidOperationException("טווח תאריכי השנה אינו תקין");

            var activeTypeIds = await _sharedContext.AssistantTypes.AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .ToListAsync();

            var existingDetails = await _context.YearlyBudgetDetails
                .Where(d => d.YearlyBudgetId == budget.Id)
                .ToListAsync();

            var requestByType = request.Details
                .GroupBy(d => d.AssistantTypeId)
                .ToDictionary(g => g.Key, g => g.Last());

            var workingDetails = new List<YearlyBudgetDetail>();

            foreach (var typeId in activeTypeIds)
            {
                requestByType.TryGetValue(typeId, out var line);
                var fte = line?.Fte ?? 0;
                var hours = line?.Hours ?? 0;
                var amount = line?.Amount ?? 0;
                var remarks = line?.Remarks;

                if (fte < 0 || hours < 0 || amount < 0)
                    throw new InvalidOperationException("ערכי תקציב חייבים להיות אי-שליליים");

                var detail = existingDetails.FirstOrDefault(d => d.AssistantTypeId == typeId);
                if (detail == null)
                {
                    detail = new YearlyBudgetDetail
                    {
                        EntityId = entityId,
                        YearlyBudgetId = budget.Id,
                        AssistantTypeId = typeId,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.YearlyBudgetDetails.Add(detail);
                }

                detail.Fte = Round2(fte);
                detail.Hours = Round2(hours);
                detail.Amount = Round2(amount);
                detail.Remarks = NormalizeRemarks(remarks);
                detail.UpdatedAt = DateTime.UtcNow;
                detail.UpdateUser = userId;
                workingDetails.Add(detail);
            }

            var oldMonths = await _context.YearlyBudgetMonthDetails
                .Where(m => m.YearlyBudgetId == budget.Id)
                .ToListAsync();
            _context.YearlyBudgetMonthDetails.RemoveRange(oldMonths);

            foreach (var detail in workingDetails)
            {
                foreach (var (periodYear, periodMonth) in months)
                {
                    _context.YearlyBudgetMonthDetails.Add(new YearlyBudgetMonthDetail
                    {
                        EntityId = entityId,
                        YearlyBudgetId = budget.Id,
                        AssistantTypeId = detail.AssistantTypeId,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        Fte = Round2(detail.Fte / months.Count),
                        Hours = Round2(detail.Hours / months.Count),
                        Amount = Round2(detail.Amount / months.Count),
                        Remarks = detail.Remarks,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUser = userId
                    });
                }
            }

            budget.UpdatedAt = DateTime.UtcNow;
            budget.UpdateUser = userId;

            await _context.SaveChangesAsync();
            await _comparisonService.RebuildBudgetComparisonsAsync(budget.Id, userId);
            _logger.LogInformation("Saved yearly budget {BudgetId} version {Version}", budget.Id, budget.Version);
            return await MapDtoAsync(budget.Id, year);
        }

        public async Task<YearlyBudgetDto> RecalculateSummariesAsync(int? userId, int id)
        {
            var budget = await _context.YearlyBudgets.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            if (budget.Status == YearlyBudgetStatuses.Deleted)
                throw new InvalidOperationException("לא ניתן לחשב סיכומים לגרסה מחוקה");

            var year = await LoadHebrewYearAsync(budget.HebrewYearId, requireActive: false);
            await _comparisonService.RecalculateYearSummariesAsync(id, userId);
            return await MapDtoAsync(id, year);
        }

        public async Task<YearlyBudgetDto> LockAsync(int? userId, int id)
        {
            var budget = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            if (!budget.IsLastVersion || budget.Status != YearlyBudgetStatuses.Open)
                throw new InvalidOperationException("ניתן לנעול רק את הגרסה הפתוחה האחרונה");

            budget.Status = YearlyBudgetStatuses.Locked;
            budget.UpdatedAt = DateTime.UtcNow;
            budget.UpdateUser = userId;
            await _context.SaveChangesAsync();

            var year = await LoadHebrewYearAsync(budget.HebrewYearId, requireActive: false);
            return await MapDtoAsync(budget.Id, year);
        }

        /// <summary>
        /// Creates the first version (0) when none exist, or the next version from a locked last version.
        /// </summary>
        public async Task<YearlyBudgetDto> CreateNewVersionForYearAsync(int entityId, int? userId, int yearId)
        {
            var year = await LoadHebrewYearAsync(yearId);

            var last = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.HebrewYearId == yearId
                                          && b.IsLastVersion
                                          && b.Status != YearlyBudgetStatuses.Deleted);

            if (last == null)
            {
                var created = await CreateInitialVersionAsync(entityId, userId, year);
                return await MapDtoAsync(created.Id, year);
            }

            if (last.Status != YearlyBudgetStatuses.Locked)
                throw new InvalidOperationException("ניתן ליצור גרסה חדשה רק כאשר אין גרסה, או כאשר הגרסה האחרונה נעולה");

            return await CreateNewVersionFromLockedAsync(entityId, userId, last, year);
        }

        public async Task<YearlyBudgetDto> CreateNewVersionAsync(int entityId, int? userId, int id)
        {
            var source = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            if (!source.IsLastVersion || source.Status != YearlyBudgetStatuses.Locked)
                throw new InvalidOperationException("ניתן ליצור גרסה חדשה רק מגרסה נעולה אחרונה");

            var year = await LoadHebrewYearAsync(source.HebrewYearId, requireActive: false);
            return await CreateNewVersionFromLockedAsync(entityId, userId, source, year);
        }

        private async Task<YearlyBudgetDto> CreateNewVersionFromLockedAsync(
            int entityId, int? userId, YearlyBudget source, HebrewYear year)
        {

            var sourceDetails = await _context.YearlyBudgetDetails
                .AsNoTracking()
                .Where(d => d.YearlyBudgetId == source.Id)
                .ToListAsync();

            var sourceMonths = await _context.YearlyBudgetMonthDetails
                .AsNoTracking()
                .Where(m => m.YearlyBudgetId == source.Id)
                .ToListAsync();

            source.IsLastVersion = false;
            source.UpdatedAt = DateTime.UtcNow;
            source.UpdateUser = userId;

            var next = new YearlyBudget
            {
                EntityId = entityId,
                HebrewYearId = source.HebrewYearId,
                MasterYearlyBudgetId = source.MasterYearlyBudgetId,
                Version = source.Version + 1,
                IsLastVersion = true,
                Status = YearlyBudgetStatuses.Open,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdateUser = userId
            };
            _context.YearlyBudgets.Add(next);
            await _context.SaveChangesAsync();

            foreach (var d in sourceDetails)
            {
                _context.YearlyBudgetDetails.Add(new YearlyBudgetDetail
                {
                    EntityId = entityId,
                    YearlyBudgetId = next.Id,
                    AssistantTypeId = d.AssistantTypeId,
                    Fte = d.Fte,
                    Hours = d.Hours,
                    Amount = d.Amount,
                    Remarks = d.Remarks,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = userId
                });
            }

            foreach (var m in sourceMonths)
            {
                _context.YearlyBudgetMonthDetails.Add(new YearlyBudgetMonthDetail
                {
                    EntityId = entityId,
                    YearlyBudgetId = next.Id,
                    AssistantTypeId = m.AssistantTypeId,
                    PeriodYear = m.PeriodYear,
                    PeriodMonth = m.PeriodMonth,
                    Fte = m.Fte,
                    Hours = m.Hours,
                    Amount = m.Amount,
                    Remarks = m.Remarks,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = userId
                });
            }

            await _context.SaveChangesAsync();
            await _comparisonService.RebuildBudgetComparisonsAsync(next.Id, userId);
            _logger.LogInformation(
                "Created yearly budget version {Version} from {SourceId} as {NewId}",
                next.Version, source.Id, next.Id);

            return await MapDtoAsync(next.Id, year);
        }

        public async Task<YearlyBudgetDto?> DeleteAsync(int? userId, int id)
        {
            var budget = await _context.YearlyBudgets
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw new InvalidOperationException("תקציב לא נמצא");

            if (budget.Status == YearlyBudgetStatuses.Deleted)
                throw new InvalidOperationException("הגרסה כבר נמחקה");

            var wasLast = budget.IsLastVersion;
            budget.Status = YearlyBudgetStatuses.Deleted;
            budget.IsLastVersion = false;
            budget.UpdatedAt = DateTime.UtcNow;
            budget.UpdateUser = userId;

            YearlyBudget? promoted = null;
            if (wasLast)
            {
                promoted = await _context.YearlyBudgets
                    .Where(b => b.HebrewYearId == budget.HebrewYearId
                                && b.MasterYearlyBudgetId == budget.MasterYearlyBudgetId
                                && b.Id != budget.Id
                                && b.Status != YearlyBudgetStatuses.Deleted)
                    .OrderByDescending(b => b.Version)
                    .FirstOrDefaultAsync();

                if (promoted != null)
                {
                    promoted.IsLastVersion = true;
                    promoted.UpdatedAt = DateTime.UtcNow;
                    promoted.UpdateUser = userId;
                }
            }

            await _context.SaveChangesAsync();

            if (promoted != null)
            {
                var year = await LoadHebrewYearAsync(promoted.HebrewYearId, requireActive: false);
                return await MapDtoAsync(promoted.Id, year);
            }

            return null;
        }

        private async Task<YearlyBudget> CreateInitialVersionAsync(int entityId, int? userId, HebrewYear year)
        {
            var existing = await _context.YearlyBudgets
                .Where(b => b.HebrewYearId == year.Id)
                .OrderByDescending(b => b.Version)
                .FirstOrDefaultAsync();

            var masterId = existing?.MasterYearlyBudgetId ?? 0;
            var nextVersion = existing != null ? existing.Version + 1 : 0;

            // Clear any stale last-version flag (e.g. all remaining rows deleted)
            var staleLast = await _context.YearlyBudgets
                .Where(b => b.HebrewYearId == year.Id && b.IsLastVersion)
                .ToListAsync();
            foreach (var row in staleLast)
                row.IsLastVersion = false;

            var budget = new YearlyBudget
            {
                EntityId = entityId,
                HebrewYearId = year.Id,
                MasterYearlyBudgetId = masterId,
                Version = nextVersion,
                IsLastVersion = true,
                Status = YearlyBudgetStatuses.Open,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdateUser = userId
            };
            _context.YearlyBudgets.Add(budget);
            await _context.SaveChangesAsync();

            if (budget.MasterYearlyBudgetId == 0)
            {
                budget.MasterYearlyBudgetId = budget.Id;
                await _context.SaveChangesAsync();
            }

            var types = await _sharedContext.AssistantTypes.AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.DisplayName)
                .ToListAsync();

            var months = GetMonthsInYear(year);
            if (months.Count == 0)
                throw new InvalidOperationException("טווח תאריכי השנה אינו תקין");

            foreach (var type in types)
            {
                _context.YearlyBudgetDetails.Add(new YearlyBudgetDetail
                {
                    EntityId = entityId,
                    YearlyBudgetId = budget.Id,
                    AssistantTypeId = type.Id,
                    Fte = 0,
                    Hours = 0,
                    Amount = 0,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = userId
                });

                foreach (var (periodYear, periodMonth) in months)
                {
                    _context.YearlyBudgetMonthDetails.Add(new YearlyBudgetMonthDetail
                    {
                        EntityId = entityId,
                        YearlyBudgetId = budget.Id,
                        AssistantTypeId = type.Id,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        Fte = 0,
                        Hours = 0,
                        Amount = 0,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUser = userId
                    });
                }
            }

            await _context.SaveChangesAsync();
            await _comparisonService.RebuildBudgetComparisonsAsync(budget.Id, userId);
            _logger.LogInformation(
                "Created yearly budget {BudgetId} v{Version} for year {YearId}",
                budget.Id, budget.Version, year.Id);

            return budget;
        }

        private async Task<YearlyBudgetDto> MapDtoAsync(int budgetId, HebrewYear year)
        {
            var budget = await _context.YearlyBudgets.AsNoTracking()
                .FirstAsync(b => b.Id == budgetId);

            var versions = await _context.YearlyBudgets.AsNoTracking()
                .Where(b => b.MasterYearlyBudgetId == budget.MasterYearlyBudgetId)
                .OrderByDescending(b => b.Version)
                .Select(b => new YearlyBudgetVersionItemDto
                {
                    Id = b.Id,
                    Version = b.Version,
                    Status = b.Status,
                    IsLastVersion = b.IsLastVersion,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

            var details = await _context.YearlyBudgetDetails.AsNoTracking()
                .Where(d => d.YearlyBudgetId == budgetId)
                .ToListAsync();

            var monthDetails = await _context.YearlyBudgetMonthDetails.AsNoTracking()
                .Where(m => m.YearlyBudgetId == budgetId)
                .OrderBy(m => m.PeriodYear)
                .ThenBy(m => m.PeriodMonth)
                .ThenBy(m => m.AssistantTypeId)
                .ToListAsync();

            var comparisons = await _context.YearlyBudgetComparisons.AsNoTracking()
                .Where(c => c.YearlyBudgetId == budgetId)
                .ToListAsync();
            if (comparisons.Count == 0 && budget.Status != YearlyBudgetStatuses.Deleted)
            {
                await _comparisonService.RebuildBudgetComparisonsAsync(budgetId, null);
                comparisons = await _context.YearlyBudgetComparisons.AsNoTracking()
                    .Where(c => c.YearlyBudgetId == budgetId)
                    .ToListAsync();
            }

            var typeIds = details.Select(d => d.AssistantTypeId)
                .Concat(monthDetails.Select(m => m.AssistantTypeId))
                .Concat(comparisons.Where(c => c.AssistantTypeId.HasValue).Select(c => c.AssistantTypeId!.Value))
                .Distinct()
                .ToList();

            var typeNames = await _sharedContext.AssistantTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var typeOrder = await _sharedContext.AssistantTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.Id))
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.DisplayName)
                .Select(t => t.Id)
                .ToListAsync();

            var isLastNonDeleted = budget.IsLastVersion && budget.Status != YearlyBudgetStatuses.Deleted;

            return new YearlyBudgetDto
            {
                Id = budget.Id,
                MasterYearlyBudgetId = budget.MasterYearlyBudgetId,
                HebrewYearId = budget.HebrewYearId,
                HebrewYearName = year.YearName,
                Version = budget.Version,
                Status = budget.Status,
                IsLastVersion = budget.IsLastVersion,
                CanEdit = isLastNonDeleted && budget.Status == YearlyBudgetStatuses.Open,
                CanLock = isLastNonDeleted && budget.Status == YearlyBudgetStatuses.Open,
                CanCreateNewVersion = isLastNonDeleted && budget.Status == YearlyBudgetStatuses.Locked,
                CanDelete = budget.Status != YearlyBudgetStatuses.Deleted,
                Versions = versions,
                Details = details
                    .OrderBy(d => typeOrder.IndexOf(d.AssistantTypeId))
                    .Select(d => new YearlyBudgetDetailDto
                    {
                        Id = d.Id,
                        AssistantTypeId = d.AssistantTypeId,
                        AssistantTypeName = typeNames.TryGetValue(d.AssistantTypeId, out var n) ? n : string.Empty,
                        Fte = d.Fte,
                        Hours = d.Hours,
                        Amount = d.Amount,
                        Remarks = d.Remarks
                    })
                    .ToList(),
                MonthDetails = monthDetails.Select(m => new YearlyBudgetMonthDetailDto
                {
                    Id = m.Id,
                    AssistantTypeId = m.AssistantTypeId,
                    AssistantTypeName = typeNames.TryGetValue(m.AssistantTypeId, out var n) ? n : string.Empty,
                    PeriodYear = m.PeriodYear,
                    PeriodMonth = m.PeriodMonth,
                    Fte = m.Fte,
                    Hours = m.Hours,
                    Amount = m.Amount,
                    Remarks = m.Remarks
                }).ToList(),
                Comparisons = comparisons
                    .OrderBy(c => c.PeriodYear)
                    .ThenBy(c => c.PeriodMonth)
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

        private static YearlyBudgetDto MapEmptyDto(HebrewYear year)
        {
            return new YearlyBudgetDto
            {
                Id = 0,
                MasterYearlyBudgetId = 0,
                HebrewYearId = year.Id,
                HebrewYearName = year.YearName,
                Version = 0,
                Status = string.Empty,
                IsLastVersion = false,
                CanEdit = false,
                CanLock = false,
                CanCreateNewVersion = true,
                CanDelete = false,
                Versions = new List<YearlyBudgetVersionItemDto>(),
                Details = new List<YearlyBudgetDetailDto>(),
                MonthDetails = new List<YearlyBudgetMonthDetailDto>(),
                Comparisons = new List<YearlyBudgetComparisonDto>()
            };
        }

        private static void EnsureEditable(YearlyBudget budget)
        {
            if (!budget.IsLastVersion || budget.Status != YearlyBudgetStatuses.Open)
                throw new InvalidOperationException("ניתן לערוך רק גרסה פתוחה אחרונה");
        }

        private async Task<HebrewYear> LoadHebrewYearAsync(int yearId, bool requireActive = true)
        {
            var year = await _sharedContext.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId)
                ?? throw new InvalidOperationException("שנה לא נמצאה");

            if (requireActive && !year.IsActive)
                throw new InvalidOperationException("השנה אינה פעילה");

            if (year.StartDate is null || year.EndDate is null)
                throw new InvalidOperationException("לתאריכי השנה חסרים ערכים");

            return year;
        }

        /// <summary>
        /// Consecutive Gregorian months from the Hebrew year start month through end month (inclusive).
        /// </summary>
        internal static List<(int Year, int Month)> GetMonthsInYear(HebrewYear year)
        {
            if (year.StartDate is null || year.EndDate is null)
                return new List<(int, int)>();

            var start = new DateOnly(year.StartDate.Value.Year, year.StartDate.Value.Month, 1);
            var end = new DateOnly(year.EndDate.Value.Year, year.EndDate.Value.Month, 1);
            if (end < start)
                return new List<(int, int)>();

            var months = new List<(int, int)>();
            for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
                months.Add((cursor.Year, cursor.Month));

            return months;
        }

        internal static List<(int Year, int Month)> GetCalendarMonths(int calendarYear)
        {
            var months = new List<(int, int)>(12);
            for (var month = 1; month <= 12; month++)
                months.Add((calendarYear, month));
            return months;
        }

        internal static DateOnly CalendarYearStart(int calendarYear) => new(calendarYear, 1, 1);

        internal static DateOnly CalendarYearEnd(int calendarYear) => new(calendarYear, 12, 31);

        internal static bool YearCoversMonth(HebrewYear year, int periodYear, int periodMonth)
        {
            if (year.StartDate is null || year.EndDate is null)
                return false;

            var monthStart = new DateOnly(periodYear, periodMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            return year.StartDate.Value <= monthEnd && year.EndDate.Value >= monthStart;
        }

        internal static HebrewYear? FindYearForMonth(IEnumerable<HebrewYear> years, int periodYear, int periodMonth)
        {
            var mid = new DateOnly(periodYear, periodMonth, 15);
            return years.FirstOrDefault(y =>
                       y.StartDate is not null && y.EndDate is not null
                       && y.StartDate.Value <= mid && y.EndDate.Value >= mid)
                   ?? years.FirstOrDefault(y => YearCoversMonth(y, periodYear, periodMonth));
        }

        internal static List<HebrewYear> GetHebrewYearsCoveringCalendarYear(
            IEnumerable<HebrewYear> years, int calendarYear)
        {
            var start = CalendarYearStart(calendarYear);
            var end = CalendarYearEnd(calendarYear);
            return years
                .Where(y => y.StartDate is not null && y.EndDate is not null
                            && y.StartDate.Value <= end && y.EndDate.Value >= start)
                .OrderBy(y => y.StartDate)
                .ToList();
        }

        private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string? NormalizeRemarks(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                return null;
            return remarks.Trim();
        }
    }
}
