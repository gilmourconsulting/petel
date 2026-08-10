namespace PetelAssistants.BlazorServer.DTOs
{
    public class ClassAssistantBudgetHoursDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public string SchoolLevel { get; set; } = string.Empty;
        public int ClassClassificationId { get; set; }
        public string ClassClassificationName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }

    public class UpsertClassAssistantBudgetHoursRequest
    {
        public int HebrewYearId { get; set; }
        public List<ClassAssistantBudgetHoursLineRequest> Lines { get; set; } = new();
    }

    public class ClassAssistantBudgetHoursLineRequest
    {
        public string SchoolLevel { get; set; } = string.Empty;
        public int ClassClassificationId { get; set; }
        public decimal Hours { get; set; }
    }

    public class CalculateYearlyBudgetResultDto
    {
        public YearlyBudgetDto Budget { get; set; } = new();
        public decimal TotalHours { get; set; }
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
