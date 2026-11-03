namespace PetelAssistants.BlazorServer.DTOs
{
    public class HebrewYearMonthStudyDaysDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal? StudyDays { get; set; }
    }

    public class HebrewYearMonthStudyDaysItemRequest
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal? StudyDays { get; set; }
    }

    public class UpsertHebrewYearMonthStudyDaysRequest
    {
        public int HebrewYearId { get; set; }
        public List<HebrewYearMonthStudyDaysItemRequest> Items { get; set; } = new();
    }
}
