using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("institution_year_settings")]
    public class InstitutionYearSettings : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("institution_id")]
        public int InstitutionId { get; set; }

        [Required]
        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Column("school_days_per_week")]
        public decimal SchoolDaysPerWeek { get; set; }

        [Column("weekly_hours")]
        public decimal WeeklyHours { get; set; }

        [Column("has_extended_school_day")]
        public bool HasExtendedSchoolDay { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        [ForeignKey(nameof(InstitutionId))]
        public virtual Institution? Institution { get; set; }
    }
}
