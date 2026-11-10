using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs.Meitar;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class MeitarMucarimEntitlementMatchService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _shared;
        private readonly ILogger<MeitarMucarimEntitlementMatchService> _logger;

        public MeitarMucarimEntitlementMatchService(
            AssistDbContext context,
            SharedDbContext shared,
            ILogger<MeitarMucarimEntitlementMatchService> logger)
        {
            _context = context;
            _shared = shared;
            _logger = logger;
        }

        public async Task<MeitarMucarimEntitlementMatchResultDto> MatchPeriodAsync(
            int entityId,
            int periodYear,
            int periodMonth,
            int? userId,
            string matchTrigger = MeitarEntitlementMatchTriggers.PeriodRecheck,
            CancellationToken cancellationToken = default)
        {
            if (periodMonth < 1 || periodMonth > 12)
                throw new ArgumentOutOfRangeException(nameof(periodMonth));

            var lines = await _context.MeitarMucarim
                .Where(m => m.PeriodYear == periodYear && m.PeriodMonth == periodMonth)
                .ToListAsync(cancellationToken);

            var lineIds = lines.Select(l => l.Id).ToList();

            var existing = lineIds.Count == 0
                ? new List<MeitarMucarimEntitlementMatch>()
                : await _context.MeitarMucarimEntitlementMatches
                    .Where(m => m.IsActive && lineIds.Contains(m.MeitarMucarimId))
                    .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var disabledCount = DisableMatches(existing, userId, now);

            var process = new MeitarEntitlementMatchProcess
            {
                EntityId = entityId,
                Source = MeitarEntitlementMatchSources.Mucarim,
                MatchTrigger = matchTrigger,
                PeriodYear = periodYear,
                PeriodMonth = periodMonth,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.MeitarEntitlementMatchProcesses.Add(process);
            await _context.SaveChangesAsync(cancellationToken);

            var (matchedCount, unmatchedLineCount) = await BuildMatchesForLinesAsync(
                entityId, lines, process.Id, userId, now, cancellationToken);

            process.DisabledCount = disabledCount;
            process.MatchedCount = matchedCount;
            process.UnmatchedLineCount = unmatchedLineCount;
            process.UpdatedAt = DateTime.UtcNow;
            process.UpdateUser = userId;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "MUCARIM entitlement match period {Year}/{Month}: disabled={Disabled}, matched={Matched}, unmatchedLines={Unmatched}",
                periodYear, periodMonth, disabledCount, matchedCount, unmatchedLineCount);

            return new MeitarMucarimEntitlementMatchResultDto
            {
                ProcessId = process.Id,
                DisabledCount = disabledCount,
                MatchedCount = matchedCount,
                UnmatchedLineCount = unmatchedLineCount
            };
        }

        public async Task<MeitarMucarimEntitlementMatchResultDto> MatchEntitlementAsync(
            int entityId,
            int entitlementId,
            int? userId,
            CancellationToken cancellationToken = default)
        {
            var entitlement = await _context.Entitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entitlementId, cancellationToken)
                ?? throw new InvalidOperationException("הזכאות לא נמצאה");

            if (!entitlement.IsLastVersion || entitlement.IsCancelled || !entitlement.IsValid)
                throw new InvalidOperationException("ניתן להתאים רק זכאות פעילה ותקינה בגרסה אחרונה");

            if (entitlement.InstitutionId == null)
                throw new InvalidOperationException("לזכאות אין מוסד מקושר");

            var existing = await _context.MeitarMucarimEntitlementMatches
                .Where(m => m.IsActive && m.EntitlementId == entitlementId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var disabledCount = DisableMatches(existing, userId, now);

            var process = new MeitarEntitlementMatchProcess
            {
                EntityId = entityId,
                Source = MeitarEntitlementMatchSources.Mucarim,
                MatchTrigger = MeitarEntitlementMatchTriggers.Entitlement,
                EntitlementId = entitlementId,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.MeitarEntitlementMatchProcesses.Add(process);
            await _context.SaveChangesAsync(cancellationToken);

            var topicCodeSet = (await _shared.MeitarTopics
                .AsNoTracking()
                .Where(t => t.IsActive && t.AssistantTypeId == entitlement.AssistantTypeId)
                .Select(t => t.Code)
                .ToListAsync(cancellationToken))
                .Select(c => c.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var symbol = await ResolveInstitutionSymbolAsync(
                entitlement.InstitutionId.Value, entitlement.HebrewYearId, cancellationToken);

            var lines = new List<MeitarMucarim>();
            if (topicCodeSet.Count > 0 && !string.IsNullOrWhiteSpace(symbol))
            {
                var symbolKey = NormalizeSymbol(symbol);
                var candidates = await _context.MeitarMucarim
                    .Where(m => m.EffectiveDate != null
                                && m.TopicCode != null
                                && m.InstitutionCode != null)
                    .ToListAsync(cancellationToken);

                lines = candidates
                    .Where(m =>
                    {
                        if (!topicCodeSet.Contains(m.TopicCode!.Trim()))
                            return false;
                        if (!string.Equals(NormalizeSymbol(m.InstitutionCode!), symbolKey, StringComparison.OrdinalIgnoreCase))
                            return false;
                        var eff = m.EffectiveDate!.Value;
                        var effMonthStart = new DateOnly(eff.Year, eff.Month, 1);
                        var effMonthEnd = effMonthStart.AddMonths(1).AddDays(-1);
                        return entitlement.StartDate <= effMonthEnd && entitlement.EndDate >= effMonthStart;
                    })
                    .ToList();
            }

            var matchedCount = 0;
            foreach (var line in lines)
            {
                _context.MeitarMucarimEntitlementMatches.Add(new MeitarMucarimEntitlementMatch
                {
                    EntityId = entityId,
                    MeitarMucarimId = line.Id,
                    EntitlementId = entitlementId,
                    MatchProcessId = process.Id,
                    IsActive = true,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
                matchedCount++;
            }

            process.DisabledCount = disabledCount;
            process.MatchedCount = matchedCount;
            process.UnmatchedLineCount = 0;
            process.UpdatedAt = DateTime.UtcNow;
            process.UpdateUser = userId;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "MUCARIM entitlement match for entitlement {EntitlementId}: disabled={Disabled}, matched={Matched}",
                entitlementId, disabledCount, matchedCount);

            return new MeitarMucarimEntitlementMatchResultDto
            {
                ProcessId = process.Id,
                DisabledCount = disabledCount,
                MatchedCount = matchedCount,
                UnmatchedLineCount = 0
            };
        }

        public async Task<List<MeitarMucarimEntitlementMatchListItemDto>> GetActiveByPeriodAsync(
            int periodYear,
            int periodMonth,
            CancellationToken cancellationToken = default)
        {
            var rows = await (
                from match in _context.MeitarMucarimEntitlementMatches.AsNoTracking()
                join mucarim in _context.MeitarMucarim.AsNoTracking() on match.MeitarMucarimId equals mucarim.Id
                join ent in _context.Entitlements.AsNoTracking() on match.EntitlementId equals ent.Id
                where match.IsActive
                      && mucarim.PeriodYear == periodYear
                      && mucarim.PeriodMonth == periodMonth
                select new { match, mucarim, ent })
                .ToListAsync(cancellationToken);

            return await MapListItemsAsync(rows.Select(r => (r.match, r.mucarim, r.ent)).ToList(), cancellationToken);
        }

        public async Task<List<MeitarMucarimEntitlementMatchListItemDto>> GetActiveByEntitlementAsync(
            int entitlementId,
            CancellationToken cancellationToken = default)
        {
            var rows = await (
                from match in _context.MeitarMucarimEntitlementMatches.AsNoTracking()
                join mucarim in _context.MeitarMucarim.AsNoTracking() on match.MeitarMucarimId equals mucarim.Id
                join ent in _context.Entitlements.AsNoTracking() on match.EntitlementId equals ent.Id
                where match.IsActive && match.EntitlementId == entitlementId
                select new { match, mucarim, ent })
                .ToListAsync(cancellationToken);

            return await MapListItemsAsync(rows.Select(r => (r.match, r.mucarim, r.ent)).ToList(), cancellationToken);
        }

        public async Task<Dictionary<int, int>> GetActiveMatchCountsAsync(
            IEnumerable<int> mucarimIds,
            CancellationToken cancellationToken = default)
        {
            var ids = mucarimIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, int>();

            return await _context.MeitarMucarimEntitlementMatches
                .AsNoTracking()
                .Where(m => m.IsActive && ids.Contains(m.MeitarMucarimId))
                .GroupBy(m => m.MeitarMucarimId)
                .Select(g => new { MeitarMucarimId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MeitarMucarimId, x => x.Count, cancellationToken);
        }

        private async Task<(int matchedCount, int unmatchedLineCount)> BuildMatchesForLinesAsync(
            int entityId,
            List<MeitarMucarim> lines,
            int processId,
            int? userId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (lines.Count == 0)
                return (0, 0);

            var typeByTopic = (await _shared.MeitarTopics
                .AsNoTracking()
                .Where(t => t.IsActive && t.AssistantTypeId != null)
                .Select(t => new { t.Code, t.AssistantTypeId })
                .ToListAsync(cancellationToken))
                .GroupBy(t => t.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().AssistantTypeId!.Value, StringComparer.OrdinalIgnoreCase);

            var hebrewYears = await _shared.HebrewYears
                .AsNoTracking()
                .Where(y => y.StartDate != null && y.EndDate != null)
                .ToListAsync(cancellationToken);

            var yearRoster = await _context.InstitutionYearSettings
                .AsNoTracking()
                .Where(s => s.Symbol != null && s.Symbol != string.Empty)
                .Select(s => new { s.InstitutionId, s.Symbol, s.HebrewYearId })
                .ToListAsync(cancellationToken);
            var rosterByYearSymbol = yearRoster
                .GroupBy(s => (s.HebrewYearId, Symbol: NormalizeSymbol(s.Symbol!)))
                .ToDictionary(g => g.Key, g => g.First().InstitutionId);

            var masterBySymbol = (await _context.Institutions
                .AsNoTracking()
                .Where(i => i.Symbol != null && i.Symbol != string.Empty)
                .Select(i => new { i.Id, i.Symbol })
                .ToListAsync(cancellationToken))
                .GroupBy(i => NormalizeSymbol(i.Symbol!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var entitlements = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.IsLastVersion && !e.IsCancelled && e.IsValid && e.InstitutionId != null)
                .Select(e => new
                {
                    e.Id,
                    e.AssistantTypeId,
                    e.InstitutionId,
                    e.HebrewYearId,
                    e.StartDate,
                    e.EndDate
                })
                .ToListAsync(cancellationToken);

            var entitlementsByKey = entitlements
                .GroupBy(e => (e.AssistantTypeId, e.InstitutionId!.Value, e.HebrewYearId))
                .ToDictionary(g => g.Key, g => g.ToList());

            var matchedCount = 0;
            var unmatchedLineCount = 0;

            foreach (var line in lines)
            {
                if (line.EffectiveDate == null
                    || string.IsNullOrWhiteSpace(line.TopicCode)
                    || string.IsNullOrWhiteSpace(line.InstitutionCode)
                    || !typeByTopic.TryGetValue(line.TopicCode.Trim(), out var assistantTypeId))
                {
                    unmatchedLineCount++;
                    continue;
                }

                var effective = line.EffectiveDate.Value;
                var hebrewYearId = hebrewYears
                    .FirstOrDefault(y => y.StartDate!.Value <= effective && effective <= y.EndDate!.Value)
                    ?.Id;
                if (hebrewYearId == null)
                {
                    unmatchedLineCount++;
                    continue;
                }

                var normalized = NormalizeSymbol(line.InstitutionCode);
                int? institutionId = null;
                if (rosterByYearSymbol.TryGetValue((hebrewYearId.Value, normalized), out var yearMatchedId))
                    institutionId = yearMatchedId;
                else if (masterBySymbol.TryGetValue(normalized, out var masterMatchedId))
                    institutionId = masterMatchedId;

                if (institutionId == null)
                {
                    unmatchedLineCount++;
                    continue;
                }

                if (!entitlementsByKey.TryGetValue((assistantTypeId, institutionId.Value, hebrewYearId.Value), out var candidates))
                {
                    unmatchedLineCount++;
                    continue;
                }

                var monthStart = new DateOnly(effective.Year, effective.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var matches = candidates
                    .Where(e => e.StartDate <= monthEnd && e.EndDate >= monthStart)
                    .ToList();

                if (matches.Count == 0)
                {
                    unmatchedLineCount++;
                    continue;
                }

                foreach (var entitlement in matches)
                {
                    _context.MeitarMucarimEntitlementMatches.Add(new MeitarMucarimEntitlementMatch
                    {
                        EntityId = entityId,
                        MeitarMucarimId = line.Id,
                        EntitlementId = entitlement.Id,
                        MatchProcessId = processId,
                        IsActive = true,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                    matchedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return (matchedCount, unmatchedLineCount);
        }

        private async Task<string?> ResolveInstitutionSymbolAsync(
            int institutionId,
            int hebrewYearId,
            CancellationToken cancellationToken)
        {
            var yearSymbol = await _context.InstitutionYearSettings
                .AsNoTracking()
                .Where(s => s.InstitutionId == institutionId && s.HebrewYearId == hebrewYearId)
                .Select(s => s.Symbol)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(yearSymbol))
                return yearSymbol;

            return await _context.Institutions
                .AsNoTracking()
                .Where(i => i.Id == institutionId)
                .Select(i => i.Symbol)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<List<MeitarMucarimEntitlementMatchListItemDto>> MapListItemsAsync(
            List<(MeitarMucarimEntitlementMatch match, MeitarMucarim mucarim, Entitlement ent)> rows,
            CancellationToken cancellationToken)
        {
            var typeIds = rows.Select(r => r.ent.AssistantTypeId).Distinct().ToList();
            var typeNames = typeIds.Count == 0
                ? new Dictionary<int, string>()
                : await _shared.AssistantTypes
                    .AsNoTracking()
                    .Where(t => typeIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);

            var institutionIds = rows
                .Where(r => r.ent.InstitutionId != null)
                .Select(r => r.ent.InstitutionId!.Value)
                .Distinct()
                .ToList();
            var institutionNames = institutionIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Institutions
                    .AsNoTracking()
                    .Where(i => institutionIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

            return rows
                .OrderBy(r => r.mucarim.InstitutionCode)
                .ThenBy(r => r.mucarim.TopicCode)
                .ThenBy(r => r.ent.Id)
                .Select(r => new MeitarMucarimEntitlementMatchListItemDto
                {
                    Id = r.match.Id,
                    MeitarMucarimId = r.mucarim.Id,
                    EntitlementId = r.ent.Id,
                    PeriodYear = r.mucarim.PeriodYear,
                    PeriodMonth = r.mucarim.PeriodMonth,
                    EffectiveDate = r.mucarim.EffectiveDate,
                    TopicCode = r.mucarim.TopicCode,
                    TopicDescription = r.mucarim.TopicDescription,
                    InstitutionCode = r.mucarim.InstitutionCode,
                    InstitutionName = r.mucarim.InstitutionName,
                    CalculatedAmount = r.mucarim.CalculatedAmount,
                    CalculatedDifference = r.mucarim.CalculatedDifference,
                    UnitCount = r.mucarim.UnitCount,
                    AssistantTypeId = r.ent.AssistantTypeId,
                    AssistantTypeName = typeNames.GetValueOrDefault(r.ent.AssistantTypeId),
                    EntitlementInstitutionName = r.ent.InstitutionId.HasValue
                        ? institutionNames.GetValueOrDefault(r.ent.InstitutionId.Value)
                        : null,
                    ClassName = r.ent.ClassName,
                    Hours = r.ent.Hours,
                    StartDate = r.ent.StartDate,
                    EndDate = r.ent.EndDate
                })
                .ToList();
        }

        private static int DisableMatches(
            List<MeitarMucarimEntitlementMatch> existing,
            int? userId,
            DateTime now)
        {
            foreach (var match in existing)
            {
                match.IsActive = false;
                match.DisabledAt = now;
                match.DisableUser = userId;
                match.UpdatedAt = now;
                match.UpdateUser = userId;
            }
            return existing.Count;
        }

        private static string NormalizeSymbol(string symbol)
        {
            var trimmed = symbol.Trim();
            if (trimmed.EndsWith(".0", StringComparison.Ordinal) &&
                trimmed.Length > 2 &&
                trimmed[..^2].All(char.IsDigit))
                trimmed = trimmed[..^2];
            return trimmed;
        }
    }
}
