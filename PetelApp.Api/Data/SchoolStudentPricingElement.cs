using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("school_student_pricing_elements")]
    public class SchoolStudentPricingElement
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("school_student")]
        public int StudentId { get; set; }

        [Column("pricing_element")]
        public int PricingElementId { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("determining_factor")]
        [MaxLength(100)]
        public string? DeterminingFactor { get; set; }

        [Column("hours")]
        public int? Hours { get; set; }
    }
}