using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_data_filter_values")]
    public class MeitarDataFilterValue
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("file_name")]
        [MaxLength(50)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Column("filter_field")]
        [MaxLength(100)]
        public string FilterField { get; set; } = string.Empty;

        [Required]
        [Column("filter_value")]
        [MaxLength(500)]
        public string FilterValue { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("display_order")]
        public int DisplayOrder { get; set; }
    }
}
