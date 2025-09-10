using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Add this for Precision attribute

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

        [Column("title")]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("school_id")]
        public int? SchoolId { get; set; }

        [Column("budgeted_hours")]
        [Precision(10, 2)]
        public decimal BudgetedHours { get; set; }

        [Column("actual_hours")]
        [Precision(10, 2)]
        public decimal ActualHours { get; set; }

        [Column("status")]
        [StringLength(50)]
        public string? Status { get; set; }

        [Column("tenant_id")]
        [StringLength(100)]
        public string? Tenant { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}