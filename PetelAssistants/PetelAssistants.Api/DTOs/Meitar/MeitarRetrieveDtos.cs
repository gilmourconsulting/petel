namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarRetrieveRequest
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public bool ReplaceExisting { get; set; }
    }
}
