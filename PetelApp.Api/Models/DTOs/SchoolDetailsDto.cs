// PetelApp.Api/Models/DTOs/SchoolDetailsDto.cs
namespace PetelApp.Api.Models.DTOs
{
    /// <summary>
    /// DTO for school details with concatenated address and person names
    /// Used by SchoolController to return formatted school information
    /// </summary>
    public class SchoolDetailsDto
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public int SchoolYearId { get; set; }
        public int Version { get; set; }
        public int? EntityTypeId { get; set; }
        public string? Name { get; set; }
        
        /// <summary>
        /// Concatenated address: street house_number, city [post_code]
        /// Post code omitted if null or all zeros
        /// </summary>
        public string? Address { get; set; }
        
        public string? Council { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        
        /// <summary>
        /// Principal full name (first_name + last_name from persons table)
        /// </summary>
        public string? PrincipalName { get; set; }
        
        /// <summary>
        /// Inspector full name (first_name + last_name from persons table)
        /// </summary>
        public string? InspectorName { get; set; }
        
        /// <summary>
        /// Contact person full name (first_name + last_name from persons table)
        /// </summary>
        public string? ContactPersonName { get; set; }
        
        public string? Characterization { get; set; }
        public string? EducationStage { get; set; }
        public string? Symbol { get; set; }
        public string? SchoolLogo { get; set; }
        public bool IsActive { get; set; }
        public bool IsLastVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
