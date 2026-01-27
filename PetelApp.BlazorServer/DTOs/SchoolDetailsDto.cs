namespace PetelApp.BlazorServer.DTOs
{
    public class SchoolDetailsDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public int? CouncilId { get; set; }
        public string? CouncilName { get; set; }
        public int? CharacterizationId { get; set; }
        public string? CharacterizationName { get; set; }
        public string? Sector { get; set; }
        public string? Status { get; set; }
        public string? EducationStage { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        
        // Address
        public int? AddressId { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        
        // People references
        public int? PrincipalId { get; set; }
        public string? PrincipalName { get; set; }
        public int? InspectorId { get; set; }
        public string? InspectorName { get; set; }
        public int? ContactPersonId { get; set; }
        public string? ContactPersonName { get; set; }
    }

    public class SchoolDetailsResponse
    {
        public bool Success { get; set; }
        public SchoolDetailsDto? Data { get; set; }
        public string? Message { get; set; }
    }

    public class SaveSchoolDetailsRequest
    {
        public string SchoolName { get; set; } = string.Empty;
        public string? SchoolSymbol { get; set; }
        public int? CouncilId { get; set; }
        public int? CharacterizationId { get; set; }
        public string? Sector { get; set; }
        public string? Status { get; set; }
        public string? EducationStage { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int? AddressId { get; set; }
        public int? PrincipalId { get; set; }
        public int? InspectorId { get; set; }
        public int? ContactPersonId { get; set; }
    }

    public class CharacterizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class CouncilDto
    {
        public int Id { get; set; }
        public string CouncilName { get; set; } = string.Empty;
    }

    public class SystemAttributesResponse<T>
    {
        public bool Success { get; set; }
        public List<T> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}
