using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("system_attributes")]
    public class SystemAttribute
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("value")]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        [Column("value_type")]
        [MaxLength(50)]
        public string ValueType { get; set; } = "string";

        [Column("description")]
        [MaxLength(200)]
        public string? Description { get; set; }
    }
}
