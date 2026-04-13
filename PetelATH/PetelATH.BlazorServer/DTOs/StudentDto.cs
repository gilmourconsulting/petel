namespace PetelATH.BlazorServer.DTOs
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string? IdNumber { get; set; }
        public int MasterStudentId { get; set; }
        public int Version { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? Gender { get; set; }
        public string? ClassName { get; set; }
        public int? ClassId { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Status { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        public int? SendingCouncil { get; set; }
        public string? CouncilName { get; set; }
        public int? DisabilityCategory { get; set; }
        public decimal? Cost { get; set; }
    }

    public class StudentSummaryDto
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int FinishedStudents { get; set; }
        public int CouncilCount { get; set; }
    }

    public class StudentListResponse
    {
        public bool Success { get; set; }
        public List<StudentDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}
