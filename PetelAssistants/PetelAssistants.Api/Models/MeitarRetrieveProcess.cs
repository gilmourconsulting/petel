using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_retrieve_processes")]
    public class MeitarRetrieveProcess : IEntityScoped
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

        [Column("total_calculated_sum")]
        public decimal? TotalCalculatedSum { get; set; }

        [Required]
        [Column("source")]
        [MaxLength(20)]
        public string Source { get; set; } = "meitar";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        [Column("mucarim_row_count")]
        public int? MucarimRowCount { get; set; }

        [Column("mucarim_total_calculated_sum")]
        public decimal? MucarimTotalCalculatedSum { get; set; }

        [Column("mucarim_error")]
        public string? MucarimError { get; set; }

        [Column("sharatim_row_count")]
        public int? SharatimRowCount { get; set; }

        [Column("sharatim_total_class_count")]
        public int? SharatimTotalClassCount { get; set; }

        [Column("sharatim_error")]
        public string? SharatimError { get; set; }

        public virtual ICollection<MeitarMutavim> Rows { get; set; } = new List<MeitarMutavim>();
        public virtual ICollection<MeitarMucarim> MucarimRows { get; set; } = new List<MeitarMucarim>();
        public virtual ICollection<MeitarSharatim> SharatimRows { get; set; } = new List<MeitarSharatim>();
        public virtual ICollection<MeitarMonthSummary> Summaries { get; set; } = new List<MeitarMonthSummary>();
        public virtual ICollection<YearlyBudgetComparison> BudgetComparisons { get; set; } = new List<YearlyBudgetComparison>();
    }
}
