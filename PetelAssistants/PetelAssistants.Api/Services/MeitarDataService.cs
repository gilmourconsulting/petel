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
        /// Loads active MUTAVIM filter config from meitar_data_filter_values.
        /// Uses the configured filter_field (e.g. TopicCode per ApiReference) and its values.
        /// </summary>
        private async Task<(string FilterField, IReadOnlyList<string> FilterValues)> GetMutavimFilterConfigAsync(
            CancellationToken cancellationToken = default)
        {
            var fileKey = MeitarDataFileNames.Mutavim.ToLowerInvariant();

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
                    $"לא הוגדרו ערכי סינון פעילים עבור {MeitarDataFileNames.Mutavim}. {detail}");
            }

            // Prefer a single filter field; if several are configured, take the first by display_order.
            var filterField = rows[0].FilterField.Trim();
            var values = rows
                .Where(r => string.Equals(r.FilterField.Trim(), filterField, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.FilterValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (values.Count == 0)
            {
                throw new InvalidOperationException(
                    $"ערכי סינון ריקים עבור {MeitarDataFileNames.Mutavim}/{filterField}.");
            }

            var otherFields = rows
                .Select(r => r.FilterField.Trim())
                .Where(f => !string.Equals(f, filterField, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (otherFields.Count > 0)
            {
                _logger.LogWarning(
                    "Multiple filter fields configured for {FileName}; using {FilterField}. Ignored: {Ignored}",
                    MeitarDataFileNames.Mutavim,
                    filterField,
                    string.Join(", ", otherFields));
            }

            return (filterField, values);
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

            if (string.IsNullOrWhiteSpace(request.FilterField))
                throw new InvalidOperationException("filterField is required.");

            if (request.FilterValueList == null || request.FilterValueList.Count == 0)
                throw new InvalidOperationException("filterValueList is required and must not be empty.");

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
                throw new InvalidOperationException("MeitarApi:BaseUrl is not configured.");

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

            // filter_field comes from config (ApiReference example: TopicCode).
            var (filterField, filterValues) = await GetMutavimFilterConfigAsync(cancellationToken);

            return await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = symbolList.ToList(),
                FileName = MeitarDataFileNames.Mutavim,
                FilterField = filterField,
                FilterValueList = filterValues.ToList()
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

            // Same config-driven filter as QueryMutavimByTopicDescriptionsAsync (e.g. TopicCode).
            var (filterField, filterValues) = await GetMutavimFilterConfigAsync(cancellationToken);

            var result = await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = new List<string> { symbolCode.Trim() },
                FileName = MeitarDataFileNames.Mutavim,
                FilterField = filterField,
                FilterValueList = filterValues.ToList()
            }, cancellationToken);

            if (!result.Success)
                return result;

            // Meitar allows one filter field; keep only the selected calendar period.
            var filtered = result.Rows
                .Where(row => MatchesPeriod(row, periodYear, periodMonth))
                .ToList();

            return new MeitarDataQueryResult
            {
                Success = true,
                FileName = result.FileName,
                RowCount = filtered.Count,
                Rows = filtered
            };
        }

        private static bool MatchesPeriod(JsonElement row, int periodYear, int periodMonth)
        {
            if (!TryGetDateOnlyProperty(row, "calcDate", out var calcDate) &&
                !TryGetDateOnlyProperty(row, "CalcDate", out calcDate))
                return false;

            return calcDate.Year == periodYear && calcDate.Month == periodMonth;
        }

        private static bool TryGetDateOnlyProperty(JsonElement row, string propertyName, out DateOnly value)
        {
            value = default;
            if (row.ValueKind != JsonValueKind.Object)
                return false;

            if (!row.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return false;

                if (DateOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out value))
                    return true;

                if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    value = DateOnly.FromDateTime(dt);
                    return true;
                }

                return false;
            }

            if (prop.ValueKind == JsonValueKind.Object &&
                prop.TryGetProperty("year", out var y) &&
                prop.TryGetProperty("month", out var m) &&
                prop.TryGetProperty("day", out var d) &&
                y.TryGetInt32(out var year) &&
                m.TryGetInt32(out var month) &&
                d.TryGetInt32(out var day))
            {
                value = new DateOnly(year, month, day);
                return true;
            }

            return false;
        }

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

            foreach (var fileName in MeitarDataFileNames.All)
            {
                var result = await QueryAsync(new MeitarDataQueryRequest
                {
                    SymbolList = symbolList.ToList(),
                    FileName = fileName,
                    FilterField = filterField,
                    FilterValueList = filterValues.ToList()
                }, cancellationToken);

                results[fileName] = result;
            }

            return results;
        }
    }
}
