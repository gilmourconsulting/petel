namespace PetelApp.Api.DTOs
{
    public class CreateSchoolAdditionalStudyProgramDto
    {
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int WeeklyHours { get; set; }
        public int NumberOfStudents { get; set; }
    }

    public class UpdateSchoolAdditionalStudyProgramDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int WeeklyHours { get; set; }
        public int NumberOfStudents { get; set; }
    }
}