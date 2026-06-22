namespace PetelAssistants.Api.Tenancy
{
    /// <summary>
    /// Marks an entity as tenant-scoped. Every table in assist_schema must implement this.
    /// AssistDbContext applies a global query filter: WHERE entity_id = &lt;current tenant&gt;.
    /// </summary>
    public interface IEntityScoped
    {
        int EntityId { get; set; }
    }
}
