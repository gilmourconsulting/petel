namespace PetelAssistants.Api.DTOs
{
    public class YearHubSummaryDto
    {
        public int AssistantCount { get; set; }
        public int EntitlementCount { get; set; }
        public decimal EntitlementAllocatedPercent { get; set; }
        public YearHubBudgetSummaryDto? Budget { get; set; }
        public decimal SalaryYtdTotal { get; set; }
        public int? LastSalaryPeriodYear { get; set; }
        public int? LastSalaryPeriodMonth { get; set; }
        public int MeitarMonthCount { get; set; }
    }

    public class YearHubBudgetSummaryDto
    {
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
