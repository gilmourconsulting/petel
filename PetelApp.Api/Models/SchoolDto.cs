namespace PetelApp.Api.Models
{
    public class SchoolDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Symbol { get; set; }
        public string? Address { get; set; }
        public string? PrincipalName { get; set; }
        public string? InspectorName { get; set; }
        public int? CharacterizationId { get; set; }
        public string? CharacterizationName { get; set; }
        public string? ContactPerson { get; set; }
        public string? EducationStage { get; set; }
        public int? OwnerId { get; set; }
        public int EntityTypeId { get; set; }
        public string EntityTypeName { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public int? SchoolYearId { get; set; }
    }
}