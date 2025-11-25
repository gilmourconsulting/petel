using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("alert_links")]
    public class AlertLink
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("alert_id")]
        [Required]
        public long AlertId { get; set; }

        [Column("alert_status")]
        [Required]
        public int AlertStatus { get; set; }

        [Column("entity_id")]
        [Required]
        public int EntityId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("is_last_version")]
        [Required]
        public bool IsLastVersion { get; set; }

        // Navigation properties
        [ForeignKey("AlertId")]
        public Alert? Alert { get; set; }

        [ForeignKey("EntityId")]
        public Entity? Entity { get; set; }
    }
}