using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_month_summaries")]
    public class MeitarMonthSummary : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("process_id")]
        public int ProcessId { get; set; }

        [Required]
        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Required]
        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Column("assistant_type_id")]
        public int? AssistantTypeId { get; set; }

        [Required]
        [Column("row_count")]
        public int RowCount { get; set; }

        [Required]
        [Column("fte")]
        public decimal Fte { get; set; }

        [Required]
        [Column("hours")]
        public decimal Hours { get; set; }

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("yearly_budget_id")]
        public int? YearlyBudgetId { get; set; }

        [Column("budget_fte")]
        public decimal? BudgetFte { get; set; }

        [Column("budget_hours")]
        public decimal? BudgetHours { get; set; }

        [Column("budget_amount")]
        public decimal? BudgetAmount { get; set; }

        [Required]
        [Column("has_budget")]
        public bool HasBudget { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual MeitarRetrieveProcess? Process { get; set; }
        public virtual YearlyBudget? YearlyBudget { get; set; }
    }
}
