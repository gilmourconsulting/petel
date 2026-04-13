using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    /// <summary>
    /// Hours budget entity following database conventions
    /// Maps to petel_schema.hours_budgets table with multi-tenant architecture
    /// </summary>
    [Table("hours_budget")]
    public class HoursBudget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public string EntityId { get; set; } = string.Empty; // Required for Entity-Based Request Flow

        [Column("school_year")]
        public string? SchoolYear { get; set; }

        [Column("budget_type")]
        public string? BudgetType { get; set; }

        [Column("allocated_hours")]
        public decimal AllocatedHours { get; set; }

        [Column("used_hours")]
        public decimal UsedHours { get; set; }

        [Column("remaining_hours")]
        public decimal RemainingHours { get; set; }

        [Column("department")]
        public string? Department { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}