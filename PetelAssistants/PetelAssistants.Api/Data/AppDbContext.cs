using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Petel.Core.Session;
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
        private readonly DataEncryptionService _encryptionService;

        public DbSet<User>           Users          { get; set; }
        public DbSet<Role>           Roles          { get; set; }
        public DbSet<UserRole>       UserRoles      { get; set; }
        public DbSet<RolesAction>    RolesActions   { get; set; }
        public DbSet<ActionAuditLog> ActionAuditLogs { get; set; }
        public DbSet<Person>         Persons        { get; set; }
        public DbSet<PersonDetail>   PersonDetails  { get; set; }
        public DbSet<PersonAddress>  PersonAddresses { get; set; }
        public DbSet<PersonPhone>     PersonPhones   { get; set; }
        public DbSet<Institution>            Institutions           { get; set; }
        public DbSet<Entitlement>            Entitlements           { get; set; }
        public DbSet<EntitlementAllocation>  EntitlementAllocations { get; set; }
        public DbSet<SalaryUploadProcess>    SalaryUploadProcesses  { get; set; }
        public DbSet<Salary>                 Salaries               { get; set; }
        public DbSet<SalaryUploadWarning>    SalaryUploadWarnings   { get; set; }
        public DbSet<SalaryFieldMapping>     SalaryFieldMappings    { get; set; }
        public DbSet<MeitarRetrieveProcess>  MeitarRetrieveProcesses { get; set; }
        public DbSet<MeitarMutavim>          MeitarMutavim          { get; set; }

        public AssistDbContext(
            DbContextOptions<AssistDbContext> options,
            IOptions<DatabaseSettings> dbSettings,
            ITenantContext tenantContext,
            DataEncryptionService encryptionService)
            : base(options)
        {
            _schemaName = dbSettings.Value.SchemaName;
            _tenantContext = tenantContext;
            _encryptionService = encryptionService;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(_schemaName);

            // Prevent shared-schema types from leaking into assist_schema.
            modelBuilder.Ignore<UserLockReason>();
            modelBuilder.Ignore<SystemAction>();
            modelBuilder.Ignore<PhoneType>();
            modelBuilder.Ignore<AssistantType>();
            modelBuilder.Ignore<HebrewYear>();

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.Username }).IsUnique();

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

                entity.HasQueryFilter(ra => _tenantContext.EntityId != 0 && ra.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<ActionAuditLog>(entity =>
            {
                entity.ToTable("action_audit_logs");
                entity.HasKey(e => e.Id);

                entity.HasQueryFilter(a => _tenantContext.EntityId != 0 && a.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<Person>(entity =>
            {
                entity.ToTable("persons");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.IdNumber }).IsUnique();

                entity.Property(e => e.IdNumber)
                    .HasConversion(
                        v => _encryptionService.EncryptDeterministic(v),
                        v => _encryptionService.DecryptDeterministic(v));

                entity.HasMany(p => p.Details)
                    .WithOne(d => d.Person)
                    .HasForeignKey(d => d.PersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Addresses)
                    .WithOne(a => a.Person)
                    .HasForeignKey(a => a.PersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Phones)
                    .WithOne(ph => ph.Person)
                    .HasForeignKey(ph => ph.PersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(p => _tenantContext.EntityId != 0 && p.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<PersonDetail>(entity =>
            {
                entity.ToTable("person_details");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PersonId);

                entity.Property(e => e.Email)
                    .HasConversion(
                        v => v != null ? _encryptionService.Encrypt(v) : null,
                        v => v != null ? _encryptionService.Decrypt(v) : null);

                entity.HasQueryFilter(d => _tenantContext.EntityId != 0 && d.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<PersonAddress>(entity =>
            {
                entity.ToTable("person_addresses");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PersonId);

                entity.Property(e => e.Street)
                    .HasConversion(
                        v => v != null ? _encryptionService.Encrypt(v) : null,
                        v => v != null ? _encryptionService.Decrypt(v) : null);

                entity.HasQueryFilter(a => _tenantContext.EntityId != 0 && a.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<PersonPhone>(entity =>
            {
                entity.ToTable("person_phones");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PersonId);

                entity.Property(e => e.PhoneNumber)
                    .HasConversion(
                        v => v != null ? _encryptionService.Encrypt(v) : null,
                        v => v != null ? _encryptionService.Decrypt(v) : null);

                entity.HasQueryFilter(p => _tenantContext.EntityId != 0 && p.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<Institution>(entity =>
            {
                entity.ToTable("institutions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.Name }).IsUnique();
                entity.HasIndex(e => new { e.EntityId, e.InstitutionType });

                entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<Entitlement>(entity =>
            {
                entity.ToTable("entitlements");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.HebrewYearId });

                entity.Property(e => e.PupilIdNumber)
                    .HasMaxLength(500)   // stores AES ciphertext, not raw 9-char ID
                    .HasConversion(
                        v => v == null ? null : _encryptionService.EncryptDeterministic(v),
                        v => v == null ? null : _encryptionService.DecryptDeterministic(v));

                entity.HasOne(e => e.Institution)
                    .WithMany()
                    .HasForeignKey(e => e.InstitutionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<EntitlementAllocation>(entity =>
            {
                entity.ToTable("entitlement_allocations");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.EntitlementId });
                entity.HasIndex(e => e.PersonId);

                entity.HasOne(a => a.Person)
                    .WithMany()
                    .HasForeignKey(a => a.PersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(a => _tenantContext.EntityId != 0 && a.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<SalaryUploadProcess>(entity =>
            {
                entity.ToTable("salary_upload_processes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.PeriodYear, e.PeriodMonth });

                entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<Salary>(entity =>
            {
                entity.ToTable("salaries");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.PeriodYear, e.PeriodMonth, e.NationalId, e.DepartmentId })
                    .IsUnique();

                entity.Property(e => e.NationalId)
                    .HasMaxLength(500)
                    .HasConversion(
                        v => _encryptionService.EncryptDeterministic(v),
                        v => _encryptionService.DecryptDeterministic(v));

                entity.HasOne(s => s.Process)
                    .WithMany(p => p.Salaries)
                    .HasForeignKey(s => s.ProcessId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.MatchedPerson)
                    .WithMany()
                    .HasForeignKey(s => s.MatchedPersonId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(s => s.MatchedAllocation)
                    .WithMany()
                    .HasForeignKey(s => s.MatchedAllocationId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(s => _tenantContext.EntityId != 0 && s.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<SalaryUploadWarning>(entity =>
            {
                entity.ToTable("salary_upload_warnings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProcessId);
                entity.HasIndex(e => e.SalaryId);

                entity.HasOne(w => w.Process)
                    .WithMany(p => p.Warnings)
                    .HasForeignKey(w => w.ProcessId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(w => w.Salary)
                    .WithMany()
                    .HasForeignKey(w => w.SalaryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(w => _tenantContext.EntityId != 0 && w.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<SalaryFieldMapping>(entity =>
            {
                entity.ToTable("salary_field_mappings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.EntityId).IsUnique();

                entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<MeitarRetrieveProcess>(entity =>
            {
                entity.ToTable("meitar_retrieve_processes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.PeriodYear, e.PeriodMonth });

                entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
            });

            modelBuilder.Entity<MeitarMutavim>(entity =>
            {
                entity.ToTable("meitar_mutavim");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityId, e.PeriodYear, e.PeriodMonth });

                entity.HasOne(m => m.Process)
                    .WithMany(p => p.Rows)
                    .HasForeignKey(m => m.ProcessId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(m => _tenantContext.EntityId != 0 && m.EntityId == _tenantContext.EntityId);
            });
        }
    }

    /// <summary>Configuration for assist_schema. Bound to "Database" in appsettings.</summary>
    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "assist_schema";
    }
}
