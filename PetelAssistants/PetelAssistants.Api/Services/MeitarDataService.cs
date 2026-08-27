using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelAssistants.Api.Configuration;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs.Meitar;

namespace PetelAssistants.Api.Services
{
    public class MeitarDataService : IMeitarDataService
    {
        private const string HttpClientName = "MeitarApi";
        private const int MeitarRowCap = 1000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SharedDbContext _sharedContext;
        private readonly MeitarApiSettings _settings;
        private readonly ILogger<MeitarDataService> _logger;

        public MeitarDataService(
            IHttpClientFactory httpClientFactory,
            SharedDbContext sharedContext,
            IOptions<MeitarApiSettings> settings,
            ILogger<MeitarDataService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _sharedContext = sharedContext;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetActiveSymbolCodesAsync(CancellationToken cancellationToken = default)
        {
            return await _sharedContext.Entities
                .AsNoTracking()
                .Where(e => e.IsActive
                         && e.SymbolCode != null
                         && e.SymbolCode != string.Empty
                         && e.EntityType != null
                         && e.EntityType.Name == "local_authority")
                .OrderBy(e => e.SymbolCode)
                .Select(e => e.SymbolCode!)
                .ToListAsync(cancellationToken);
        }

        public async Task<string?> GetSymbolCodeForEntityAsync(int entityId, CancellationToken cancellationToken = default)
        {
            var code = await _sharedContext.Entities
                .AsNoTracking()
                .Where(e => e.Id == entityId
                         && e.IsActive
                         && e.SymbolCode != null
                         && e.SymbolCode != string.Empty)
                .Select(e => e.SymbolCode)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }

        public async Task<IReadOnlyList<string>> GetFilterValuesAsync(
            string fileName,
            string filterField,
            CancellationToken cancellationToken = default)
        {
            var fileKey = fileName.Trim().ToLowerInvariant();
            var fieldKey = filterField.Trim().ToLowerInvariant();

            // Case-insensitive match — Postgres equality is case-sensitive by default.
            return await _sharedContext.MeitarDataFilterValues
                .AsNoTracking()
                .Where(v => v.IsActive
                         && v.FileName.ToLower() == fileKey
                         && v.FilterField.ToLower() == fieldKey)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.Id)
                .Select(v => v.FilterValue)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Loads active filter config from meitar_data_filter_values for the given file name, grouped by
        /// filter_field. Meitar's data/query endpoint now accepts multiple field filters (ANDed together)
        /// in one request, so every distinct configured field is returned — none are dropped.
        /// </summary>
        private async Task<List<MeitarDataQueryFilter>> GetFilterGroupsAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var fileKey = fileName.ToLowerInvariant();

            var rows = await _sharedContext.MeitarDataFilterValues
                .AsNoTracking()
                .Where(v => v.IsActive && v.FileName.ToLower() == fileKey)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.Id)
                .Select(v => new { v.FilterField, v.FilterValue })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                var detail = await DescribeFilterConfigAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"לא הוגדרו ערכי סינון פעילים עבור {fileName}. {detail}");
            }

            var groups = rows
                .GroupBy(r => r.FilterField.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new MeitarDataQueryFilter
                {
                    Field = g.Key,
                    ValueList = g
                        .Select(r => r.FilterValue)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList()
                })
                .ToList();

            var emptyField = groups.FirstOrDefault(g => g.ValueList.Count == 0);
            if (emptyField != null)
            {
                throw new InvalidOperationException(
                    $"ערכי סינון ריקים עבור {fileName}/{emptyField.Field}.");
            }

            return groups;
        }

        private async Task<string> DescribeFilterConfigAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _sharedContext.MeitarDataFilterValues
                .AsNoTracking()
                .Select(v => new { v.FileName, v.FilterField, v.IsActive })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return "הטבלה meitar_data_filter_values ריקה.";

            var summary = rows
                .GroupBy(r => $"{r.FileName}/{r.FilterField} (active={r.IsActive})")
                .Select(g => $"{g.Key}×{g.Count()}")
                .ToList();

            return "קיים בטבלה: " + string.Join(", ", summary);
        }

        public async Task<MeitarDataQueryResult> QueryAsync(
            MeitarDataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.SymbolList == null || request.SymbolList.Count == 0)
                throw new InvalidOperationException("symbolList is required and must not be empty.");

            if (string.IsNullOrWhiteSpace(request.FileName))
                throw new InvalidOperationException("fileName is required.");

            if (!MeitarDataFileNames.IsSupported(request.FileName))
                throw new InvalidOperationException($"Unknown file name suffix '{request.FileName}'.");

            var hasFilters = request.Filters is { Count: > 0 };
            var hasPeriodList = request.PeriodList is { Count: > 0 };

            if (!hasFilters && !hasPeriodList)
                throw new InvalidOperationException("At least one of filters or periodList is required and must not be empty.");

            if (hasFilters)
            {
                foreach (var filter in request.Filters!)
                {
                    if (string.IsNullOrWhiteSpace(filter.Field))
                        throw new InvalidOperationException("filters[].field is required.");

                    if (filter.ValueList == null || filter.ValueList.Count == 0)
                        throw new InvalidOperationException($"filters[].valueList is required and must not be empty for field '{filter.Field}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
                throw new InvalidOperationException("MeitarApi:BaseUrl is not configured.");

            _logger.LogInformation(
                "Meitar data/query request: FileName={FileName}, SymbolList=[{SymbolList}], Filters=[{Filters}], PeriodList=[{PeriodList}]",
                request.FileName,
                string.Join(", ", request.SymbolList),
                hasFilters
                    ? string.Join("; ", request.Filters!.Select(f => $"{f.Field} IN ({string.Join(", ", f.ValueList)})"))
                    : string.Empty,
                hasPeriodList ? string.Join(", ", request.PeriodList!) : string.Empty);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var requestUri = new Uri(client.BaseAddress!, "data/query");
            using var response = await client.PostAsJsonAsync("data/query", request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Meitar data/query failed with HTTP {StatusCode} at {RequestUri}: {Body}",
                    (int)response.StatusCode,
                    requestUri,
                    body);
                throw new InvalidOperationException($"Meitar API returned HTTP {(int)response.StatusCode}.");
            }

            MeitarApiEnvelope<MeitarDataQueryResponse>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<MeitarApiEnvelope<MeitarDataQueryResponse>>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Meitar response: {Body}", body);
                throw new InvalidOperationException("Meitar API returned an invalid response.", ex);
            }

            if (envelope == null || !envelope.Success || envelope.Data == null)
            {
                var message = envelope?.Message ?? "Meitar API request failed.";
                _logger.LogWarning("Meitar data/query returned success=false: {Message}", message);
                return MeitarDataQueryResult.Failed(message);
            }

            _logger.LogInformation(
                "Meitar data/query response: FileName={FileName}, RowCount={RowCount}",
                envelope.Data.FileName ?? request.FileName,
                envelope.Data.RowCount);

            if (envelope.Data.RowCount >= MeitarRowCap)
            {
                _logger.LogWarning(
                    "Meitar data/query for {FileName} returned {RowCount} rows (API cap is {Cap}). Results may be truncated.",
                    envelope.Data.FileName ?? request.FileName,
                    envelope.Data.RowCount,
                    MeitarRowCap);
            }

            return MeitarDataQueryResult.FromResponse(envelope.Data);
        }

        public async Task<MeitarDataQueryResult> QueryMutavimByTopicDescriptionsAsync(
            CancellationToken cancellationToken = default)
        {
            var symbolList = await GetActiveSymbolCodesAsync(cancellationToken);
            if (symbolList.Count == 0)
                throw new InvalidOperationException("No active local authorities with symbol_code configured.");

            // filter_field(s) come from config (ApiReference example: TopicCode).
            var filters = await GetFilterGroupsAsync(MeitarDataFileNames.Mutavim, cancellationToken);

            return await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = symbolList.ToList(),
                FileName = MeitarDataFileNames.Mutavim,
                Filters = filters
            }, cancellationToken);
        }

        public async Task<MeitarDataQueryResult> QueryMutavimForSymbolAndPeriodAsync(
            string symbolCode,
            int periodYear,
            int periodMonth,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbolCode))
                throw new InvalidOperationException("לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית.");

            if (periodMonth < 1 || periodMonth > 12)
                throw new InvalidOperationException("חודש לא תקין.");

            // Topic (or other) filter(s) come from meitar_data_filter_values. Meitar's data/query
            // endpoint now accepts multiple field filters plus a periodList in one request (ANDed
            // together server-side), so the provider does all the filtering — no local re-filtering
            // of the returned rows is needed.
            var filters = await GetFilterGroupsAsync(MeitarDataFileNames.Mutavim, cancellationToken);
            var periodList = new List<string> { FormatCalcDateFilterValue(periodYear, periodMonth) };

            _logger.LogInformation(
                "MUTAVIM retrieve: Period={PeriodYear}/{PeriodMonth}, SymbolCode={SymbolCode}, PeriodList=[{PeriodList}], Filters=[{Filters}]",
                periodYear, periodMonth, symbolCode, string.Join(", ", periodList),
                string.Join("; ", filters.Select(f => $"{f.Field} IN ({string.Join(", ", f.ValueList)})")));

            var result = await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = new List<string> { symbolCode.Trim() },
                FileName = MeitarDataFileNames.Mutavim,
                Filters = filters,
                PeriodList = periodList
            }, cancellationToken);

            if (!result.Success)
                return result;

            _logger.LogInformation(
                "MUTAVIM retrieve: {RowCount} row(s) returned by Meitar for period {PeriodYear}/{PeriodMonth}",
                result.Rows.Count, periodYear, periodMonth);

            return result;
        }

        public async Task<MeitarDataQueryResult> QueryMucarimForSymbolAndPeriodAsync(
            string symbolCode,
            int periodYear,
            int periodMonth,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbolCode))
                throw new InvalidOperationException("לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית.");

            if (periodMonth < 1 || periodMonth > 12)
                throw new InvalidOperationException("חודש לא תקין.");

            // See QueryMutavimForSymbolAndPeriodAsync — filters + periodList are sent together in one
            // request; Meitar ANDs them server-side, so no local re-filtering is needed here either.
            var filters = await GetFilterGroupsAsync(MeitarDataFileNames.Mucarim, cancellationToken);
            var periodList = new List<string> { FormatCalcDateFilterValue(periodYear, periodMonth) };

            _logger.LogInformation(
                "MUCARIM retrieve: Period={PeriodYear}/{PeriodMonth}, SymbolCode={SymbolCode}, PeriodList=[{PeriodList}], Filters=[{Filters}]",
                periodYear, periodMonth, symbolCode, string.Join(", ", periodList),
                string.Join("; ", filters.Select(f => $"{f.Field} IN ({string.Join(", ", f.ValueList)})")));

            var result = await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = new List<string> { symbolCode.Trim() },
                FileName = MeitarDataFileNames.Mucarim,
                Filters = filters,
                PeriodList = periodList
            }, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("MUCARIM retrieve: query failed — {Message}", result.Message);
                return result;
            }

            _logger.LogInformation(
                "MUCARIM retrieve: {RowCount} row(s) returned by Meitar for period {PeriodYear}/{PeriodMonth}",
                result.Rows.Count, periodYear, periodMonth);

            return result;
        }

        public async Task<MeitarDataQueryResult> QuerySharatimForSymbolAndPeriodAsync(
            string symbolCode,
            int periodYear,
            int periodMonth,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbolCode))
                throw new InvalidOperationException("לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית.");

            if (periodMonth < 1 || periodMonth > 12)
                throw new InvalidOperationException("חודש לא תקין.");

            // See QueryMutavimForSymbolAndPeriodAsync — filters (TopicCode=107, seeded in
            // meitar_data_filter_values) + periodList are sent together in one request; Meitar
            // ANDs them server-side. effectiveDate == calcDate is enforced by adding an explicit
            // EffectiveDate filter below (calcDate is always the first of the period month), with
            // the local check in TryMapSharatimRow kept as a defensive safety net.
            var filters = await GetFilterGroupsAsync(MeitarDataFileNames.Shratim, cancellationToken);
            filters.Add(new MeitarDataQueryFilter
            {
                Field = "EffectiveDate",
                ValueList = new List<string> { FormatEffectiveDateFilterValue(periodYear, periodMonth) }
            });
            var periodList = new List<string> { FormatCalcDateFilterValue(periodYear, periodMonth) };

            _logger.LogInformation(
                "SHARATIM retrieve: Period={PeriodYear}/{PeriodMonth}, SymbolCode={SymbolCode}, PeriodList=[{PeriodList}], Filters=[{Filters}]",
                periodYear, periodMonth, symbolCode, string.Join(", ", periodList),
                string.Join("; ", filters.Select(f => $"{f.Field} IN ({string.Join(", ", f.ValueList)})")));

            var result = await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = new List<string> { symbolCode.Trim() },
                FileName = MeitarDataFileNames.Shratim,
                Filters = filters,
                PeriodList = periodList
            }, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("SHARATIM retrieve: query failed — {Message}", result.Message);
                return result;
            }

            _logger.LogInformation(
                "SHARATIM retrieve: {RowCount} row(s) returned by Meitar for period {PeriodYear}/{PeriodMonth}",
                result.Rows.Count, periodYear, periodMonth);

            return result;
        }

        private static string FormatCalcDateFilterValue(int periodYear, int periodMonth)
            => $"{periodMonth:D2}/{periodYear}";

        private static string FormatEffectiveDateFilterValue(int periodYear, int periodMonth)
            => new DateOnly(periodYear, periodMonth, 1).ToString("yyyy-MM-dd");

        public async Task<IReadOnlyDictionary<string, MeitarDataQueryResult>> QueryAllFileTypesAsync(
            string filterField,
            IReadOnlyList<string> filterValues,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filterField))
                throw new InvalidOperationException("filterField is required.");

            if (filterValues == null || filterValues.Count == 0)
                throw new InvalidOperationException("filterValues must not be empty.");

            var symbolList = await GetActiveSymbolCodesAsync(cancellationToken);
            if (symbolList.Count == 0)
                throw new InvalidOperationException("No active local authorities with symbol_code configured.");

            var results = new Dictionary<string, MeitarDataQueryResult>(StringComparer.OrdinalIgnoreCase);
            var filters = new List<MeitarDataQueryFilter>
            {
                new() { Field = filterField, ValueList = filterValues.ToList() }
            };

            foreach (var fileName in MeitarDataFileNames.All)
            {
                var result = await QueryAsync(new MeitarDataQueryRequest
                {
                    SymbolList = symbolList.ToList(),
                    FileName = fileName,
                    Filters = filters
                }, cancellationToken);

                results[fileName] = result;
            }

            return results;
        }
    }
}
