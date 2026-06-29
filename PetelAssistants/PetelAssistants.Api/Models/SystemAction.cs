using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("actions")]
    public class SystemAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("display_name")]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [Column("reference")]
        [MaxLength(200)]
        public string? Reference { get; set; }

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column("action_type_id")]
        public int ActionTypeId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ActionType? ActionType { get; set; }
        public virtual ICollection<RolesAction> RolesActions { get; set; } = new List<RolesAction>();
    }
}
