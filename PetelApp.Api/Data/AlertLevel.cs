using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("alert_levels")]
    public class AlertLevel
    {
        [Key]
        [Column("id")]
        public short Id { get; set; }

        [Column("name")]
        [MaxLength(25)]
        public string? Name { get; set; }

        [Column("description")]
        [MaxLength(25)]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }
    }
}