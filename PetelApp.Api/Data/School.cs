// PetelApp.Api/Data/School.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// School entity representing school details by year and version
    /// Maps to petel_schema.schools table
    /// </summary>
    [Table("schools")]
    public class School
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("entity_id")]
        public int EntityId { get; set; }

        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Column("version")]
        public int Version { get; set; }

        [Column("entity_type_id")]
        public int EntityTypeId { get; set; }

        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("street")]
        [MaxLength(50)]
        public string? Street { get; set; }

        [Column("house_number")]
        [MaxLength(6)]
        public string? HouseNumber { get; set; }

        [Column("city")]
        [MaxLength(50)]
        public string? City { get; set; }

        [Column("post_code")]
        [MaxLength(10)]
        public string? PostCode { get; set; }

        [Column("council")]
        public int? Council { get; set; }

        [Column("phone")]
        [MaxLength(50)]
        public string? Phone { get; set; }

        [Column("email")]
        [MaxLength(255)]
        public string? Email { get; set; }

        [Column("principal")]
        public int? Principal { get; set; }

        [Column("inspector")]
        public int Inspector { get; set; }

        [Column("contact_person")]
        public int? ContactPerson { get; set; }

        [Column("api_connection_id")]
        [MaxLength(255)]
        public string? ApiConnectionId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("school_logo")]
        public byte[]? SchoolLogo { get; set; }

       /* [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }*/

        [Column("owner")]
        public int? Owner { get; set; }

        [Column("characterization_id")]
        public int? CharacterizationId { get; set; }

        [Column("education_stage")]
        [MaxLength(25)]
        public string? EducationStage { get; set; }

        [Column("symbol", TypeName = "character(8)")]
        public string? Symbol { get; set; }

        [Column("is_last_version")]
        public bool IsLastVersion { get; set; }

        // Navigation properties
        [ForeignKey("EntityId")]
        public virtual Entity? Entity { get; set; }

        [ForeignKey("SchoolYearId")]
        public virtual SchoolYear? SchoolYear { get; set; }

        [ForeignKey("EntityTypeId")]
        public virtual EntityType? EntityType { get; set; }

        [ForeignKey("Principal")]
        public virtual Person? PrincipalPerson { get; set; }

        [ForeignKey("Inspector")]
        public virtual Person? InspectorPerson { get; set; }

        [ForeignKey("ContactPerson")]
        public virtual Person? ContactPersonPerson { get; set; }

        [ForeignKey("Council")]
        public virtual Council? CouncilEntity { get; set; }

        [ForeignKey("CharacterizationId")]
        public virtual SpecialNeedsCharacterization? Characterization { get; set; }
    }
}
