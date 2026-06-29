using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("menu_items")]
    public class MenuItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("reference")]
        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [Required]
        [Column("text")]
        [MaxLength(100)]
        public string Text { get; set; } = string.Empty;

        [Column("action_id")]
        public int? ActionId { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
