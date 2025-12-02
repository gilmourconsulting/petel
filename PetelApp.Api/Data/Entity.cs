using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("entities")]
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

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("principal_name")]
        public string? PrincipalName { get; set; }

        [Column("api_connection_id")]
        public string? ApiConnectionId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("entity_logo")]
        public byte[]? EntityLogo { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("owner")]
        public int? OwnerId { get; set; }

        [Column("council")]
        public int? CouncilId { get; set; }

        [Column("inspector_name")]
        public string? InspectorName { get; set; }

        [Column("characterization")]
        public string? Characterization { get; set; }

        [Column("contact_person")]
        public int? ContactPersonId { get; set; }  

        [ForeignKey("ContactPersonId")]
        public Person? ContactPerson { get; set; }  // Navigation property

        [Column("education_stage")]
        public string? EducationStage { get; set; }

        [Column("symbol")]
        public string? Symbol { get; set; }

        [Column("characterization_id")]
        public int? CharacterizationId { get; set; }

        [Column("tax_number")]
        public string? TaxNumber { get; set; }

        [Column("street")]
        public string? Street { get; set; }

        [Column("house_number")]
        public string? HouseNumber { get; set; }

        [Column("city")]
        public string? City { get; set; }

        [Column("post_code")]
        public string? PostCode { get; set; }

        // Navigation properties
        public EntityType? EntityType { get; set; }

        [ForeignKey("OwnerId")]
        public Entity? Owner { get; set; }

        public ICollection<Entity> OwnedEntities { get; set; } = new List<Entity>();

        [ForeignKey("CharacterizationId")]
        public SpecialNeedsCharacterization? SpecialNeedsCharacterization { get; set; }

        [ForeignKey("CouncilId")]
        public Council? Council { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}