namespace PetelApp.Api.DTOs
{
    public class SchoolClassDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public string? ClassNumber { get; set; }
        public TimeOnly? EndHour { get; set; }

    }

    public class SchoolClassUpdateDto
    {
        public int? Id { get; set; }
        public string? Level { get; set; }
        public string? ClassNumber { get; set; }
                public TimeOnly? EndHour { get; set; }


    }

    public class SchoolClassBulkUpdateDto
    {
        public int SchoolYearId { get; set; }
        public List<SchoolClassUpdateDto> Classes { get; set; } = new();
    }

        public class SchoolClassCreateDto
    {
        public int SchoolYearId { get; set; }
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public TimeOnly? EndHour { get; set; }
    }
}