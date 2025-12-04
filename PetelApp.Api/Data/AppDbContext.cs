// PetelApp.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelApp.Api.Configuration;
using PetelApp.Api.Models;

namespace PetelApp.Api.Data
{
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

        // DbSets following Authentication & Session Management
        public DbSet<SystemAttribute> SystemAttributes { get; set; }
        public DbSet<HoursBudget> HoursBudgets { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Entity> Entities { get; set; }
        public DbSet<EntityType> EntityTypes { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<SchoolYear> SchoolYears { get; set; }
        public DbSet<RolesAction> RolesActions { get; set; }

        // Views
        public DbSet<StudentSchoolYearsRegistrationSummaryVw> StudentSchoolYearsRegistrationSummaryVw { get; set; }

        // DbSets following Entity-Based Request Flow
        public DbSet<SchoolStudent> SchoolStudents { get; set; }

            public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<HebrewYear> HebrewYears { get; set; }

        //  DbSets for Council and SchoolClass
        public DbSet<School> Schools { get; set; }
        public DbSet<Council> Councils { get; set; }
        public DbSet<SchoolClass> SchoolClasses { get; set; }

        // Person DbSet for contact management
        public DbSet<Person> Persons { get; set; } = null!;

        // DbSets for school attributes
        public DbSet<SchoolAttributeType> SchoolAttributeTypes { get; set; }
        public DbSet<SchoolAttributeTypeValue> SchoolAttributeTypeValues { get; set; }
        public DbSet<SchoolAttribute> SchoolAttributes { get; set; }
        // DbSets for Tracks management
        public DbSet<Track> Tracks { get; set; }
        public DbSet<TrackLevel> TrackLevels { get; set; }
        public DbSet<SchoolTrack> SchoolTracks { get; set; }

        public DbSet<SchoolAdditionalStudyProgram> SchoolAdditionalStudyPrograms { get; set; }
        public DbSet<SpecialNeedsCharacterization> SpecialNeedsCharacterizations { get; set; } = null!;


        public DbSet<Alert> Alerts { get; set; }
        public DbSet<AlertLink> AlertLinks { get; set; }
        public DbSet<AlertType> AlertTypes { get; set; }
        public DbSet<AlertStatus> AlertStatuses { get; set; }
        public DbSet<AlertLevel> AlertLevels { get; set; }


        // DbSets for Documents management
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<DocumentType> DocumentTypes { get; set; } = null!;
        public DbSet<DocumentLink> DocumentLinks { get; set; } = null!;

        public DbSet<DocumentStatusType> DocumentStatusTypes { get; set; } = null!;


        public DbSet<SchoolStudentPricingElement> SchoolStudentPricingElements { get; set; }
        public DbSet<SpecialNeedsPricingElement> SpecialNeedsPricingElements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(_schemaName);

            // User entity configuration following Authentication & Session Management
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasOne(u => u.Entity)
                      .WithMany(e => e.Users)
                      .HasForeignKey(u => u.EntityId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Entity configuration following Entity-Based Request Flow
            modelBuilder.Entity<Entity>(entity =>
            {
                entity.ToTable("entities");
                entity.Property(e => e.OwnerId).HasColumnName("owner"); // Add this line

                entity.HasOne(e => e.EntityType)
                      .WithMany(et => et.Entities)
                      .HasForeignKey(e => e.EntityTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Owner)
                .WithMany(e => e.OwnedEntities)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
                            });

            // EntityType configuration
            modelBuilder.Entity<EntityType>(entity =>
            {
                entity.ToTable("entity_types");
            });


            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.ToTable("menu_items");
                entity.HasIndex(e => e.SortOrder);
            });

            // Role configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
            });

            // UserRole configuration - fix to match actual UserRole.cs file
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasIndex(e => new { e.UserId }).IsUnique();

                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);


            });

            // RolesAction configuration
            modelBuilder.Entity<RolesAction>(entity =>
            {
                entity.ToTable("roles_actions");
                entity.HasOne(ra => ra.Role)
                      .WithMany(r => r.RolesActions)
                      .HasForeignKey(ra => ra.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // HoursBudget configuration following Entity-Based Request Flow
            modelBuilder.Entity<HoursBudget>(entity =>
            {
                entity.ToTable("hours_budget");
                entity.HasIndex(e => new { e.EntityId, e.SchoolYear, e.BudgetType })
                      .HasDatabaseName("ix_hours_budget_entity_year_type");

                entity.Property(e => e.AllocatedHours).HasPrecision(10, 2);
                entity.Property(e => e.UsedHours).HasPrecision(10, 2);
                entity.Property(e => e.RemainingHours).HasPrecision(10, 2);
            });

            // SystemAttribute configuration following System Attributes Pattern
            modelBuilder.Entity<SystemAttribute>(entity =>
            {
                entity.ToTable("system_attributes");
                entity.HasIndex(e => e.Description).IsUnique();
            });

            // SchoolYear configuration following Entity-Based Request Flow
            modelBuilder.Entity<SchoolYear>(entity =>
            {
                entity.ToTable("school_years");
                entity.HasIndex(e => new { e.SchoolId, e.YearName }).IsUnique();
            });

            // View configuration
            modelBuilder.Entity<StudentSchoolYearsRegistrationSummaryVw>(entity =>
            {
                entity.ToView("student_school_years_registration_summary_vw");
                entity.HasNoKey(); // Views don't have primary keys
                entity.Property(s => s.SchoolId).HasColumnName("school_id");
                entity.Property(s => s.SchoolYearId).HasColumnName("school_year_id");
                entity.Property(s => s.SchoolGrade).HasColumnName("school_grade");
                entity.Property(s => s.SchoolTrack).HasColumnName("school_track");
                entity.Property(s => s.Registered).HasColumnName("registered");
            });

            // Council entity configuration following Database Conventions
            modelBuilder.Entity<Council>(entity =>
            {
                entity.ToTable("councils");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CouncilCode).IsRequired();
                entity.Property(e => e.CouncilType).HasMaxLength(25);
                entity.Property(e => e.CouncilShortName).HasMaxLength(25);
                entity.Property(e => e.CouncilLongName).HasMaxLength(50);
                entity.Property(e => e.CouncilDistrict).HasMaxLength(25);

                // Ignore computed properties (not in database)
                entity.Ignore(e => e.Name);
                entity.Ignore(e => e.ShortName);
            });

            // SchoolClass entity configuration following Database Conventions
            modelBuilder.Entity<SchoolClass>(entity =>
            {
                entity.ToTable("school_classes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SchoolYearId).IsRequired();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(6);
                entity.Property(e => e.Level).IsRequired().HasMaxLength(3);
                entity.Property(e => e.ClassNumber).IsRequired().HasMaxLength(3);
                //  entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                //  entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });



            // Configure School relationships
            modelBuilder.Entity<School>()
                .HasOne(s => s.PrincipalPerson)
                .WithMany()
                .HasForeignKey(s => s.Principal)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<School>()
                .HasOne(s => s.InspectorPerson)
                .WithMany()
                .HasForeignKey(s => s.Inspector)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<School>()
                .HasOne(s => s.ContactPersonPerson)
                .WithMany()
                .HasForeignKey(s => s.ContactPerson)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<School>()
                .HasOne(s => s.CouncilEntity)
                .WithMany()
                .HasForeignKey(s => s.Council)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure school attribute relationships
            modelBuilder.Entity<SchoolAttributeTypeValue>(entity =>
            {
                entity.ToTable("school_attribute_types_values");
                entity.HasOne(v => v.SchoolAttributeType)
                    .WithMany()
                    .HasForeignKey(v => v.SchoolAttributeTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SchoolAttributeType>(entity =>
            {
                entity.ToTable("school_attributes_types");
            });

            modelBuilder.Entity<SchoolAttribute>(entity =>
            {
                entity.ToTable("school_attributes");

                // Foreign key to school_years
                entity.HasOne(a => a.SchoolYear)
                    .WithMany()
                    .HasForeignKey(a => a.SchoolYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Foreign key to school_attributes_types
                entity.HasOne(a => a.SchoolAttributeType)
                    .WithMany()
                    .HasForeignKey(a => a.SchoolAttributeTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique constraint for school_attributes
                entity.HasIndex(a => new { a.Id, a.SchoolYearId, a.Version })
                    .IsUnique();
            });

            // DbSets for Tracks management
            // Configure Track
            modelBuilder.Entity<Track>(entity =>
            {
                entity.ToTable("tracks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrackName).HasColumnName("name");
                entity.Property(e => e.YearId).HasColumnName("year_id");
            });

            // Configure TrackLevel
            modelBuilder.Entity<TrackLevel>(entity =>
            {
                entity.ToTable("tracks_levels");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LevelName).HasColumnName("level");
                entity.Property(e => e.SchoolTrackId).HasColumnName("school_track_id");
            });

            // Configure SchoolTrack
            modelBuilder.Entity<SchoolTrack>(entity =>
            {
                entity.ToTable("school_tracks");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.SchoolYear)
                    .WithMany()
                    .HasForeignKey(e => e.SchoolYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Track)
                    .WithMany(t => t.SchoolTracks)
                    .HasForeignKey(e => e.TrackId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.TrackLevel)
                    .WithMany(tl => tl.SchoolTracks)
                    .HasForeignKey(e => e.TrackLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.SchoolClass)
                    .WithMany()
                    .HasForeignKey(e => e.ClassId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            modelBuilder.Entity<SchoolAdditionalStudyProgram>(entity =>
                {
                    entity.ToTable("school_additional_study_programs");
                    entity.HasKey(e => e.Id);

                    // Configure relationships
                    entity.HasOne(e => e.SchoolYear)
                        .WithMany()
                        .HasForeignKey(e => e.SchoolYearId)
                        .OnDelete(DeleteBehavior.Restrict);

                    entity.HasOne(e => e.SchoolClass)
                        .WithMany()
                        .HasForeignKey(e => e.ClassId)
                        .OnDelete(DeleteBehavior.Restrict);

                    // ✅ Self-referencing relationship for version history
                    entity.HasOne(e => e.MasterProgram)
                        .WithMany(e => e.VersionHistory)
                        .HasForeignKey(e => e.MasterId)
                        .OnDelete(DeleteBehavior.Restrict);

                    // ✅ Decimal precision for financial fields
                    entity.Property(e => e.Cost).HasPrecision(10, 2);
                    entity.Property(e => e.ApprovedAmount).HasPrecision(10, 2);

                    // ✅ Indexes for performance
                    entity.HasIndex(e => e.SchoolYearId);
                    entity.HasIndex(e => e.ClassId);
                    entity.HasIndex(e => e.IsLastVersion);
                    entity.HasIndex(e => e.MasterId);

                    // ✅ Default values
                    entity.Property(e => e.Version).HasDefaultValue(1);
                    entity.Property(e => e.IsLastVersion).HasDefaultValue(true);
                });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("documents"); // ✅ Lowercase table name with schema
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id"); // ✅ Lowercase column name

                entity.Property(e => e.MasterDocumentId)
                    .HasColumnName("master_document_id");

                entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500);

                entity.Property(e => e.DocumentTypeId)
                    .HasColumnName("document_type_id");

                entity.Property(e => e.StatusId)
                    .HasColumnName("status_id");

                entity.Property(e => e.FileBlob)
                    .HasColumnName("file_blob")
                    .IsRequired(false);

                entity.Property(e => e.FileEncoding)
                    .HasColumnName("file_encoding")
                    .HasMaxLength(10)
                    .IsRequired(false);

                entity.Property(e => e.FileName).HasColumnName("file_name");


                entity.Property(e => e.Version)
                    .HasColumnName("version");

                entity.Property(e => e.IsLastVersion)
                    .HasColumnName("is_last_version");

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at");


                entity.HasOne(d => d.DocumentType)
                    .WithMany()
                    .HasForeignKey(d => d.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(d => d.DocumentLinks)
                    .WithOne(dl => dl.Document)
                    .HasForeignKey(dl => dl.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DocumentType>(entity =>
            {
                entity.ToTable("document_types"); // ✅ Lowercase table name with schema
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Level)
                    .HasColumnName("level")
                    .HasMaxLength(50);

                entity.Property(e => e.YearId)
                    .HasColumnName("year_id");
            });

            modelBuilder.Entity<DocumentLink>(entity =>
            {
                entity.ToTable("document_links"); // ✅ Lowercase table name with schema
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.DocumentId)
                    .HasColumnName("document_id");

                entity.Property(e => e.SchoolStudentId)
                    .HasColumnName("school_student_id");

                entity.Property(e => e.EntityId)
                    .HasColumnName("entity_id");

                entity.HasOne(dl => dl.Document)
                    .WithMany(d => d.DocumentLinks)
                    .HasForeignKey(dl => dl.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.DocumentId, e.EntityId });
                entity.HasIndex(e => new { e.DocumentId, e.SchoolStudentId });
            });
            modelBuilder.Entity<DocumentStatusType>(entity =>
            {
                entity.ToTable("document_status_types");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(50)
                    .IsRequired();
            });



            // Alert entities configuration
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.ToTable("alerts");
                entity.HasKey(e => e.Id);

            });

            modelBuilder.Entity<AlertLink>(entity =>
            {
                entity.ToTable("alert_links");
                entity.HasKey(e => e.Id);


                entity.HasOne(e => e.Alert)
                    .WithMany()
                    .HasForeignKey(e => e.AlertId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Entity)
                    .WithMany()
                    .HasForeignKey(e => e.EntityId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AlertType>(entity =>
            {
                entity.ToTable("alert_types");
                entity.HasKey(e => e.Id);

            });

            modelBuilder.Entity<AlertStatus>(entity =>
            {
                entity.ToTable("alert_statuses");
                entity.HasKey(e => e.Id);

            });

            modelBuilder.Entity<AlertLevel>(entity =>
            {
                entity.ToTable("alert_levels");
                entity.HasKey(e => e.Id);

            });

        }
    }

}