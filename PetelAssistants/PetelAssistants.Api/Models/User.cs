using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    /// <summary>
    /// Tenant-scoped user account. Belongs to a single local authority (EntityId).
    /// Entity navigation is intentionally absent — Entity lives in shared_schema (SharedDbContext).
    /// </summary>
    [Table("users")]
    public class User : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>FK to shared_schema.entities — the owning local authority.</summary>
        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("username")]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("first_name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [Column("last_name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [Column("email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("phone")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Column("is_locked")]
        public bool IsLocked { get; set; } = false;

        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }

        [Column("locked_by")]
        public int? LockedBy { get; set; }

        [Column("lock_reason_id")]
        public int? LockReasonId { get; set; }

        [Column("failed_password_attempts")]
        public int FailedPasswordAttempts { get; set; } = 0;

        [Column("failed_otp_attempts")]
        public int FailedOtpAttempts { get; set; } = 0;

        [Column("password_changed_at")]
        public DateTime PasswordChangedAt { get; set; } = DateTime.UtcNow;

        [Column("password_change_required")]
        public bool PasswordChangeRequired { get; set; } = false;

        [Column("otp_secret")]
        [MaxLength(255)]
        public string? OtpSecret { get; set; }

        [Column("otp_enabled")]
        public bool OtpEnabled { get; set; } = false;

        [Column("otp_verified")]
        public bool OtpVerified { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        /// <summary>LockReason navigation is intentionally absent — UserLockReason lives in shared_schema (SharedDbContext).</summary>
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
