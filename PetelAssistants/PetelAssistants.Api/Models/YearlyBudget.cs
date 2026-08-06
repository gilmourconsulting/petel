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
}
