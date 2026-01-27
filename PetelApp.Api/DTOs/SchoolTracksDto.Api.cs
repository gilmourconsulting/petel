namespace PetelApp.Api.DTOs
{
    public class SchoolTrackDto
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public string Track { get; set; } = string.Empty;
        public int? TrackLevelId { get; set; }
        public string TrackLevel { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public decimal WeeklyHours { get; set; }
    }

    public class CreateSchoolTrackDto
    {
        public int SchoolYearId { get; set; }
        public int TrackId { get; set; }
        public int? TrackLevelId { get; set; }
        public int ClassId { get; set; }
        public decimal WeeklyHours { get; set; }
    }

    public class UpdateSchoolTrackDto
    {
        public int Id { get; set; }
        public int? TrackLevelId { get; set; }
        public int ClassId { get; set; }
        public decimal WeeklyHours { get; set; }
    }
}