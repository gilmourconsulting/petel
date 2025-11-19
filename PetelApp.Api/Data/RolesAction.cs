using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("roles_actions")]
    public class RolesAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("action_id")]
        public int ActionId { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("action_level")]
        public int ActionLevel { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation properties
        public virtual Role Role { get; set; } = null!;
    }
}