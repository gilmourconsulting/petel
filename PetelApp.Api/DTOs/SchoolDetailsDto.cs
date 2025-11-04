namespace PetelApp.Api.DTOs
{
    /// <summary>
    /// DTO for school details with formatted fields
    /// Used by SchoolController to return school information
    /// </summary>
    public class SchoolDetailsDto
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public int SchoolYearId { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Formatted address: "Street HouseNumber, City [PostCode]"
        /// PostCode omitted if null or all zeros
        /// </summary>
        public string Address { get; set; } = string.Empty;
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        
        public string? Phone { get; set; }
        public string? Email { get; set; }
        
// ✅ Add person IDs
        public int? PrincipalId { get; set; }
        public string PrincipalName { get; set; } = string.Empty;
        
        public int? InspectorId { get; set; }
        public string InspectorName { get; set; } = string.Empty;
        
        public int? ContactPersonId { get; set; }
        public string ContactPersonName { get; set; } = string.Empty;

        public int? CharacterizationId { get; set; }
        public string? CharacterizationName { get; set; }
                public int? CouncilId { get; set; }
        public string? EducationStage { get; set; }
        public string? Symbol { get; set; }
        public bool IsActive { get; set; }
        public string? CouncilName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}