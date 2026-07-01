using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("user_lock_reasons")]
    public class UserLockReason
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(200)]
        public string? Description { get; set; }

        [Column("allow_forgot_password")]
        public bool AllowForgotPassword { get; set; } = true;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public const string LoginAttemptsExceeded = "LOGIN_ATTEMPTS_EXCEEDED";
        public const string AdminLocked = "ADMIN_LOCKED";
    }
}
