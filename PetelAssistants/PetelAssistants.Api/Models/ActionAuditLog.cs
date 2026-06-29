using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("action_audit_logs")]
    public class ActionAuditLog : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Required]
        [Column("action_name")]
        [MaxLength(200)]
        public string ActionName { get; set; } = string.Empty;

        [Column("screen_name")]
        [MaxLength(100)]
        public string? ScreenName { get; set; }

        [Column("function_name")]
        [MaxLength(100)]
        public string? FunctionName { get; set; }

        [Required]
        [Column("event_type")]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [Column("result")]
        [MaxLength(20)]
        public string Result { get; set; } = string.Empty;

        [Column("action_params")]
        [MaxLength(500)]
        public string? ActionParams { get; set; }

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("ip_address")]
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
