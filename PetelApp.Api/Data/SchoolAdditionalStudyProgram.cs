using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("school_additional_study_programs")]
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
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("weekly_hours")]
        public int WeeklyHours { get; set; }

        [Required]
        [Column("number_of_class_students")]
        public int NumberOfStudents { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        [ForeignKey("SchoolYearId")]
        public virtual SchoolYear? SchoolYear { get; set; }

        [ForeignKey("ClassId")]
        public virtual SchoolClass? SchoolClass { get; set; }
    }
}