using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("alerts")]
    public class Alert
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("alert_type")]
        [Required]
        public int AlertType { get; set; }

        [Column("alert_level")]
        [Required]
        public int AlertLevel { get; set; }

        [Column("description")]
        [Required]
        public string Description { get; set; } = string.Empty;

        [Column("status")]
        [Required]
        public int Status { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("is_event")]
        [Required]
        public bool IsEvent { get; set; }

        [Column("event_date")]
        public DateTime? EventDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;
    }
}