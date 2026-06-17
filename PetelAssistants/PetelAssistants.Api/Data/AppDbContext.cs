using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Data
{
    /// <summary>
    /// PetelAssistants application database context.
    /// Uses assistants_schema (configured via Database:SchemaName in appsettings.json).
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly string _schemaName;

        public DbSet<Entity> Entities { get; set; }
        public DbSet<SystemAttribute> SystemAttributes { get; set; }
        public DbSet<User> Users { get; set; }

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IOptions<DatabaseSettings> dbSettings)
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

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "assist_schema";
    }
}
