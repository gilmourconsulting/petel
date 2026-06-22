using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Data
{
    /// <summary>
    /// Tenant-scoped DbContext for assist_schema.
    /// Every DbSet here maps to a table with a mandatory entity_id column.
    /// A global query filter (WHERE entity_id = &lt;current tenant&gt;) is applied to every
    /// tenant-scoped entity via ITenantContext. The filter is a no-op (returns nothing)
    /// when EntityId is 0, which happens on unauthenticated requests such as login —
    /// use IgnoreQueryFilters() in those endpoints.
    /// </summary>
    public class AssistDbContext : DbContext
    {
        private readonly string _schemaName;
        private readonly ITenantContext _tenantContext;

        public DbSet<User> Users { get; set; }

        public AssistDbContext(
            DbContextOptions<AssistDbContext> options,
            IOptions<DatabaseSettings> dbSettings,
            ITenantContext tenantContext)
            : base(options)
        {
            _schemaName = dbSettings.Value.SchemaName;
            _tenantContext = tenantContext;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(_schemaName);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.Username }).IsUnique();
                // Global tenant filter — prevents cross-tenant data leakage automatically.
                entity.HasQueryFilter(u => _tenantContext.EntityId != 0 && u.EntityId == _tenantContext.EntityId);
            });
        }
    }

    /// <summary>Configuration for assist_schema. Bound to "Database" in appsettings.</summary>
    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "assist_schema";
    }
}
