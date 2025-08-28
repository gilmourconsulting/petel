namespace PetelApp.Api.Models.DTOs
{
    public class StudentRegistrationSummaryDto
    {
        public string SchoolGrade { get; set; } = string.Empty;
        public string SchoolTrack { get; set; } = string.Empty;
        public int Registered { get; set; }
    }
}