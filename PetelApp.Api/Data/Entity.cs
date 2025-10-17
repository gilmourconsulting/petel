using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("entities", Schema = "petel_schema")]
    public class Entity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("entity_type_id")]
        public int EntityTypeId { get; set; }


        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Column("symbol")]
        public string? Symbol { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("principal_name")]
        public string? PrincipalName { get; set; }

        [Column("inspector_name")]
        public string? InspectorName { get; set; }

        [Column("characterization")]
        public string? Characterization { get; set; }

        [Column("contact_person")]
        public string? ContactPerson { get; set; }

        [Column("education_stage")]
        public string? EducationStage { get; set; }
            [Column("owner")]
        public int? OwnerId { get; set; }
        

        // Navigation properties
        public virtual EntityType? EntityType { get; set; }
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}