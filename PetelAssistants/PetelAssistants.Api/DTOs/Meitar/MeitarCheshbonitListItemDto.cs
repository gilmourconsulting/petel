namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarCheshbonitListItemDto
    {
        public int Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string BeneficiaryCode { get; set; } = string.Empty;
        public DateOnly CalcDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? AccountingUnit { get; set; }
        public string? TopicCode { get; set; }
        public string? TopicDescription { get; set; }
        public decimal? TotalDue { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? PaymentDifference { get; set; }
        public decimal? DebtBalance { get; set; }
        public decimal? MonthlyExecutionBalance { get; set; }
        public string? Notes { get; set; }
    }
}
