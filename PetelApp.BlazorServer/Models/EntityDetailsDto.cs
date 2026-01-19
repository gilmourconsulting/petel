namespace PetelApp.BlazorServer.Models
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
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OwnerOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OwnerOptionsResponseDto
    {
        public bool Success { get; set; }
        public List<OwnerOptionDto> OwnerOptions { get; set; } = new();
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
