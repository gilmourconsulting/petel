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

        [Column("name")]
        public string ElementName { get; set; } = string.Empty;


    }
}