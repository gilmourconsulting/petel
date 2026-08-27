namespace PetelAssistants.Api.DTOs.Meitar
{
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
    }
}
