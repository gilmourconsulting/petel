// PetelATH.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using PetelATH.Api.Configuration;
using PetelATH.Api.Models;
using PetelATH.Api.Services;

namespace PetelATH.Api.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _schemaName;
        private readonly DataEncryptionService _encryptionService;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IOptions<DatabaseSettings> dbSettings,
            DataEncryptionService encryptionService)
            : base(options)
        {
            _schemaName = dbSettings.Value.SchemaName;
            _encryptionService = encryptionService;
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
        public DbSet<CouncilSummaryVw> CouncilSummaryVw { get; set; }
        // DbSets following Entity-Based Request Flow
        public DbSet<SchoolStudent> SchoolStudents { get; set; }
public DbSet<Status> Statuses { get; set; }
            public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<HebrewYear> HebrewYears { get; set; }
        public DbSet<SchoolYearAttribute> SchoolYearAttributes { get; set; }
        public DbSet<TransactionAccountType> TransactionAccountTypes { get; set; }
        public DbSet<TransactionAccount> TransactionAccounts { get; set; }
        public DbSet<TransactionType> TransactionTypes { get; set; }
        public DbSet<TransactionDetailType> TransactionDetailTypes { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionDetail> TransactionDetails { get; set; }

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
       
       public DbSet<TrackPricing> TracksPricing { get; set; }

        public DbSet<SchoolAdditionalStudyProgram> SchoolAdditionalStudyPrograms { get; set; }
        public DbSet<SpecialNeedsCharacterization> SpecialNeedsCharacterizations { get; set; } = null!;

        public DbSet<AdditionalStudyProgramsPricing> AdditionalStudyProgramsPricing { get; set; }

        public DbSet<ActionType> ActionTypes { get; set; }
        public DbSet<SystemAction> SystemActions { get; set; }

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

        public DbSet<SpecialNeedsPricingCategory> SpecialNeedsPricingCategories { get; set; }   

        public DbSet<SpecialNeedsPricingStep> SpecialNeedsPricingSteps { get; set; }


        public DbSet<SignLanguageTranslator> SignLanguageTranslators { get; set; } = null!;


        public DbSet<ActionAuditLog> ActionAuditLogs { get; set; }
        public DbSet<UserLockReason> UserLockReasons { get; set; }

        // Report Generation (Excel + Word)
        public DbSet<ReportDefinition> ReportDefinitions { get; set; } = null!;
        public DbSet<ReportQuery> ReportQueries { get; set; } = null!;
        public DbSet<ReportTemplate> ReportTemplates { get; set; } = null!;
        public DbSet<ReportParameter> ReportParameters { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(_schemaName);

           modelBuilder.HasDefaultSchema(_schemaName);
    
    // ===== PERSON ENTITY - Encrypt sensitive fields =====
    modelBuilder.Entity<Person>(entity =>
    {
        entity.ToTable("persons");
    
        // ✅ Encrypt ID number - NOT searchable in database
        entity.Property(e => e.IdNumber)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // ✅ Encrypt email with dedicated converter
        entity.Property(e => e.Email)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // ✅ Encrypt phone number with dedicated converter
        entity.Property(e => e.PhoneNumber)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    });
    
    // ===== SCHOOL_STUDENT ENTITY - Encrypt sensitive fields =====
    modelBuilder.Entity<SchoolStudent>(entity =>
    {
        entity.ToTable("school_students");
    
        // ✅ Encrypt ID number - NOT searchable in database
        entity.Property(e => e.IdNumber)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // ✅ Encrypt street address with dedicated converter
        entity.Property(e => e.Street)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // Navigation properties
        entity.HasOne(s => s.Status)
            .WithMany()
            .HasForeignKey(s => s.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
    });
    
    // ===== USER ENTITY - Encrypt OTP secret and email =====
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("users");
        
        entity.HasIndex(e => e.Username).IsUnique();
    
        // ✅ Encrypt OTP secret with dedicated converter
        entity.Property(e => e.OtpSecret)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // ✅ Encrypt user email with dedicated converter
        entity.Property(e => e.Email)
            .HasConversion(
                v => v != null ? _encryptionService.Encrypt(v) : null,
                v => v != null ? _encryptionService.Decrypt(v) : null
            );
    
        // Navigation properties
        entity.HasOne(d => d.Entity)
            .WithMany()
            .HasForeignKey(d => d.EntityId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(d => d.LockReason)
            .WithMany(r => r.Users)
            .HasForeignKey(d => d.LockReasonId)
            .OnDelete(DeleteBehavior.SetNull);
    });
    
    // ===== ENTITY TABLE - NO ENCRYPTION (Leave as is) =====
    // Entity.email and Entity.phone remain plaintext
      

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
                entity.Property(e => e.Email)
                    .HasConversion((string? v) => v, (string? v) => v); // Identity conversion = no encryption

                 entity.Property(e => e.Phone)
                    .HasConversion((string? v) => v, (string? v) => v); // Identity conversion = no encryption
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

            // UserRole configuration 
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);


            });

            modelBuilder.Entity<Status>(entity =>
                {
                    entity.ToTable("statuses");
                    entity.HasKey(e => e.Id);
                });


            // RolesAction configuration
