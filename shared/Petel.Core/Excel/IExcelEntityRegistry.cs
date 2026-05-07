namespace Petel.Core.Excel
{
    /// <summary>
    /// Abstraction over the app-specific set of exportable entities.
    /// Each API project (PetelATH, PetelAssistants) provides its own implementation.
    /// </summary>
    public interface IExcelEntityRegistry
    {
        /// <summary>
        /// Returns the list of entities available for report building.
        /// The list may be filtered based on the current user context.
        /// </summary>
        IReadOnlyList<ExcelEntityDescriptor> GetAvailableEntities();

        /// <summary>
        /// Returns the full descriptor for a single entity, or null if not found.
        /// </summary>
        ExcelEntityDescriptor? GetEntityDescriptor(string entityName);

        /// <summary>
        /// Executes a query against the entity and returns rows as dictionaries.
        /// Keys are field names; values are the raw (decrypted) field values as strings.
        /// </summary>
        /// <param name="queryConfig">Validated query configuration.</param>
        /// <param name="context">Resolved entity scope (scoping + year).</param>
        /// <param name="runtimeParams">Named parameters supplied at runtime (e.g. year_id).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<List<Dictionary<string, object?>>> QueryEntityAsync(
            ExcelQueryConfig queryConfig,
            ExcelEntityContext context,
            Dictionary<string, string> runtimeParams,
            CancellationToken cancellationToken = default);
    }
}
