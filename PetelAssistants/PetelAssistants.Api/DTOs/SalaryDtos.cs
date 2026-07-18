namespace PetelAssistants.Api.DTOs
{
    public class SalaryListItemDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string NationalId { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public decimal PositionPercentage { get; set; }
        public decimal TotalSalary { get; set; }
        public int? MatchedPersonId { get; set; }
        public string? MatchedPersonName { get; set; }
        public bool HasIdWarning { get; set; }
        public int ProcessId { get; set; }
    }
}
