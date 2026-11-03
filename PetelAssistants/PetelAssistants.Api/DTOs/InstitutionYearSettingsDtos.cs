using System.ComponentModel.DataAnnotations;

namespace PetelAssistants.Api.DTOs
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
        [Range(1, int.MaxValue)]
        public int InstitutionId { get; set; }

        [Range(0, 7)]
        public decimal SchoolDaysPerWeek { get; set; }

        [Range(0, double.MaxValue)]
        public decimal WeeklyHours { get; set; }

        public bool HasExtendedSchoolDay { get; set; }
    }

    public class UpsertInstitutionYearSettingsRequest
    {
        [Range(1, int.MaxValue)]
        public int YearId { get; set; }

        [Required]
        public List<InstitutionYearSettingsItemRequest> Items { get; set; } = new();
    }
}
