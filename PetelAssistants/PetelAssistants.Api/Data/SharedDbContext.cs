using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Data
{
    /// <summary>
    /// Read-mostly DbContext for shared_schema — global reference data with no tenant ownership.
    /// Tables: entities, entity_types, assistant_types, cities, system_attributes, and similar lookup tables.
    /// No global query filters — shared data is visible to all tenants.
    /// </summary>
    public class SharedDbContext : DbContext
    {
        private readonly string _schemaName;

        public DbSet<Entity>           Entities         { get; set; }
        public DbSet<SystemAttribute>  SystemAttributes { get; set; }

        public SharedDbContext(
            DbContextOptions<SharedDbContext> options,
            IOptions<SharedDatabaseSettings> dbSettings)
            : base(options)
        {
            _schemaName = dbSettings.Value.SchemaName;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(_schemaName);

            modelBuilder.Entity<Entity>(entity =>
            {
                entity.ToTable("entities");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<SystemAttribute>(entity =>
            {
                entity.ToTable("system_attributes");
                entity.HasKey(e => e.Id);
            });
        }
    }

    /// <summary>Configuration for shared_schema connection. Bound to "SharedDatabase" in appsettings.</summary>
    public class SharedDatabaseSettings
    {
        public string SchemaName { get; set; } = "shared_schema";
    }
}
