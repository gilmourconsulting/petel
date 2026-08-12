namespace PetelAssistants.Api.DTOs
{
    public class PersonalEntitlementFileRow
    {
        public int RowNumber { get; set; }
        public string PupilIdNumber { get; set; } = string.Empty;
        public string PupilFirstName { get; set; } = string.Empty;
        public string PupilLastName { get; set; } = string.Empty;
        public string InstitutionSymbol { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        /// <summary>Municipality participation % from file (before converting to ministry %).</summary>
        public decimal AuthorityParticipationPct { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool ParseError { get; set; }
        public string? ParseErrorMessage { get; set; }
    }

    public class PersonalEntitlementOrphanDto
    {
        public int Id { get; set; }
        public string? PupilIdNumber { get; set; }
        public string? PupilFirstName { get; set; }
        public string? PupilLastName { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public decimal MinistryParticipationPct { get; set; }
    }

    public class PersonalEntitlementFileProcessingResult
    {
        public int ProcessId { get; set; }
        public int Created { get; set; }
        public int Versioned { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorList { get; set; } = new();
        public List<PersonalEntitlementOrphanDto> Orphans { get; set; } = new();
    }

    public class PersonalEntitlementFilePreviewRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    public class PersonalEntitlementFileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? MappingJson { get; set; }
        public int YearId { get; set; }
        public bool SaveMapping { get; set; }
    }

    public class PersonalEntitlementFieldMappingSaveRequest
    {
        public string MappingJson { get; set; } = string.Empty;
    }

    public class PersonalEntitlementCancelOrphansRequest
    {
        public int YearId { get; set; }
        public List<int> EntitlementIds { get; set; } = new();
    }
}
