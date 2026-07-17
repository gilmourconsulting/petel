using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Data
{
    /// <summary>
    /// Read-mostly DbContext for shared_schema — global reference data with no tenant ownership.
    /// No global query filters — shared data is visible to all tenants.
    /// </summary>
    public class SharedDbContext : DbContext
    {
        private readonly string _schemaName;

        public DbSet<Entity>          Entities         { get; set; }
        public DbSet<EntityType>      EntityTypes      { get; set; }
        public DbSet<SystemAttribute> SystemAttributes { get; set; }
        public DbSet<HebrewYear>      HebrewYears      { get; set; }
        public DbSet<MenuItem>        MenuItems        { get; set; }
        public DbSet<SystemAction>    SystemActions    { get; set; }
        public DbSet<ActionType>      ActionTypes      { get; set; }
        public DbSet<UserLockReason>  UserLockReasons  { get; set; }
        public DbSet<PhoneType>       PhoneTypes       { get; set; }
        public DbSet<AssistantType>              AssistantTypes              { get; set; }
        public DbSet<MinistryParticipationOption> MinistryParticipationOptions { get; set; }
        public DbSet<MeitarDataFilterValue>       MeitarDataFilterValues       { get; set; }

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

            // Prevent assist-schema types from leaking into shared_schema.
            // User and RolesAction live in AssistDbContext (assist_schema).
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<RolesAction>();

            modelBuilder.Entity<Entity>(entity =>
            {
                entity.ToTable("entities");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.EntityType)
                    .WithMany(et => et.Entities)
                    .HasForeignKey(e => e.EntityTypeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ParentEntity)
                    .WithMany(p => p.ChildEntities)
                    .HasForeignKey(e => e.ParentEntityId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EntityType>(entity =>
            {
                entity.ToTable("entity_types");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<SystemAttribute>(entity =>
            {
                entity.ToTable("system_attributes");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<HebrewYear>(entity =>
            {
                entity.ToTable("hebrew_years");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.ToTable("menu_items");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<SystemAction>(entity =>
            {
                entity.ToTable("actions");
                entity.HasKey(e => e.Id);

                entity.HasOne(a => a.ActionType)
                    .WithMany(at => at.Actions)
                    .HasForeignKey(a => a.ActionTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ActionType>(entity =>
            {
                entity.ToTable("action_types");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<UserLockReason>(entity =>
            {
                entity.ToTable("user_lock_reasons");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<PhoneType>(entity =>
            {
                entity.ToTable("phone_types");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<AssistantType>(entity =>
            {
                entity.ToTable("assistant_types");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<MinistryParticipationOption>(entity =>
            {
                entity.ToTable("ministry_participation_options");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<MeitarDataFilterValue>(entity =>
            {
                entity.ToTable("meitar_data_filter_values");
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
