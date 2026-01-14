namespace PetelApp.BlazorServer.DTOs
{
    public class AdditionalStudyProgramDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
        public int Sessions { get; set; }
        public int NumberOfStudents { get; set; }
        public decimal? Cost { get; set; }
        public decimal? HourlyCost { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public int? ApprovalStatus { get; set; }
        public int Version { get; set; }
        public int? MasterId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUsername { get; set; }
    }

    public class AdditionalStudyProgramsResponse
    {
        public bool Success { get; set; }
        public List<AdditionalStudyProgramDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    public class CreateProgramRequest
    {
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
        public int Sessions { get; set; }
        public int NumberOfStudents { get; set; }
        public decimal? Cost { get; set; }
        public decimal? HourlyCost { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public int? ApprovalStatus { get; set; }
        public string CalculationMode { get; set; } = "totalCost";
    }

    public class UpdateProgramRequest : CreateProgramRequest
    {
        public int MasterId { get; set; }
        public int Version { get; set; }
    }

    public class ProgramVersionHistoryDto
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByUsername { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
        public int Sessions { get; set; }
        public int NumberOfStudents { get; set; }
        public decimal? Cost { get; set; }
        public bool IsCurrent { get; set; }
    }
}
