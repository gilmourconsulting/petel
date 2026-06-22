namespace PetelAssistants.Api.Tenancy
{
    /// <summary>
    /// Provides the current tenant's EntityId for use in EF Core global query filters.
    /// Resolved per HTTP request via HttpTenantContext (scoped DI).
    /// Returns 0 when no authenticated session exists (e.g., during login).
    /// </summary>
    public interface ITenantContext
    {
        int EntityId { get; }
    }
}
