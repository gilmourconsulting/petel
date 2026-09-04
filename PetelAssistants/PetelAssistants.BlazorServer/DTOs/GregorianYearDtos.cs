namespace PetelAssistants.BlazorServer.DTOs
{
    public class GregorianYearDto
    {
        public int Year { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPrevious { get; set; }
    }

    public class GregorianYearContextDto
    {
        public GregorianYearDto? CurrentYear { get; set; }
        public GregorianYearDto? PreviousYear { get; set; }
        public List<GregorianYearDto> AllYears { get; set; } = new();
    }

    public class GregorianHubSummaryDto
    {
        public int CalendarYear { get; set; }
        public int AssistantCount { get; set; }
        public int EntitlementCount { get; set; }
        public decimal EntitlementAllocatedPercent { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal BudgetHours { get; set; }
        public decimal BudgetYtd { get; set; }
        public decimal SalaryYtdTotal { get; set; }
        public decimal MeitarYtdTotal { get; set; }
        public decimal NetMunicipal { get; set; }
        public decimal Variance { get; set; }
        public int? LastSalaryPeriodYear { get; set; }
        public int? LastSalaryPeriodMonth { get; set; }
        public int MeitarMonthCount { get; set; }
        public List<GregorianBudgetSourceDto> Sources { get; set; } = new();
    }

    public class GregorianBudgetSourceDto
    {
        public int HebrewYearId { get; set; }
        public string HebrewYearName { get; set; } = string.Empty;
        public int FromMonth { get; set; }
        public int ToMonth { get; set; }
        public int? YearlyBudgetId { get; set; }
        public int? Version { get; set; }
        public string Status { get; set; } = "none";
        public bool HasBudget { get; set; }
        public bool IsLocked { get; set; }
    }

    public class GregorianBudgetDto
    {
        public int CalendarYear { get; set; }
        public List<GregorianBudgetSourceDto> Sources { get; set; } = new();
        public List<YearlyBudgetDetailDto> Details { get; set; } = new();
        public List<YearlyBudgetMonthDetailDto> MonthDetails { get; set; } = new();
        public List<YearlyBudgetComparisonDto> Comparisons { get; set; } = new();
    }

    public class GregorianAssistantDto
    {
        public int Id { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneSummary { get; set; }
        public int HebrewYearId { get; set; }
        public string HebrewYearName { get; set; } = string.Empty;
    }
}
