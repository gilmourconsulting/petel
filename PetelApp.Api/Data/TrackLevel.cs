using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("tracks_levels")]
    public class TrackLevel
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_track_id")]
        public int SchoolTrackId { get; set; }

        [Column("level")]
        [MaxLength(15)]
        public string? LevelName { get; set; }

        [Required]
        [Column("min_hours")]
        public int MinHours { get; set; }

        [Column("max_hours")]
        public int? MaxHours { get; set; }

        [Column("available_for_classes")]
        public string[]? AvailableForClasses { get; set; }

        // Navigation properties
        [ForeignKey("SchoolTrackId")]
        public virtual Track? Track { get; set; }

        public virtual ICollection<SchoolTrack>? SchoolTracks { get; set; }
    }
}