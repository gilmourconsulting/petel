using PetelAssistants.Api.DTOs.Meitar;

namespace PetelAssistants.Api.Services
{
    public interface IMeitarDataService
    {
        Task<IReadOnlyList<string>> GetActiveSymbolCodesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetFilterValuesAsync(
            string fileName,
            string filterField,
            CancellationToken cancellationToken = default);

        Task<MeitarDataQueryResult> QueryAsync(
            MeitarDataQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<MeitarDataQueryResult> QueryMutavimByTopicDescriptionsAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, MeitarDataQueryResult>> QueryAllFileTypesAsync(
            string filterField,
            IReadOnlyList<string> filterValues,
            CancellationToken cancellationToken = default);
    }
}