// Update RolesAction configuration - ADD SystemAction navigation
modelBuilder.Entity<RolesAction>(entity =>
{
    entity.ToTable("roles_actions");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.RoleId, e.ActionId }).IsUnique();

    // Role navigation
    entity.HasOne(ra => ra.Role)
        .WithMany(r => r.RolesActions)
        .HasForeignKey(ra => ra.RoleId)
        .OnDelete(DeleteBehavior.Cascade);

    // ✅  SystemAction navigation
    entity.HasOne(ra => ra.SystemAction)
        .WithMany(a => a.RolesActions)
        .HasForeignKey(ra => ra.ActionId)
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

            // Configure SpecialNeedsPricingElement
modelBuilder.Entity<SpecialNeedsPricingElement>(entity =>
{
    entity.ToTable("special_needs_pricing_elements");
    
    // Configure relationship to HebrewYear
    entity.HasOne(e => e.Year)
        .WithMany()
        .HasForeignKey(e => e.YearId)
        .OnDelete(DeleteBehavior.Restrict);
});

// Configure SpecialNeedsPricingCategory
modelBuilder.Entity<SpecialNeedsPricingCategory>(entity =>
{
    entity.ToTable("special_needs_pricing_categories");
    
    // Configure relationship to SpecialNeedsPricingElement
    entity.HasOne(c => c.PricingElementNavigation)
        .WithMany(e => e.Categories)
        .HasForeignKey(c => c.PricingElement)
        .OnDelete(DeleteBehavior.Restrict);
});


            modelBuilder.Entity<SpecialNeedsPricingStep>(entity =>
            {
                entity.ToTable("special_needs_pricing_steps");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.PricingElementNavigation)
                    .WithMany()
                    .HasForeignKey(e => e.PricingElement)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.PricingElement, e.Category, e.ObjectCheck, e.ObjectElementCheck, e.ObjectElementValue })
                    .IsUnique()
                    .HasDatabaseName("special_needs_pricing_steps_uc");
            });

            modelBuilder.Entity<CouncilSummaryVw>(entity =>
                    {
                        entity.ToView("council_summary_vw");
                        entity.HasNoKey(); // Views typically don't have a single key
                    });

            // Council entity configuration following Database Conventions
        /*    modelBuilder.Entity<Council>(entity =>
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
            });*/

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

                entity.HasOne(e => e.Characterization)
                    .WithMany()
                    .HasForeignKey(e => e.CharacterizationId)
                    .OnDelete(DeleteBehavior.Restrict);
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

               // Sign Language Translators configuration
    modelBuilder.Entity<SignLanguageTranslator>(entity =>
    {
        entity.ToTable("sign_language_translators");
        
        entity.HasOne(t => t.SchoolYear)
            .WithMany()
            .HasForeignKey(t => t.SchoolYearId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(t => t.Person)
            .WithMany()
            .HasForeignKey(t => t.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
            
        entity.HasIndex(e => e.SchoolYearId);
        entity.HasIndex(e => e.PersonId);
        entity.HasIndex(e => new { e.SchoolYearId, e.PersonId }).IsUnique();
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


                modelBuilder.Entity<AdditionalStudyProgramsPricing>(entity =>
                {
                    entity.ToTable("additional_study_programs_pricing");
                    entity.HasKey(e => e.Id);
                    
                    entity.HasOne(e => e.HebrewYear)
                        .WithMany()
                        .HasForeignKey(e => e.YearId)
                        .OnDelete(DeleteBehavior.Restrict);
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

                modelBuilder.Entity<SchoolStudent>(entity =>
                {
                    entity.ToTable("school_students");
                    
                    // ✅ Configure Status relationship
                    entity.HasOne(s => s.Status)
                        .WithMany(st => st.Students)
                        .HasForeignKey(s => s.StatusId)
                        .OnDelete(DeleteBehavior.SetNull);
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

             
            // Action security entities configuration
modelBuilder.Entity<ActionType>(entity =>
{
    entity.ToTable("action_types");
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Name)
        .IsRequired()
        .HasMaxLength(50);
    
    entity.Property(e => e.Description)
        .HasMaxLength(255);
    
    entity.HasIndex(e => e.Name).IsUnique();

    entity.HasMany(at => at.Actions)
        .WithOne(a => a.ActionType)
        .HasForeignKey(a => a.ActionTypeId)
        .OnDelete(DeleteBehavior.Restrict);
});

modelBuilder.Entity<SystemAction>(entity =>
{
    entity.ToTable("actions");
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Name)
        .IsRequired()
        .HasMaxLength(100);
    
    entity.Property(e => e.DisplayName)
        .HasMaxLength(150);
    
    entity.Property(e => e.Description)
        .HasMaxLength(255);
    
    entity.Property(e => e.OnclickName)
        .HasMaxLength(100);
    
    entity.Property(e => e.Reference)
        .HasMaxLength(200);

    entity.HasIndex(e => e.Name).IsUnique();
    entity.HasIndex(e => e.ActionTypeId);
    entity.HasIndex(e => e.Reference);
    entity.HasIndex(e => e.IsActive);

    entity.HasOne(a => a.ActionType)
        .WithMany(at => at.Actions)
        .HasForeignKey(a => a.ActionTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasMany(a => a.RolesActions)
        .WithOne(ra => ra.SystemAction)
        .HasForeignKey(ra => ra.ActionId)
        .OnDelete(DeleteBehavior.Cascade);
});
            
            // Update RolesAction to include SystemAction navigation
            modelBuilder.Entity<RolesAction>(entity =>
            {
                entity.ToTable("roles_actions");
                entity.HasKey(e => e.Id);
            
                entity.HasOne(ra => ra.Role)
                    .WithMany(r => r.RolesActions)
                    .HasForeignKey(ra => ra.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            
                entity.HasOne(ra => ra.SystemAction)
                    .WithMany(a => a.RolesActions)
                    .HasForeignKey(ra => ra.ActionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ActionAuditLog>(entity =>
{
    entity.ToTable("action_audit_logs");
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.Timestamp);
    entity.HasIndex(e => e.Result);
    entity.HasIndex(e => new { e.UserId, e.Timestamp });

    entity.HasOne(a => a.User)
        .WithMany()
        .HasForeignKey(a => a.UserId)
        .OnDelete(DeleteBehavior.Restrict);
});

            // SchoolYearAttribute configuration
            modelBuilder.Entity<SchoolYearAttribute>(entity =>
            {
                entity.ToTable("school_year_attributes");
                
                entity.HasIndex(e => e.YearId);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => new { e.YearId, e.Name }).IsUnique();

                entity.HasOne(sya => sya.HebrewYear)
                    .WithMany()
                    .HasForeignKey(sya => sya.YearId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TransactionAccountType configuration
            modelBuilder.Entity<TransactionAccountType>(entity =>
            {
                entity.ToTable("transaction_account_types");

                // Indexes
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsActive);

                // Relationships
                entity.HasOne(tat => tat.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(tat => tat.CreatedUser)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(tat => tat.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(tat => tat.UpdateUser)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // TransactionAccount configuration
            modelBuilder.Entity<TransactionAccount>(entity =>
            {
                entity.ToTable("transaction_accounts");

                // Indexes for performance
                entity.HasIndex(e => e.OwnerEntityId);
                entity.HasIndex(e => e.RelatedEntityId);
                entity.HasIndex(e => e.AccountTypeId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => new { e.OwnerEntityId, e.RelatedEntityId, e.AccountTypeId }).IsUnique();

                // Configure relationships
                entity.HasOne(ta => ta.OwnerEntity)
                    .WithMany()
                    .HasForeignKey(ta => ta.OwnerEntityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ta => ta.RelatedEntity)
                    .WithMany()
                    .HasForeignKey(ta => ta.RelatedEntityId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ta => ta.AccountType)
                    .WithMany(at => at.TransactionAccounts)
                    .HasForeignKey(ta => ta.AccountTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ta => ta.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(ta => ta.CreatedUser)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(ta => ta.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(ta => ta.UpdateUser)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // TransactionType configuration
            modelBuilder.Entity<TransactionType>(entity =>
            {
                entity.ToTable("transaction_types");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            // TransactionDetailType configuration
            modelBuilder.Entity<TransactionDetailType>(entity =>
            {
                entity.ToTable("transaction_detail_types");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            // Transaction configuration
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");

                // Indexes for performance
                entity.HasIndex(e => e.AccountId);
                entity.HasIndex(e => e.TransactionTypeId);
                entity.HasIndex(e => e.TransactionDate);
                entity.HasIndex(e => e.RelatedTransactionId);
                entity.HasIndex(e => e.RelatedStudentId);
                entity.HasIndex(e => e.SchoolYearId);
                entity.HasIndex(e => e.UserId);

                // Configure relationships
                entity.HasOne(t => t.TransactionAccount)
                    .WithMany(ta => ta.Transactions)
                    .HasForeignKey(t => t.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.TransactionType)
                    .WithMany(tt => tt.Transactions)
                    .HasForeignKey(t => t.TransactionTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.RelatedTransaction)
                    .WithMany(t => t.RelatedTransactions)
                    .HasForeignKey(t => t.RelatedTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.RelatedStudent)
                    .WithMany()
                    .HasForeignKey(t => t.RelatedStudentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.SchoolYear)
                    .WithMany()
                    .HasForeignKey(t => t.SchoolYearId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.User)
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // TransactionDetail configuration
            modelBuilder.Entity<TransactionDetail>(entity =>
            {
                entity.ToTable("transaction_details");

                // Indexes for performance
                entity.HasIndex(e => e.TransactionId);
                entity.HasIndex(e => e.DetailTypeId);
                entity.HasIndex(e => e.RelatedStudentId);

                // Configure relationships
                entity.HasOne(td => td.Transaction)
                    .WithMany(t => t.TransactionDetails)
                    .HasForeignKey(td => td.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(td => td.DetailType)
                    .WithMany(dt => dt.TransactionDetails)
                    .HasForeignKey(td => td.DetailTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(td => td.RelatedStudent)
                    .WithMany()
                    .HasForeignKey(td => td.RelatedStudentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Report Generation (Excel + Word)
            modelBuilder.Entity<ReportDefinition>(entity =>
            {
                entity.ToTable("report_definitions");
                entity.HasMany(e => e.Parameters)
                    .WithOne(p => p.Definition)
                    .HasForeignKey(p => p.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Query)
                    .WithOne(q => q.Definition)
                    .HasForeignKey<ReportQuery>(q => q.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Template)
                    .WithOne(t => t.Definition)
                    .HasForeignKey<ReportTemplate>(t => t.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReportQuery>(entity =>
            {
                entity.ToTable("report_queries");
            });

            modelBuilder.Entity<ReportTemplate>(entity =>
            {
                entity.ToTable("report_templates");
            });

            modelBuilder.Entity<ReportParameter>(entity =>
            {
                entity.ToTable("report_parameters");
            });

        }
    }

}