using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelATH.Api.Services;

namespace PetelATH.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.councils table
    /// Represents Israeli municipalities/councils (רשויות)
    /// </summary>
    [Table("councils")]
    public class Council
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("council_code")]
        public int CouncilCode { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(25)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Long name used in templates and screens (שם מלא)
        /// </summary>
        [Column("long_name")]
        [MaxLength(100)]
        public string? LongName { get; set; }

        /// <summary>
        /// FK to council_types (סוג רשות): עירייה, מועצה מקומית, מועצה אזורית
        /// </summary>
        [ForeignKey("CouncilType")]
        [Column("council_type_id")]
        public int? CouncilTypeId { get; set; }

        /// <summary>
        /// FK to districts (מחוז)
        /// </summary>
        [ForeignKey("District")]
        [Column("district_id")]
        public int? DistrictId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("user_id")]
        public int UserId { get; set; } = 0;

        // Computed property for backward compatibility
        [NotMapped]
        public string ShortName => Name;

        // ✅ Computed property for efficient Hebrew text matching (in-memory)
        [NotMapped]
        public string NormalizedName => GlobalFunctions.PureHebrewText(Name);

        // Navigation properties
        public virtual CouncilType? CouncilType { get; set; }
        public virtual District? District { get; set; }
    }
}
