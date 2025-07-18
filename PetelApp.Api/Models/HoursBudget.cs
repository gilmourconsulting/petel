using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Models
{
    public class HoursBudget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }
        
        [Column("tenant_id")]
        public int TenantId { get; set; }
        
        [Column("hours_budget_type")]
        public string HoursBudgetType { get; set; } = string.Empty;
        
        [Column("allocated_hours")]
        public decimal AllocatedHours { get; set; }
        
        [Column("used_hours")]
        public decimal UsedHours { get; set; }
        
        [Column("remaining_hours")]
        public decimal RemainingHours { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("created_by")]
        public int? CreatedBy { get; set; }
        
        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
