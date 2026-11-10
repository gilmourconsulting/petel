namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarPeriodTotalDto
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public int? AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public decimal Amount { get; set; }
        public decimal Hours { get; set; }
    }
}
