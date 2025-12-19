// PetelApp.Api/Data/SpecialNeedsPricingCategory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("special_needs_pricing_categories")]
    public class SpecialNeedsPricingCategory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("pricing_element")]
        public int PricingElement { get; set; }

        [Required]
        [Column("category")]
        [Range(1, 9)]
        public int Category { get; set; }

        [Column("is_lowest_level")]
        public bool? IsLowestLevel { get; set; }

        [Column("price")]
        public decimal? Price { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }



        // Navigation property
        [ForeignKey("PricingElement")]
        public virtual SpecialNeedsPricingElement PricingElementNavigation { get; set; } = null!;
    }
}