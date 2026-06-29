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
    /// when EntityId is 0 — use IgnoreQueryFilters() in those endpoints.
    /// </summary>
    public class AssistDbContext : DbContext
    {
        private readonly string _schemaName;
        private readonly ITenantContext _tenantContext;

        public DbSet<User>           Users          { get; set; }
        public DbSet<Role>           Roles          { get; set; }
        public DbSet<UserRole>       UserRoles      { get; set; }
        public DbSet<RolesAction>    RolesActions   { get; set; }
        public DbSet<ActionAuditLog> ActionAuditLogs { get; set; }

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

                entity.HasOne(u => u.LockReason)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.LockReasonId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(u => u.UserRoles)
                    .WithOne(ur => ur.User)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(u => _tenantContext.EntityId != 0 && u.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.Name }).IsUnique();

                entity.HasMany(r => r.UserRoles)
                    .WithOne(ur => ur.Role)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(r => r.RolesActions)
                    .WithOne(ra => ra.Role)
                    .HasForeignKey(ra => ra.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(r => _tenantContext.EntityId != 0 && r.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

                entity.HasQueryFilter(ur => _tenantContext.EntityId != 0 && ur.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<RolesAction>(entity =>
            {
                entity.ToTable("roles_actions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RoleId, e.ActionId }).IsUnique();

                entity.HasOne(ra => ra.Action)
                    .WithMany(a => a.RolesActions)
                    .HasForeignKey(ra => ra.ActionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(ra => _tenantContext.EntityId != 0 && ra.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<ActionAuditLog>(entity =>
            {
                entity.ToTable("action_audit_logs");
                entity.HasKey(e => e.Id);

                entity.HasQueryFilter(a => _tenantContext.EntityId != 0 && a.EntityId == _tenantContext.EntityId);
            });
        }
    }

    /// <summary>Configuration for assist_schema. Bound to "Database" in appsettings.</summary>
    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "assist_schema";
    }
}
