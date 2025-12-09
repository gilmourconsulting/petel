using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Unified audit log for all authorization attempts
    /// Covers: button clicks, menu navigation, API calls, file uploads, etc.
    /// </summary>
    [Table("action_audit_logs")]
    public class ActionAuditLog
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Action identifier (format: screenname_functionname or custom)
        /// </summary>
        [Required]
        [Column("action_name")]
        [MaxLength(200)]
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// Screen/page name where action was attempted
        /// </summary>
        [Required]
        [Column("screen_name")]
        [MaxLength(100)]
        public string ScreenName { get; set; } = string.Empty;

        /// <summary>
        /// Function name that was called
        /// </summary>
        [Required]
        [Column("function_name")]
        [MaxLength(100)]
        public string FunctionName { get; set; } = string.Empty;

        /// <summary>
        /// Event type: ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL, FILE_UPLOAD, etc.
        /// </summary>
        [Required]
        [Column("event_type")]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Result: GRANTED or DENIED
        /// </summary>
        [Required]
        [Column("result")]
        [MaxLength(20)]
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Action parameters (e.g., "yearId=5, type='current'" for showSchoolYear)
        /// </summary>
        [Column("action_params")]
        [MaxLength(500)]
        public string? ActionParams { get; set; }

        /// <summary>
        /// Optional description providing context
        /// </summary>
        [Column("description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Column("ip_address")]
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
    }
}