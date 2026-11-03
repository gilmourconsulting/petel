namespace PetelAssistants.BlazorServer.DTOs
{
    public class InstitutionYearSettingsDto
    {
        public int Id { get; set; }
        public int InstitutionId { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string? InstitutionSymbol { get; set; }
        public string InstitutionType { get; set; } = string.Empty;
        public int HebrewYearId { get; set; }
        public decimal SchoolDaysPerWeek { get; set; }
        public decimal WeeklyHours { get; set; }
        public bool HasExtendedSchoolDay { get; set; }
    }

    public class InstitutionYearSettingsItemRequest
    {
        public int InstitutionId { get; set; }
        public decimal SchoolDaysPerWeek { get; set; }
        public decimal WeeklyHours { get; set; }
        public bool HasExtendedSchoolDay { get; set; }
    }

    public class UpsertInstitutionYearSettingsRequest
    {
        public int YearId { get; set; }
        public List<InstitutionYearSettingsItemRequest> Items { get; set; } = new();
    }
}
