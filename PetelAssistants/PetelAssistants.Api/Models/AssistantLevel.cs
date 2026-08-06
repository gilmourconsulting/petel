using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("assistant_levels")]
    public class AssistantLevel
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("code")]
        [MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("display_name")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
