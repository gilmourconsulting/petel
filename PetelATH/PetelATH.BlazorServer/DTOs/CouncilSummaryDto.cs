namespace PetelATH.BlazorServer.DTOs
{
    public class CouncilSummaryDto
    {
        public int Id { get; set; }
        public string CouncilName { get; set; } = string.Empty;
        public int NumberOfStudents { get; set; }
        public decimal TotalRequested { get; set; }
        public string TotalRequestedFormatted { get; set; } = string.Empty;
    }

    public class CouncilSummaryResponse
    {
        public bool Success { get; set; }
        public int YearId { get; set; }
        public List<CouncilSummaryDto>? Data { get; set; }
    }
}
