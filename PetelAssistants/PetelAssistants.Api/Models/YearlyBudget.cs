using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    public static class YearlyBudgetStatuses
    {
        public const string Open    = "open";
        public const string Locked  = "locked";
        public const string Deleted = "deleted";
    }

    [Table("yearly_budgets")]
    public class YearlyBudget : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Required]
        [Column("master_yearly_budget_id")]
        public int MasterYearlyBudgetId { get; set; }

        [Required]
        [Column("version")]
        public int Version { get; set; }

        [Required]
        [Column("is_last_version")]
        public bool IsLastVersion { get; set; } = true;

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = YearlyBudgetStatuses.Open;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual ICollection<YearlyBudgetDetail> Details { get; set; } = new List<YearlyBudgetDetail>();
        public virtual ICollection<YearlyBudgetMonthDetail> MonthDetails { get; set; } = new List<YearlyBudgetMonthDetail>();
        public virtual ICollection<YearlyBudgetComparison> Comparisons { get; set; } = new List<YearlyBudgetComparison>();
    }

    [Table("yearly_budget_details")]
    public class YearlyBudgetDetail : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("yearly_budget_id")]
        public int YearlyBudgetId { get; set; }

        [Required]
        [Column("assistant_type_id")]
        public int AssistantTypeId { get; set; }

        [Required]
        [Column("fte")]
        public decimal Fte { get; set; }

        [Required]
        [Column("hours")]
        public decimal Hours { get; set; }

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual YearlyBudget? YearlyBudget { get; set; }
    }

    [Table("yearly_budget_month_details")]
    public class YearlyBudgetMonthDetail : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("yearly_budget_id")]
        public int YearlyBudgetId { get; set; }

        [Required]
        [Column("assistant_type_id")]
        public int AssistantTypeId { get; set; }

        [Required]
        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Required]
        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Required]
        [Column("fte")]
        public decimal Fte { get; set; }

        [Required]
        [Column("hours")]
        public decimal Hours { get; set; }

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual YearlyBudget? YearlyBudget { get; set; }
    }

    [Table("yearly_budget_comparisons")]
    public class YearlyBudgetComparison : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("yearly_budget_id")]
        public int YearlyBudgetId { get; set; }

        [Required]
        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Required]
        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Column("assistant_type_id")]
        public int? AssistantTypeId { get; set; }

        [Required]
        [Column("budget_fte")]
        public decimal BudgetFte { get; set; }

        [Required]
        [Column("budget_hours")]
        public decimal BudgetHours { get; set; }

        [Required]
        [Column("budget_amount")]
        public decimal BudgetAmount { get; set; }

        [Required]
        [Column("salary_row_count")]
        public int SalaryRowCount { get; set; }

        [Required]
        [Column("salary_fte")]
        public decimal SalaryFte { get; set; }

        [Required]
        [Column("salary_hours")]
        public decimal SalaryHours { get; set; }

        [Required]
        [Column("salary_amount")]
        public decimal SalaryAmount { get; set; }

        [Column("salary_process_id")]
        public int? SalaryProcessId { get; set; }

        [Required]
        [Column("meitar_row_count")]
        public int MeitarRowCount { get; set; }

        [Required]
        [Column("meitar_hours")]
        public decimal MeitarHours { get; set; }

        [Required]
        [Column("meitar_amount")]
        public decimal MeitarAmount { get; set; }

        [Column("meitar_process_id")]
        public int? MeitarProcessId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual YearlyBudget? YearlyBudget { get; set; }
        public virtual SalaryUploadProcess? SalaryProcess { get; set; }
        public virtual MeitarRetrieveProcess? MeitarProcess { get; set; }
    }
}
