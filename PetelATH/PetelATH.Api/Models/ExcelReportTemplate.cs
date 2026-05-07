using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Models
{
    [Table("excel_report_templates")]
    public class ExcelReportTemplate
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("report_id")]
        public int ReportId { get; set; }

        [Required]
        [Column("template_filename")]
        [MaxLength(255)]
        public string TemplateFilename { get; set; } = string.Empty;

        [Required]
        [Column("template_blob")]
        public byte[] TemplateBlob { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// JSON: [{placeholder, entity_name, field_name, is_collection}]
        /// placeholder = the {{Name}} text found in the template cell.
        /// </summary>
        [Column("cell_mappings_json")]
        public string CellMappingsJson { get; set; } = "[]";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ExcelReportDefinition? Definition { get; set; }
    }
}
