using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_mutavim")]
    public class MeitarMutavim : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("period_year")]
        public int PeriodYear { get; set; }

        [Required]
        [Column("period_month")]
        public int PeriodMonth { get; set; }

        [Required]
        [Column("beneficiary_code")]
        [MaxLength(50)]
        public string BeneficiaryCode { get; set; } = string.Empty;

        [Required]
        [Column("calc_date")]
        public DateOnly CalcDate { get; set; }

        [Column("effective_date")]
        public DateOnly? EffectiveDate { get; set; }

        [Column("topic_code")]
        [MaxLength(50)]
        public string? TopicCode { get; set; }

        [Column("topic_description")]
        [MaxLength(500)]
        public string? TopicDescription { get; set; }

        [Column("unit_count")]
        public decimal? UnitCount { get; set; }

        [Column("cost")]
        public decimal? Cost { get; set; }

        [Column("participation_percent")]
        public decimal? ParticipationPercent { get; set; }

        [Required]
        [Column("calculated_amount")]
        public decimal CalculatedAmount { get; set; }

        [Column("previous_calculated_amount")]
        public decimal? PreviousCalculatedAmount { get; set; }

        [Column("calculated_difference")]
        public decimal? CalculatedDifference { get; set; }

        [Required]
        [Column("process_id")]
        public int ProcessId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual MeitarRetrieveProcess? Process { get; set; }
    }
}
