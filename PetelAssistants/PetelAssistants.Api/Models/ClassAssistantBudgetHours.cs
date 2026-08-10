using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("class_assistant_budget_hours")]
    public class ClassAssistantBudgetHours
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Required]
        [Column("school_level")]
        [MaxLength(20)]
        public string SchoolLevel { get; set; } = string.Empty;

        [Column("class_classification_id")]
        public int ClassClassificationId { get; set; }

        [Column("hours")]
        public decimal Hours { get; set; }

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

        [ForeignKey(nameof(ClassClassificationId))]
        public ClassClassification? ClassClassification { get; set; }
    }
}
