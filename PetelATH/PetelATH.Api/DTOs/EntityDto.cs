using System.ComponentModel.DataAnnotations;

namespace PetelATH.Api.DTOs
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


            // Create entity (basic)
        public class CreateEntityDto
        {
            public string Name { get; set; } = string.Empty;
            public int EntityTypeId { get; set; }
            public int? OwnerId { get; set; }
        }


                // Update entity (basic fields only; adheres to existing structure)
        public class UpdateEntityDto
        {
            public string? Name { get; set; }
            public int? EntityTypeId { get; set; } // must be one of 3,5,6 if provided
            public int? OwnerId { get; set; }
        }
}