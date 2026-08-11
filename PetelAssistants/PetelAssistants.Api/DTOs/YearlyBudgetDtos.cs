using System.ComponentModel.DataAnnotations;

namespace PetelAssistants.Api.DTOs
{
    public class YearlyBudgetDto
    {
        public int Id { get; set; }
        public int MasterYearlyBudgetId { get; set; }
        public int HebrewYearId { get; set; }
        public string HebrewYearName { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsLastVersion { get; set; }
        public bool CanEdit { get; set; }
        public bool CanLock { get; set; }
        public bool CanCreateNewVersion { get; set; }
        public bool CanDelete { get; set; }
        public List<YearlyBudgetVersionItemDto> Versions { get; set; } = new();
        public List<YearlyBudgetDetailDto> Details { get; set; } = new();
        public List<YearlyBudgetMonthDetailDto> MonthDetails { get; set; } = new();
    }

    public class YearlyBudgetVersionItemDto
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsLastVersion { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class YearlyBudgetDetailDto
    {
        public int Id { get; set; }
        public int AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public decimal Fte { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public string? Remarks { get; set; }
    }

    public class YearlyBudgetMonthDetailDto
    {
        public int Id { get; set; }
        public int AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal Fte { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateYearlyBudgetRequest
    {
        [Required]
        public List<UpdateYearlyBudgetDetailRequest> Details { get; set; } = new();
    }

    public class UpdateYearlyBudgetDetailRequest
    {
        [Required]
        public int AssistantTypeId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Fte { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Hours { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        public string? Remarks { get; set; }
    }

    public class CalculateYearlyBudgetResultDto
    {
        public YearlyBudgetDto Budget { get; set; } = new();
        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
        public int EntitlementCount { get; set; }
        public int SuccessCount { get; set; }
        public List<CalculateBudgetFailureDto> Failures { get; set; } = new();
    }

    public class CalculateBudgetFailureDto
    {
        public int EntitlementId { get; set; }
        public int MasterEntitlementId { get; set; }
        public string? InstitutionName { get; set; }
        public string? ClassName { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
