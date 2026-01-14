namespace PetelApp.BlazorServer.DTOs
{
    public class SchoolTrackDto
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public string Track { get; set; } = string.Empty;
        public int TrackLevelId { get; set; }
        public string TrackLevel { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
    }

    public class SchoolTracksResponse
    {
        public bool Success { get; set; }
        public List<SchoolTrackDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    public class CreateSchoolTrackRequest
    {
        public int SchoolYearId { get; set; }
        public int TrackId { get; set; }
        public int TrackLevelId { get; set; }
        public int ClassId { get; set; }
        public decimal WeeklyHours { get; set; }
    }

    public class UpdateSchoolTrackRequest
    {
        public int TrackId { get; set; }
        public int TrackLevelId { get; set; }
        public int ClassId { get; set; }
        public decimal WeeklyHours { get; set; }
    }
}
