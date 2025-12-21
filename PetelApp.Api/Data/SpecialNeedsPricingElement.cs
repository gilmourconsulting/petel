using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("special_needs_pricing_elements")]
    public class SpecialNeedsPricingElement
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // ✅ Keep existing property name for backward compatibility
        [Required]
        [Column("name")]
        [MaxLength(50)]
        public string ElementName { get; set; } = string.Empty;

        // ✅ Add missing columns from database
        [Required]
        [Column("year_id")]
        public int YearId { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(25)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ✅ NEW: Calculation level field
        [Column("calculation_level")]
        [MaxLength(50)]
        public string? CalculationLevel { get; set; }

        // ✅ NEW: Sort order field
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // ✅ Navigation property to HebrewYear
        public virtual HebrewYear? Year { get; set; }

        [Column("attribute_to_check")]
        [MaxLength(50)]
        public string? AttributeToCheck { get; set; }

        // ✅ Collection navigation to categories
        public virtual ICollection<SpecialNeedsPricingCategory> Categories { get; set; } = new List<SpecialNeedsPricingCategory>();
    }
}