using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("entitlement_upload_processes")]
    public class EntitlementUploadProcess : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Column("file_name")]
        [MaxLength(255)]
        public string? FileName { get; set; }

        [Column("created_count")]
        public int CreatedCount { get; set; }

        [Column("versioned_count")]
        public int VersionedCount { get; set; }

        [Column("skipped_count")]
        public int SkippedCount { get; set; }

        [Column("error_count")]
        public int ErrorCount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }
    }
}
