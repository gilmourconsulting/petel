using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Models
{
    [Table("report_queries")]
    public class ReportQuery
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("report_id")]
        public int ReportId { get; set; }

        /// <summary>Null for advanced_sql type; e.g. "Students", "Schools".</summary>
        [Column("entity_name")]
        [MaxLength(100)]
        public string? EntityName { get; set; }

        /// <summary>JSON: [{field, label_override}]</summary>
        [Column("fields_json")]
        public string FieldsJson { get; set; } = "[]";

        /// <summary>JSON: [{field, operator, value, param_name}]</summary>
        [Column("filters_json")]
        public string FiltersJson { get; set; } = "[]";

        /// <summary>JSON: [{field, direction}]</summary>
        [Column("sort_json")]
        public string SortJson { get; set; } = "[]";

        /// <summary>Null for query_builder type; raw SQL for advanced_sql type.</summary>
        [Column("sql_query")]
        public string? SqlQuery { get; set; }

        [Column("sheet_name")]
        [MaxLength(100)]
        public string SheetName { get; set; } = "נתונים";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ReportDefinition? Definition { get; set; }
    }
}
