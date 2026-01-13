using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity representing school year-specific attributes (e.g., required sessions for additional study programs)
    /// </summary>
    [Table("school_year_attributes")]
    public class SchoolYearAttribute
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to hebrew_years table
        /// </summary>
        [Required]
        [ForeignKey("HebrewYear")]
        [Column("year_id")]
        public int YearId { get; set; }

        /// <summary>
        /// Name of the attribute (e.g., "additional_study_sessions_required")
        /// </summary>
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Hebrew description of the attribute (e.g., "מפגשי תל\"ן נדרשים")
        /// </summary>
        [Column("description")]
        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// Value of the attribute (stored as string for flexibility)
        /// </summary>
        [Required]
        [Column("value")]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation property
        public virtual HebrewYear? HebrewYear { get; set; }
    }
}
