namespace PetelAssistants.Api.DTOs
{
    public class SalaryFileRow
    {
        public int RowNumber { get; set; }
        public string NationalId { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public decimal PositionPercentage { get; set; }
        public decimal TotalSalary { get; set; }
        public bool ParseError { get; set; }
        public string? ParseErrorMessage { get; set; }
    }

    public class SalaryFileProcessingResult
    {
        public int ProcessId { get; set; }
        public int Created { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public decimal TotalSalarySum { get; set; }
        public List<string> ErrorList { get; set; } = new();
        public List<string> WarningList { get; set; } = new();
    }

    public class SalaryFilePreviewRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    public class SalaryFileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? MappingJson { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public bool ReplaceExisting { get; set; }
        public bool IdIncludesCheckDigit { get; set; } = true;
        public bool SaveMapping { get; set; }
    }

    public class SalaryFieldMappingSaveRequest
    {
        public string MappingJson { get; set; } = string.Empty;
        public bool IdIncludesCheckDigit { get; set; } = true;
    }
}
