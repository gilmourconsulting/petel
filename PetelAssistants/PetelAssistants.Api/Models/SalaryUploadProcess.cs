using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("salary_upload_processes")]
    public class SalaryUploadProcess : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Required]
        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Column("row_count")]
        public int? RowCount { get; set; }

        [Column("total_salary_sum")]
        public decimal? TotalSalarySum { get; set; }

        [Required]
        [Column("source")]
        [MaxLength(20)]
        public string Source { get; set; } = "manual";

        [Column("file_name")]
        [MaxLength(255)]
        public string? FileName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual ICollection<Salary> Salaries { get; set; } = new List<Salary>();
        public virtual ICollection<SalaryUploadWarning> Warnings { get; set; } = new List<SalaryUploadWarning>();
        public virtual ICollection<SalaryMonthSummary> Summaries { get; set; } = new List<SalaryMonthSummary>();
        public virtual ICollection<SalaryAnomaly> Anomalies { get; set; } = new List<SalaryAnomaly>();
    }
}
