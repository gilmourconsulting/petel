using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Models
{
    [Table("report_parameters")]
    public class ReportParameter
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("report_id")]
        public int ReportId { get; set; }

        [Required]
        [Column("param_name")]
        [MaxLength(100)]
        public string ParamName { get; set; } = string.Empty;

        [Required]
        [Column("param_label_he")]
        [MaxLength(150)]
        public string ParamLabelHe { get; set; } = string.Empty;

        /// <summary>year_selector | entity_selector | date_range | text | enum</summary>
        [Required]
        [Column("param_type")]
        [MaxLength(30)]
        public string ParamType { get; set; } = "text";

        [Column("is_required")]
        public bool IsRequired { get; set; } = true;

        [Column("default_value")]
        [MaxLength(500)]
        public string? DefaultValue { get; set; }

        /// <summary>JSON: [{value, label}] — for enum type only.</summary>
        [Column("options_json")]
        public string? OptionsJson { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ReportDefinition? Definition { get; set; }
    }
}
