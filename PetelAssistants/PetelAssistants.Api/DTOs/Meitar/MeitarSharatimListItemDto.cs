namespace PetelAssistants.Api.DTOs.Meitar
{
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
