// PetelApp.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Models; // ADD THIS if referencing model DTOs
using System.ComponentModel.DataAnnotations; // ADD THIS for data annotations
using System.ComponentModel.DataAnnotations.Schema; // ADD THIS for table mapping
using System.Security.Cryptography;
using System.Text;

namespace PetelApp.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<Entity> Entities { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<SystemAttribute> SystemAttributes { get; set; }
        public DbSet<EntityType> EntityTypes { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolesAction> RolesActions { get; set; }
        public DbSet<SchoolYear> SchoolYears { get; set; }
        public DbSet<StudentSchoolYearsRegistrationSummaryVw> StudentSchoolYearsRegistrationSummaryVw { get; set; }
        public DbSet<HoursBudget> HoursBudgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure petel_schema following multi-tenant architecture
            modelBuilder.HasDefaultSchema("petel_schema");
            
            // Configure table names following database conventions
            modelBuilder.Entity<HoursBudget>().ToTable("hours_budgets");
    
            base.OnModelCreating(modelBuilder);

            // Configure Entities table
            modelBuilder.Entity<Entity>(entity =>
            {
                entity.ToTable("entities", "petel_schema");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .HasColumnName("id");
                entity.Property(e => e.EntityTypeId)
                    .HasColumnName("entity_type_id")
                    .IsRequired();
                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .HasMaxLength(500);
                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20);
                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(200);
                entity.Property(e => e.PrincipalName)
                    .HasColumnName("principal_name")
                    .HasMaxLength(200);
                entity.Property(e => e.ApiConnectionId)
                    .HasColumnName("api_connection_id");
                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);
                entity.Property(e => e.SchoolLogo)
                    .HasColumnName("school_logo")
                    .HasMaxLength(500);
                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Symbol)
                    .HasColumnName("symbol");  
                entity.Property(e => e.InspectorName)
                    .HasColumnName("inspector_name")
                    .HasMaxLength(255);
                entity.Property(e => e.Characterization)
                    .HasColumnName("charactarization")
                    .HasMaxLength(255);
                entity.Property(e => e.OwnerId)
                    .HasColumnName("owner");
                entity.Property(e => e.ContactPerson)
                    .HasColumnName("contact_person")
                    .HasMaxLength(255);
                entity.Property(e => e.EducationStage)
                    .HasColumnName("education_stage")
                    .HasMaxLength(100);
                // Foreign key to EntityType
                entity.HasOne(e => e.EntityType)
                    .WithMany(et => et.Entities)
                    .HasForeignKey(e => e.EntityTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index for performance
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.EntityTypeId);
            });

            // Configure Users table
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users", "petel_schema");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Id)
                    .HasColumnName("id");
                entity.Property(u => u.EntityId)
                    .HasColumnName("entity_id")
                    .IsRequired();
                entity.Property(u => u.Username)
                    .HasColumnName("username")
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(u => u.PasswordHash)
                    .HasColumnName("password_hash")
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(u => u.Email)
                    .HasColumnName("email")
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(u => u.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20);
                entity.Property(u => u.FirstName)
                    .HasColumnName("first_name")
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(u => u.LastName)
                    .HasColumnName("last_name")
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(u => u.LastLogin)
                    .HasColumnName("last_login");
                entity.Property(u => u.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);
                entity.Property(u => u.UpdateUser)
                    .HasColumnName("update_user");
                entity.Property(u => u.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(u => u.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Unique constraint on Username + EntityId (multi-tenant)
                entity.HasIndex(u => new { u.Username, u.EntityId })
                    .IsUnique();

                // Foreign key to Entity (using EntityId)
                entity.HasOne(u => u.Entity)
                    .WithMany(e => e.Users)
                    .HasForeignKey(u => u.EntityId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes for performance
                entity.HasIndex(u => u.Username);
                entity.HasIndex(u => u.EntityId);
                entity.HasIndex(u => u.IsActive);
                entity.HasIndex(u => u.Email);
            });

            // Configure SystemAttributes table following database conventions
            modelBuilder.Entity<SystemAttribute>(entity =>
            {
                entity.ToTable("system_attributes", "petel_schema");
                entity.HasKey(s => s.Id);
                
                // Map actual database columns
                entity.Property(s => s.Id).HasColumnName("id");
                entity.Property(s => s.Name).HasColumnName("name").HasMaxLength(255);
                entity.Property(s => s.Value).HasColumnName("value");
                entity.Property(s => s.ValueType).HasColumnName("value_type").HasMaxLength(50);
                entity.Property(s => s.Description).HasColumnName("description");
                entity.Property(s => s.CreatedAt).HasColumnName("created_at");
                entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");
                entity.Property(s => s.UpdateUser).HasColumnName("update_user");
                entity.Property(s => s.ForeignId).HasColumnName("foreign_id");
                
                // Indexes for performance
                entity.HasIndex(s => s.Name);
                entity.HasIndex(s => s.ForeignId);
            });

            // Configure EntityTypes table
            modelBuilder.Entity<EntityType>(entity =>
            {
                entity.ToTable("entity_types", "petel_schema");
                entity.HasKey(et => et.Id);
                entity.Property(et => et.Id)
                    .HasColumnName("id");
                entity.Property(et => et.Name)
                    .HasColumnName("name")
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(et => et.CreatedAt)
                    .HasColumnName("created_at");
                entity.Property(et => et.UpdatedAt)
                    .HasColumnName("updated_at");

                entity.HasIndex(et => et.Name);
            });

            // Configure Roles table
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles", "petel_schema");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(r => r.CreatedAt).HasColumnName("created_at");
                entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");
                entity.HasIndex(r => r.Name).IsUnique();
            });

            // Configure UserRoles table
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles", "petel_schema");
                entity.HasKey(ur => ur.Id);
                entity.Property(ur => ur.Id).HasColumnName("id");
                entity.Property(ur => ur.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(ur => ur.RoleId).HasColumnName("role_id").IsRequired();
                entity.Property(ur => ur.CreatedAt).HasColumnName("created_at");
                entity.Property(ur => ur.UpdatedAt).HasColumnName("updated_at");
                entity.Property(ur => ur.UpdateUser).HasColumnName("update_user");

                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId);

                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId);
            });

            // Configure RolesActions table
            modelBuilder.Entity<RolesAction>(entity =>
            {
                entity.ToTable("roles_actions", "petel_schema");
                entity.HasKey(ra => ra.Id);
                entity.Property(ra => ra.Id).HasColumnName("id");
                entity.Property(ra => ra.ActionId).HasColumnName("action_id").IsRequired();
                entity.Property(ra => ra.RoleId).HasColumnName("role_id").IsRequired();
                entity.Property(ra => ra.ActionLevel).HasColumnName("action_level").IsRequired();
                entity.Property(ra => ra.UpdatedAt).HasColumnName("updated_at");
                entity.Property(ra => ra.UpdateUser).HasColumnName("update_user");

                entity.HasOne(ra => ra.Role)
                    .WithMany(r => r.RolesActions)
                    .HasForeignKey(ra => ra.RoleId);
            });

            // Configure SchoolYears table
            modelBuilder.Entity<SchoolYear>(entity =>
            {
                entity.ToTable("school_years", "petel_schema");
                entity.HasKey(sy => sy.Id);
                entity.Property(sy => sy.Id).HasColumnName("id");
                entity.Property(sy => sy.SchoolId).HasColumnName("school_id").IsRequired();
                entity.Property(sy => sy.YearName).HasColumnName("year_name").IsRequired().HasMaxLength(100);
                entity.Property(sy => sy.StartDate).HasColumnName("start_date").IsRequired();
                entity.Property(sy => sy.EndDate).HasColumnName("end_date").IsRequired();
                entity.Property(sy => sy.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(sy => sy.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(sy => sy.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Indexes for performance - school as tenant
                entity.HasIndex(sy => sy.SchoolId);
                entity.HasIndex(sy => sy.IsActive);
            });

            // Configure StudentSchoolYearsRegistrationSummaryVw view
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
        }
    }

    // Entity Models
    public class Entity
    {
        public int Id { get; set; }
        public int EntityTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PrincipalName { get; set; }
        public int? ApiConnectionId { get; set; }
        public bool IsActive { get; set; } = true;
        public string? SchoolLogo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? Symbol { get; set; }

        public string? InspectorName { get; set; } = string.Empty;

        public string? Characterization { get; set; }
        public string? ContactPerson { get; set; }
        public string? EducationStage { get; set; }

        public int? OwnerId { get; set; }

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual EntityType EntityType { get; set; } = null!;
        
        
    }

    public class User
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public bool IsActive { get; set; } = true;
        public int? UpdateUser { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Entity Entity { get; set; } = null!;
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }

    public class EntityType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Entity> Entities { get; set; } = new List<Entity>();
    }

    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<RolesAction> RolesActions { get; set; } = new List<RolesAction>();
    }

    public class UserRole
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdateUser { get; set; }

        // Navigation
        public virtual User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;
    }

    public class RolesAction
    {
        public int Id { get; set; }
        public int ActionId { get; set; }
        public int RoleId { get; set; }
        public int ActionLevel { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdateUser { get; set; }

        // Navigation
        public virtual Role Role { get; set; } = null!;
    }

    public class SchoolYear
    {
        public int Id { get; set; }
        public int SchoolId { get; set; } // This is the tenant ID
        public string YearName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StudentSchoolYearsRegistrationSummaryVw
    {
        public int SchoolId { get; set; } // This is the tenant ID
        public int SchoolYearId { get; set; }
        public string SchoolGrade { get; set; } = string.Empty;
        public string SchoolTrack { get; set; } = string.Empty;
        public int Registered { get; set; }
    }
}