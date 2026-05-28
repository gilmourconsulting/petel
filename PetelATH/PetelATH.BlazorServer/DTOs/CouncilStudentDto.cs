namespace PetelATH.BlazorServer.DTOs
{
    public class CouncilStudentDto
    {
        public int Id { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        public int? ClassId { get; set; }
        public string? ClassName { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? DisabilityCategory { get; set; }
        public decimal? Cost { get; set; }
        public int? SendingCouncil { get; set; }
        public string? Status { get; set; }
        public int? StatusId { get; set; }
        public decimal? BasicAmount { get; set; }
        public string? CouncilName { get; set; }
        public int SchoolYearId { get; set; }
        public string? SchoolName { get; set; }
    }

    public class CouncilStudentsResponse
    {
        public bool Success { get; set; }
        public int CouncilId { get; set; }
        public int YearId { get; set; }
        public List<CouncilStudentDto>? Data { get; set; }
    }

    public class CouncilEntityIdResponse
    {
        public bool Success { get; set; }
        public int EntityId { get; set; }
    }
}
