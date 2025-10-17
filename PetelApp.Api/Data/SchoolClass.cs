using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.school_classes table
    /// Represents school classes (כיתות לימוד)
    /// </summary>
    [Table("school_classes", Schema = "petel_schema")]
    public class SchoolClass
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_year")]
        public int SchoolYear { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(6)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("level")]
        [MaxLength(3)]
        public string Level { get; set; } = string.Empty;

        [Required]
        [Column("class_number")]
        [MaxLength(3)]
        public string ClassNumber { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
