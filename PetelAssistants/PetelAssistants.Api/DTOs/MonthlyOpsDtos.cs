namespace PetelAssistants.Api.DTOs
{
    public class StatusDto
    {
        public int Id { get; set; }
        public string Object { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class SalaryDepartmentMappingDto
    {
        public int Id { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SaveSalaryDepartmentMappingRequest
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int AssistantTypeId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UnmappedSalaryDepartmentDto
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int RowCount { get; set; }
        public decimal TotalSalary { get; set; }
    }

    public class MonthSummaryLineDto
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public int? AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public decimal Fte { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public int? YearlyBudgetId { get; set; }
        public decimal? BudgetFte { get; set; }
        public decimal? BudgetHours { get; set; }
        public decimal? BudgetAmount { get; set; }
        public bool HasBudget { get; set; }
        public decimal AmountVariance => Amount - (BudgetAmount ?? 0);
        public decimal FteVariance => Fte - (BudgetFte ?? 0);
        public decimal HoursVariance => Hours - (BudgetHours ?? 0);
    }

    public class MonthSummaryResponse
    {
        public int? ProcessId { get; set; }
        public bool HasBudget { get; set; }
        public List<MonthSummaryLineDto> Lines { get; set; } = new();
    }

    public class YearMonthSummariesResponse
    {
        public List<MonthSummaryLineDto> Lines { get; set; } = new();
    }

    public class SalaryAnomalyDto
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        public int? SalaryId { get; set; }
        public string NationalId { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public decimal PositionPercentage { get; set; }
        public decimal TotalSalary { get; set; }
        public int? MatchedPersonId { get; set; }
        public string? MatchedPersonName { get; set; }
        public int? MatchedAllocationId { get; set; }
        public int? MappedAssistantTypeId { get; set; }
        public string? MappedAssistantTypeName { get; set; }
        public int? AllocationAssistantTypeId { get; set; }
        public string? AllocationAssistantTypeName { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class UpdateSalaryAnomalyStatusRequest
    {
        public int StatusId { get; set; }
        public string? Notes { get; set; }
    }
}
