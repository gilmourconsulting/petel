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
        
        public string? Phone { get; set; }
        public string? Email { get; set; }
        
        /// <summary>
        /// Principal full name (FirstName LastName)
        /// </summary>
        public string PrincipalName { get; set; } = string.Empty;
        
        /// <summary>
        /// Inspector full name (FirstName LastName)
        /// </summary>
        public string InspectorName { get; set; } = string.Empty;
        
        /// <summary>
        /// Contact person full name (FirstName LastName)
        /// </summary>
        public string ContactPersonName { get; set; } = string.Empty;
        
        public string? Characterization { get; set; }
        public string? EducationStage { get; set; }
        public string? Symbol { get; set; }
        public bool IsActive { get; set; }
        public string? CouncilName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}