// PetelApp.Api/Models/AdditionalStudyProgram.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Models
{
    [Table("school_additional_study_programs")]
    public class AdditionalStudyProgram
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("class_id")]
        public int ClassId { get; set; }

        [Column("weekly_hours")]
        public int WeeklyHours { get; set; }

        [Column("number_of_class_students")]
        public int NumberOfClassStudents { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("version")]
        public int Version { get; set; } = 1;

        [Column("is_last_version")]
        public bool IsLastVersion { get; set; } = true;

        [Column("master_id")]
        public int? MasterId { get; set; }

        [Column("cost")]
        public decimal? Cost { get; set; }

        [Column("approved_amount")]
        public decimal? ApprovedAmount { get; set; }

        [Column("hourly_cost")]
        public decimal? HourlyCost { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
