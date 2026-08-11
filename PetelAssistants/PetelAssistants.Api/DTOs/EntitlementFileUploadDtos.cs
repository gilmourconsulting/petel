namespace PetelAssistants.Api.DTOs
{
    public class EntitlementFileRow
    {
        public int RowNumber { get; set; }
        public string InstitutionSymbol { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public string SupportType { get; set; } = string.Empty;
        public decimal AnnualHours { get; set; }
        public decimal ParticipationPct { get; set; }
        public string? GradeLayer { get; set; }
        public string? GradeParallel { get; set; }
        public string? ClassTypeCode { get; set; }
        public string? HebrewYear { get; set; }
        public bool ParseError { get; set; }
        public string? ParseErrorMessage { get; set; }
    }

    public class EntitlementOrphanDto
    {
        public int Id { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string AssistantTypeName { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public decimal MinistryParticipationPct { get; set; }
    }

    public class EntitlementFileProcessingResult
    {
        public int ProcessId { get; set; }
        public int Created { get; set; }
        public int Versioned { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorList { get; set; } = new();
        public List<EntitlementOrphanDto> Orphans { get; set; } = new();
    }

    public class EntitlementFilePreviewRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    public class EntitlementFileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? MappingJson { get; set; }
        public int YearId { get; set; }
        public bool SaveMapping { get; set; }
    }

    public class EntitlementFieldMappingSaveRequest
    {
        public string MappingJson { get; set; } = string.Empty;
    }

    public class EntitlementCancelOrphansRequest
    {
        public int YearId { get; set; }
        public List<int> EntitlementIds { get; set; } = new();
    }
}
