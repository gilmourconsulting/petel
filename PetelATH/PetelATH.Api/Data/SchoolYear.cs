using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("school_years")]
    public class SchoolYear
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_id")]
        public int SchoolId { get; set; } // This is the EntityId for tenant scoping

        [Required]
        [Column("hebrew_year_name")]
        public string YearName { get; set; } = string.Empty;

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        [Column("is_current")]
        public bool IsCurrent { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Column("status")]
        public int? Status { get; set; }

        [Column("year_id")]
        public int YearId { get; set; }

    }
}