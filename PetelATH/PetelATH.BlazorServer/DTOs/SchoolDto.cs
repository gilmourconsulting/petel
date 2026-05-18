namespace PetelATH.BlazorServer.DTOs
{
    public class SchoolDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? Address { get; set; }
        public string? PrincipalName { get; set; }
        public string? InspectorName { get; set; }
        public string? CharacterizationName { get; set; }
        public string? ContactPerson { get; set; }
        public string? EducationStage { get; set; }
        public string? OwnerName { get; set; }
        public int? SchoolYearId { get; set; }
    }
}
