using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.additional_study_programs_pricing table
    /// Stores maximum allowed pricing per student based on class size and year
    /// </summary>
    [Table("additional_study_programs_pricing")]
    public class AdditionalStudyProgramsPricing
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("year_id")]
        public int YearId { get; set; }

        [Required]
        [Column("students")]
        public int Students { get; set; }

        [Column("price")]
        public decimal? Price { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        // Navigation property
        [ForeignKey(nameof(YearId))]
        public virtual HebrewYear? HebrewYear { get; set; }
    }
}