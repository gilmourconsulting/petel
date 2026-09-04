namespace PetelAssistants.BlazorServer.DTOs
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
        public List<YearlyBudgetComparisonDto> Comparisons { get; set; } = new();
    }

    public class YearlyBudgetComparisonDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public int? AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public decimal BudgetFte { get; set; }
        public decimal BudgetHours { get; set; }
        public decimal SalaryAmount { get; set; }
        public decimal SalaryFte { get; set; }
        public decimal SalaryHours { get; set; }
        public int SalaryRowCount { get; set; }
        public decimal MeitarAmount { get; set; }
        public decimal MeitarHours { get; set; }
        public int MeitarRowCount { get; set; }
        public decimal SalaryAmountVariance => SalaryAmount - BudgetAmount;
        public decimal SalaryFteVariance => SalaryFte - BudgetFte;
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
        public List<UpdateYearlyBudgetDetailRequest> Details { get; set; } = new();
    }

    public class UpdateYearlyBudgetDetailRequest
    {
        public int AssistantTypeId { get; set; }
        public decimal Fte { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public string? Remarks { get; set; }
    }
}
