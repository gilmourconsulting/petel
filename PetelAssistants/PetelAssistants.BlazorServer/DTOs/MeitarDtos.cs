namespace PetelAssistants.BlazorServer.DTOs
{
    public class MeitarMutavimListItemDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string BeneficiaryCode { get; set; } = string.Empty;
        public DateOnly CalcDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? TopicCode { get; set; }
        public string? TopicDescription { get; set; }
        public decimal? UnitCount { get; set; }
        public decimal? Cost { get; set; }
        public decimal? ParticipationPercent { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal? PreviousCalculatedAmount { get; set; }
        public decimal? CalculatedDifference { get; set; }
    }

    public class MeitarMucarimListItemDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string BeneficiaryCode { get; set; } = string.Empty;
        public DateOnly CalcDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? InstitutionCode { get; set; }
        public string? InstitutionName { get; set; }
        public string? TopicCode { get; set; }
        public string? TopicDescription { get; set; }
        public string? Status { get; set; }
        public decimal? UnitCount { get; set; }
        public decimal? Percent { get; set; }
        public decimal? Cost { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal? PreviousCalculatedAmount { get; set; }
        public decimal? CalculatedDifference { get; set; }
        public string? UnitDescription { get; set; }
        public int ActiveMatchCount { get; set; }
    }

    public class MeitarMucarimEntitlementMatchResultDto
    {
        public int ProcessId { get; set; }
        public int DisabledCount { get; set; }
        public int MatchedCount { get; set; }
        public int UnmatchedLineCount { get; set; }
    }

    public class MeitarMucarimEntitlementMatchListItemDto
    {
        public int Id { get; set; }
        public int MeitarMucarimId { get; set; }
        public int EntitlementId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? TopicCode { get; set; }
        public string? TopicDescription { get; set; }
        public string? InstitutionCode { get; set; }
        public string? InstitutionName { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal? CalculatedDifference { get; set; }
        public decimal? UnitCount { get; set; }
        public int AssistantTypeId { get; set; }
        public string? AssistantTypeName { get; set; }
        public string? EntitlementInstitutionName { get; set; }
        public string? ClassName { get; set; }
        public decimal Hours { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }

    public class MeitarSharatimListItemDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public DateOnly CalcDate { get; set; }
        public DateOnly EffectiveDate { get; set; }
        public string? InstitutionCode { get; set; }
        public string? InstitutionName { get; set; }
        public string? TopicCode { get; set; }
        public int ClassCount { get; set; }
    }
}
