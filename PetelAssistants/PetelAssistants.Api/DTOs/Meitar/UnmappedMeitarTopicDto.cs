namespace PetelAssistants.Api.DTOs.Meitar
{
    public class UnmappedMeitarTopicDto
    {
        public string? TopicCode { get; set; }
        public string? TopicDescription { get; set; }
        public int RowCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
