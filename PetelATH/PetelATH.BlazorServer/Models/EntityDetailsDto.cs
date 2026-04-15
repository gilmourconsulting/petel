using System.Text.Json.Serialization;

namespace PetelATH.BlazorServer.Models
{
    public class EntityDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public string? TaxNumber { get; set; }
        public int EntityTypeId { get; set; }
        public string EntityTypeDescription { get; set; } = string.Empty;
        public int? OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public int? ContactPersonId { get; set; }
        public string? ContactPersonName { get; set; }
    }

    public class EntityTypeDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class OwnerOptionDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class OwnerOptionsResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("ownerOptions")]
        public List<OwnerOptionDto> OwnerOptions { get; set; } = new();
        
        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }
    }

    public class EntityUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public int EntityTypeId { get; set; }
        public int? OwnerId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public int? ContactPersonId { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
    }

    public class EntityListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int EntityTypeId { get; set; }
        public string EntityTypeDescription { get; set; } = string.Empty;
        public int? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateEntityResponseDto
    {
        public int EntityId { get; set; }
    }
}
