// PetelATH.Api/Data/SpecialNeedsPricingStep.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("special_needs_pricing_steps")]
    public class SpecialNeedsPricingStep
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("pricing_element")]
        public int PricingElement { get; set; }

        [Required]
        [Column("category")]
        public int Category { get; set; }

        [Required]
        [Column("object_check")]
        [MaxLength(50)]
        public string ObjectCheck { get; set; } = string.Empty;

        [Required]
        [Column("object_element_check")]
        [MaxLength(50)]
        public string ObjectElementCheck { get; set; } = string.Empty;

        [Required]
        [Column("object_element_value")]
        [MaxLength(50)]
        public string ObjectElementValue { get; set; } = string.Empty;

        [Column("price")]
        public decimal? Price { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        // ✅ Navigation property to pricing element
        [ForeignKey("PricingElement")]
        public virtual SpecialNeedsPricingElement? PricingElementNavigation { get; set; }
    }
}