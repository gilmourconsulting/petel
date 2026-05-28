using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Models
{
    [Table("report_definitions")]
    public class ReportDefinition
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>query_builder | advanced_sql | template</summary>
        [Required]
        [Column("report_type")]
        [MaxLength(30)]
        public string ReportType { get; set; } = "query_builder";

        /// <summary>
        /// When true, year_selector parameter becomes optional (account entities only).
        /// Server rejects this flag for non-account entities.
        /// </summary>
        [Column("allow_cross_year")]
        public bool AllowCrossYear { get; set; } = false;

        /// <summary>
        /// When true, the run modal injects an entity_context_selector for system admins.
        /// Set to false for system-level reports (Users, SystemAttributes).
        /// </summary>
        [Column("requires_entity_context")]
        public bool RequiresEntityContext { get; set; } = true;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>Null = any logged-in user may run this report.</summary>
        [Column("required_action_id")]
        public int? RequiredActionId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        /// <summary>
        /// JSON string conforming to Petel.Core.Excel.ReportDefinition.
        /// When non-null and ReportType == "template", the ReportTemplateEngine
        /// is used instead of the legacy scalar-only FillTemplate path.
        /// </summary>
        [Column("definition_json")]
        public string? DefinitionJson { get; set; }

        /// <summary>"excel" | "word"</summary>
        [Column("format")]
        [MaxLength(10)]
        public string Format { get; set; } = "excel";

        // Navigation properties
        public virtual ReportQuery? Query { get; set; }
        public virtual ReportTemplate? Template { get; set; }
        public virtual ICollection<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
    }
}
