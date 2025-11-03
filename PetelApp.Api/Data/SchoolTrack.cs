using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("school_tracks", Schema = "petel_schema")]
    public class SchoolTrack
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("track_id")]
        public int TrackId { get; set; }

        [Column("track_level_id")]
        public int? TrackLevelId { get; set; }

        [Required]
        [Column("class_id")]
        public int ClassId { get; set; }

        [Column("weekly_hours")]
        public int? WeeklyHours { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        /*[Column("created_at")]
        public DateTime CreatedAt { get; set; } */

        // Navigation properties
        [ForeignKey("SchoolYearId")]
        public virtual SchoolYear? SchoolYear { get; set; }

        [ForeignKey("TrackId")]
        public virtual Track? Track { get; set; }

        [ForeignKey("TrackLevelId")]
        public virtual TrackLevel? TrackLevel { get; set; }

        [ForeignKey("ClassId")]
        public virtual SchoolClass? SchoolClass { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}