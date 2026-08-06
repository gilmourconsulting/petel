using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("assistant_types")]
    public class AssistantType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("display_name")]
        [MaxLength(150)]
        public string DisplayName { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(255)]
        public string? Description { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("level")]
        [MaxLength(30)]
        public string? Level { get; set; }

        /// <summary>weekly or monthly (סוג משרה).</summary>
        [Column("position_type")]
        [MaxLength(20)]
        public string? PositionType { get; set; }

        /// <summary>שעות משרה.</summary>
        [Column("position_hours")]
        public decimal? PositionHours { get; set; }
    }
}
