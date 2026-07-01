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
        [Column("hebrew_year")]
        [MaxLength(20)]
        public string YearName { get; set; } = string.Empty;
    }
}
