using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Hours budget entity following database conventions
    /// Maps to petel_schema.hours_budgets table with multi-tenant architecture
    /// </summary>
    [Table("hours_budgets", Schema = "petel_schema")]
    public class HoursBudget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("school_id")]
        public int SchoolId { get; set; } // Tenant ID

        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Column("budget_name")]
        [Required]
        [MaxLength(255)]
        public string BudgetName { get; set; } = string.Empty;

        [Column("allocated_hours")]
        [Required]
        public decimal AllocatedHours { get; set; }

        [Column("used_hours")]
        public decimal UsedHours { get; set; } = 0;

        [Column("remaining_hours")]
        public decimal RemainingHours => AllocatedHours - UsedHours;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual SchoolYear SchoolYear { get; set; } = null!;
    }
}