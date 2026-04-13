using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    /// <summary>
    /// Stores action types such as 'menu_item', 'button', 'page_action', 'api_endpoint'
    /// Allows categorization of actions for different security contexts
    /// </summary>
    [Table("action_types")]
    public class ActionType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(255)]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int UserId { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<SystemAction> Actions { get; set; } = new List<SystemAction>();
    }
}