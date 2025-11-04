namespace PetelApp.Api.DTOs
{
    /// <summary>
    /// DTO for updating school details
    /// Used by SchoolController.UpdateSchoolDetails endpoint
    /// </summary>
    public class UpdateSchoolDetailsDto
    {
        public int SchoolYearId { get; set; }
        public string? Symbol { get; set; }
        public int? CharacterizationId { get; set; }
        public string? EducationStage { get; set; }
        public int? CouncilId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        
        // ✅ Address fields
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        
        // ✅ Person references
        public int? PrincipalId { get; set; }
        public int? InspectorId { get; set; }
        public int? ContactPersonId { get; set; }
    }
}