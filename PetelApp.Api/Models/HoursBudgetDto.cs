namespace PetelApp.Api.Models
{
    /// <summary>
    /// Hours budget DTO following project-specific patterns
    /// Used for API responses and business logic operations
    /// </summary>
    public class HoursBudgetDto
    {
        public int Id { get; set; }
        public int SchoolId { get; set; }
        public int SchoolYearId { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        public decimal AllocatedHours { get; set; }
        public decimal UsedHours { get; set; }
        public decimal RemainingHours { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SchoolYearName { get; set; } = string.Empty;
    }
}