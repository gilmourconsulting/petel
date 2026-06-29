using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("hebrew_years")]
    public class HebrewYear
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("year_name")]
        [MaxLength(20)]
        public string YearName { get; set; } = string.Empty;

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("is_current")]
        public bool IsCurrent { get; set; }

        [Column("is_previous")]
        public bool IsPrevious { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
