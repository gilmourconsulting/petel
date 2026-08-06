using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_topics")]
    public class MeitarTopic
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
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("position_type")]
        [MaxLength(20)]
        public string? PositionType { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
