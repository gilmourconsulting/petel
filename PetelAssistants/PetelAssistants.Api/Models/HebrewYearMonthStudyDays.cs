using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("hebrew_year_month_study_days")]
    public class HebrewYearMonthStudyDays
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Column("study_days")]
        public decimal StudyDays { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        [ForeignKey(nameof(HebrewYearId))]
        public HebrewYear? HebrewYear { get; set; }
    }
}
