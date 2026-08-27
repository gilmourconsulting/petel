namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarRetrieveRequest
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public bool ReplaceExisting { get; set; }
    }

    public class MeitarRetrieveRangeRequest
    {
        public int FromYear { get; set; }
        public int FromMonth { get; set; }
        public int ToYear { get; set; }
        public int ToMonth { get; set; }
        public bool ReplaceExisting { get; set; }
    }
}
