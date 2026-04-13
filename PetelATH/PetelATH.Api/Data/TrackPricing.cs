using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    [Table("tracks_pricing")]
    public class TrackPricing
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_track_id")]
        public int SchoolTrackId { get; set; }

        [Column("price")]
        public decimal? Price { get; set; }

        [ForeignKey("CategoryNavigation")]
        [Column("category")]
        public int? Category { get; set; }

        [ForeignKey("TrackLevel")]
        [Column("level_id")]
        public int? LevelId { get; set; }

        // Navigation properties
        [ForeignKey("SchoolTrackId")]
        public virtual SchoolTrack? SchoolTrack { get; set; }
        
        public virtual TrackLevel? TrackLevel { get; set; }
        public virtual SpecialNeedsPricingCategory? CategoryNavigation { get; set; }
    }
}