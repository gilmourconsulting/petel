using System.ComponentModel.DataAnnotations;

namespace PetelApp.Api.DTOs
{

    public class EntityUpdateDto
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public string ContactPerson { get; set; }
        public string TaxNumber { get; set; }
        public int? OwnerId { get; set; }
        public int EntityTypeId { get; set; }
    }
}