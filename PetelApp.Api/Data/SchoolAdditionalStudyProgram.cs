using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.school_additional_study_programs table
    /// Represents additional study programs (תל"ן - תכניות לימוד נוספות)
    /// Includes version tracking for historical changes
    /// </summary>
    [Table("school_additional_study_programs", Schema = "petel_schema")]
    public class SchoolAdditionalStudyProgram
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("class_id")]
        public int ClassId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("weekly_hours")]
        public int WeeklyHours { get; set; }

        [Required]
        [Column("number_of_class_students")]
        public int NumberOfStudents { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } 

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Version number for tracking historical changes (1 = first version, increments on update)
        /// </summary>
        [Required]
        [Column("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Flag indicating if this is the most recent version of the record
        /// </summary>
        [Required]
        [Column("is_last_version")]
        public bool IsLastVersion { get; set; } = true;

        /// <summary>
        /// Reference to the original (first version) record ID for version history tracking
        /// </summary>
        [Required]
        [Column("master_id")]
        public int MasterId { get; set; }

        /// <summary>
        /// Estimated or budgeted cost for the program
        /// </summary>
        [Column("cost")]
        public decimal? Cost { get; set; }

        /// <summary>
        /// Approved budget amount for the program
        /// </summary>
        [Column("approved_amount")]
        public decimal? ApprovedAmount { get; set; }

        // Navigation properties
        [ForeignKey(nameof(SchoolYearId))]
        public virtual SchoolYear? SchoolYear { get; set; }

        [ForeignKey(nameof(ClassId))]
        public virtual SchoolClass? SchoolClass { get; set; }

        [ForeignKey(nameof(MasterId))]
        public virtual SchoolAdditionalStudyProgram? MasterProgram { get; set; }

        public virtual ICollection<SchoolAdditionalStudyProgram> VersionHistory { get; set; } = new List<SchoolAdditionalStudyProgram>();
    }
}