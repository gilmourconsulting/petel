using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PetelAssistants.Api.Data
{
    /// <summary>
    /// PetelAssistants application database context.
    /// Uses assistants_schema (configured via Database:SchemaName in appsettings.json).
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly string _schemaName;

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
        }
    }

    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "assistants_schema";
    }
}
