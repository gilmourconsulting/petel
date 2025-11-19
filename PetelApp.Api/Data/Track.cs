using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("tracks")]
    public class Track
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string TrackName { get; set; } = string.Empty;

        [Required]
        [Column("year_id")]
        public int YearId { get; set; }

        [Column("external_code")]
        [MaxLength(10)]
        public string? ExternalCode { get; set; }

        [Column("available_for_classes")]
        public string[]? AvailableForClasses { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<SchoolTrack>? SchoolTracks { get; set; }
        public virtual ICollection<TrackLevel>? TrackLevels { get; set; }
    }
}