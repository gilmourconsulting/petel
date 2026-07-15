using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("ministry_participation_options")]
    public class MinistryParticipationOption
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("percentage")]
        public decimal Percentage { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
