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
        private const string MutavimTopicDescriptionField = "TopicDescription";
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

        public async Task<IReadOnlyList<string>> GetFilterValuesAsync(
            string fileName,
            string filterField,
            CancellationToken cancellationToken = default)
        {
            return await _sharedContext.MeitarDataFilterValues
                .AsNoTracking()
                .Where(v => v.IsActive
                         && v.FileName == fileName
                         && v.FilterField == filterField)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.Id)
                .Select(v => v.FilterValue)
                .ToListAsync(cancellationToken);
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
            using var response = await client.PostAsJsonAsync("data/query", request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Meitar data/query failed with HTTP {StatusCode}: {Body}",
                    (int)response.StatusCode,
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

            var filterValues = await GetFilterValuesAsync(
                MeitarDataFileNames.Mutavim,
                MutavimTopicDescriptionField,
                cancellationToken);

            if (filterValues.Count == 0)
                throw new InvalidOperationException(
                    $"No active filter values configured for {MeitarDataFileNames.Mutavim}/{MutavimTopicDescriptionField}.");

            return await QueryAsync(new MeitarDataQueryRequest
            {
                SymbolList = symbolList.ToList(),
                FileName = MeitarDataFileNames.Mutavim,
                FilterField = MutavimTopicDescriptionField,
                FilterValueList = filterValues.ToList()
            }, cancellationToken);
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
