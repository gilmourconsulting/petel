namespace PetelAssistants.Api.DTOs
{
    public class PersonalApprovalRow
    {
        public int PageNumber { get; set; }
        public string? ApprovalDate { get; set; }
        public string? CouncilName { get; set; }
        public string? CouncilSymbol { get; set; }
        public string? StudentFirstName { get; set; }
        public string? StudentLastName { get; set; }
        public string? StudentId { get; set; }
        public string? SupportCode { get; set; }
        public string? Framework { get; set; }
        public string? InstitutionName { get; set; }
        public string? InstitutionSymbol { get; set; }
        public string? Hours { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public decimal? ParticipationPct { get; set; }
        public string? Error { get; set; }
    }

    public class PersonalApprovalsPdfConvertResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FileName { get; set; }
        public string? ContentBase64 { get; set; }
        public int RowCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
