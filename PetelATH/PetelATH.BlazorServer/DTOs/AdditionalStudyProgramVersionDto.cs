namespace PetelATH.BlazorServer.DTOs
{
    public class AdditionalStudyProgramVersionDto
    {
        public int Id { get; set; }
        public int? MasterId { get; set; }
        public int Version { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
        public int NumberOfSessions { get; set; }
        public int NumberOfStudents { get; set; }
        public decimal? Cost { get; set; }
        public decimal? HourlyCost { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public int? ApprovalStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UserName { get; set; }
        public bool IsLastVersion { get; set; }
    }

    public class ProgramVersionHistoryResponse
    {
        public bool Success { get; set; }
        public List<AdditionalStudyProgramVersionDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}
