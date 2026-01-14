namespace PetelApp.BlazorServer.DTOs
{
    public class SchoolClassDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public TimeOnly? EndHour { get; set; }
        public int StudentCount { get; set; }
    }

    public class SchoolClassesResponse
    {
        public bool Success { get; set; }
        public List<SchoolClassDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    public class CreateSchoolClassRequest
    {
        public int SchoolYearId { get; set; }
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public string? EndHour { get; set; }
    }

    public class UpdateSchoolClassRequest
    {
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public string? EndHour { get; set; }
    }
}
