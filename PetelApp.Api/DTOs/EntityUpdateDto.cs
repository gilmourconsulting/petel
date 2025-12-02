using System.ComponentModel.DataAnnotations;

namespace PetelApp.Api.DTOs
{

    public class EntityUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }

        public int? ContactPersonId { get; set; }   
        public string? TaxNumber { get; set; }
        public int? OwnerId { get; set; }
        public int EntityTypeId { get; set; }
        
        // Address components for formatting
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
    }
}