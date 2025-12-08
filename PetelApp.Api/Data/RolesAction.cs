using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Maps roles to actions, establishing which actions each role can perform
    /// Follows the Authentication & Session Management pattern
    /// </summary>
    [Table("roles_actions")]
    public class RolesAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to roles table
        /// </summary>
        [Required]
        [ForeignKey("Role")]
        [Column("role_id")]
        public int RoleId { get; set; }

        /// <summary>
        /// Foreign key to actions table
        /// </summary>
        [Required]
        [ForeignKey("SystemAction")]
        [Column("action_id")]
        public int ActionId { get; set; }

        /// <summary>
        /// Permission level (0=no access, 1=view, 2=edit, 3=admin)
        /// Can be extended based on application needs
        /// </summary>
        [Column("action_level")]
        public int ActionLevel { get; set; } = 0;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation properties - following Entity-Based Request Flow pattern
        public virtual Role Role { get; set; } = null!;
        public virtual SystemAction SystemAction { get; set; } = null!;
    }
}