// PetelApp.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        public DbSet<HebrewYear> HebrewYears { get; set; }

        // NEW DbSets for Council and SchoolClass
        public DbSet<School> Schools { get; set; }
        public DbSet<Council> Councils { get; set; }
        public DbSet<SchoolClass> SchoolClasses { get; set; }

            // Add these DbSets for school attributes
        public DbSet<SchoolAttributeType> SchoolAttributeTypes { get; set; }
        public DbSet<SchoolAttributeTypeValue> SchoolAttributeTypeValues { get; set; }
        public DbSet<SchoolAttribute> SchoolAttributes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration following Authentication & Session Management
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users", "petel_schema");
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
                entity.ToTable("entities", "petel_schema");
                entity.Property(e => e.OwnerId).HasColumnName("owner"); // Add this line

                entity.HasOne(e => e.EntityType)
                      .WithMany(et => et.Entities)
                      .HasForeignKey(e => e.EntityTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // EntityType configuration
            modelBuilder.Entity<EntityType>(entity =>
            {
                entity.ToTable("entity_types", "petel_schema");
            });

            // Role configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles", "petel_schema");
            });

            // UserRole configuration - fix to match actual UserRole.cs file
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles", "petel_schema");
                entity.HasIndex(e => new { e.UserId }).IsUnique();
                
                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                      

            });

            // RolesAction configuration
            modelBuilder.Entity<RolesAction>(entity =>
            {
                entity.ToTable("roles_actions", "petel_schema");
                entity.HasOne(ra => ra.Role)
                      .WithMany(r => r.RolesActions)
                      .HasForeignKey(ra => ra.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // HoursBudget configuration following Entity-Based Request Flow
            modelBuilder.Entity<HoursBudget>(entity =>
            {
                entity.ToTable("hours_budget", "petel_schema");
                entity.HasIndex(e => new { e.EntityId, e.SchoolYear, e.BudgetType })
                      .HasDatabaseName("ix_hours_budget_entity_year_type");
                      
                entity.Property(e => e.AllocatedHours).HasPrecision(10, 2);
                entity.Property(e => e.UsedHours).HasPrecision(10, 2);
                entity.Property(e => e.RemainingHours).HasPrecision(10, 2);
            });

            // SystemAttribute configuration following System Attributes Pattern
            modelBuilder.Entity<SystemAttribute>(entity =>
            {
                entity.ToTable("system_attributes", "petel_schema");
                entity.HasIndex(e => e.Description).IsUnique();
            });

            // SchoolYear configuration following Entity-Based Request Flow
            modelBuilder.Entity<SchoolYear>(entity =>
            {
                entity.ToTable("school_years", "petel_schema");
                entity.HasIndex(e => new { e.SchoolId, e.YearName }).IsUnique();
            });

            // View configuration
            modelBuilder.Entity<StudentSchoolYearsRegistrationSummaryVw>(entity =>
            {
                entity.ToView("student_school_years_registration_summary_vw", "petel_schema");
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
                entity.ToTable("councils", "petel_schema");
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
                entity.ToTable("school_classes", "petel_schema");
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
                entity.ToTable("school_attribute_types_values", "petel_schema");
                entity.HasOne(v => v.SchoolAttributeType)
                    .WithMany()
                    .HasForeignKey(v => v.SchoolAttributeTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SchoolAttributeType>(entity =>
            {
                entity.ToTable("school_attributes_types", "petel_schema");
            });

            modelBuilder.Entity<SchoolAttribute>(entity =>
            {
                entity.ToTable("school_attributes", "petel_schema");
                
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
        }
    }

  
}