using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("otp_secret")]
        public string? OtpSecret { get; set; }

        [Column("otp_enabled")]
        public bool OtpEnabled { get; set; } = false;

        [Column("otp_verified")]
        public bool OtpVerified { get; set; } = false;

        // ✅ NEW: User locking fields
        [Column("is_locked")]
        public bool IsLocked { get; set; } = false;

        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }

        [Column("locked_by")]
        public int? LockedBy { get; set; }

        [Column("failed_password_attempts")]
        public int FailedPasswordAttempts { get; set; } = 0;

        [Column("failed_otp_attempts")]
        public int FailedOtpAttempts { get; set; } = 0;

        [Column("last_failed_attempt")]
        public DateTime? LastFailedAttempt { get; set; }

        // ✅ NEW: Password expiration fields
        [Column("password_changed_at")]
        public DateTime PasswordChangedAt { get; set; } = DateTime.UtcNow;

        [Column("password_change_required")]
        public bool PasswordChangeRequired { get; set; } = false;

        // Navigation properties following Entity-Based Request Flow
        public virtual Entity Entity { get; set; } = null!;
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        // ✅ Helper method to check if password is expired (no [NotMapped] needed for methods)
        public bool IsPasswordExpired(int expirationMonths)
        {
            if (expirationMonths <= 0) return false; // Password expiration disabled
            
            var expirationDate = PasswordChangedAt.AddMonths(expirationMonths);
            return DateTime.UtcNow > expirationDate;
        }
    }
}