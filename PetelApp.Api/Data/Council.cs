using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.councils table
    /// Represents Israeli municipalities/councils (רשויות)
    /// </summary>
    [Table("councils", Schema = "petel_schema")]
    public class Council
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("council_code")]
        public string CouncilCode { get; set; }

        [Column("council_type")]
        [MaxLength(25)]
        public string? CouncilType { get; set; }

        [Column("council_short_name")]
        [MaxLength(25)]
        public string? CouncilShortName { get; set; }

        [Column("council_long_name")]
        [MaxLength(50)]
        public string? CouncilLongName { get; set; }

        [Column("council_district")]
        [MaxLength(25)]
        public string? CouncilDistrict { get; set; }

        [Column("council_HP_number")]
        public int? CouncilHPNumber { get; set; }

        // Computed property for display - prefers short name, falls back to long name
        [NotMapped]
        public string Name => CouncilShortName ?? CouncilLongName ?? $"Council {CouncilCode}";

        [NotMapped]
        public string ShortName => CouncilShortName ?? CouncilLongName ?? $"Council {CouncilCode}";
    }
}
