namespace PetelApp.Api.DTOs
{

    public class SchoolAdditionalStudyProgramDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public string SchoolYearName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int WeeklyHours { get; set; }
        public int NumberOfStudents { get; set; }
        public int Version { get; set; }
        public bool IsLastVersion { get; set; }
        public decimal? Cost { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public decimal? HourlyCost { get; set; }

        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int? MasterId { get; set; } // Reference to original record
    }
    public class CreateSchoolAdditionalStudyProgramDto
    {
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int WeeklyHours { get; set; }
        public int NumberOfStudents { get; set; }

        public decimal? Cost { get; set; }
        public decimal? ApprovedAmount { get; set; }

        public decimal? HourlyCost { get; set; }
    }

    public class UpdateSchoolAdditionalStudyProgramDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int WeeklyHours { get; set; }
        public int NumberOfStudents { get; set; }
        public decimal? Cost { get; set; }
        public decimal? ApprovedAmount { get; set; }

        public decimal? HourlyCost { get; set; }
    }
}