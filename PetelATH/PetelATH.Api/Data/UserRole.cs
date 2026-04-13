using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("user_roles")]
    public class UserRole
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }



        [Required]
        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("update_user")]
        public int UpdateUserId { get; set; }

        // Navigation properties following Entity-Based Request Flow
        public virtual User? User { get; set; }
        public virtual Role? Role { get; set; }
    }
}