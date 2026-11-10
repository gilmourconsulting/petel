using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_cheshbonit")]
    public class MeitarCheshbonit : IEntityScoped
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

        [Column("accounting_unit")]
        [MaxLength(100)]
        public string? AccountingUnit { get; set; }

        [Column("topic_code")]
        [MaxLength(50)]
        public string? TopicCode { get; set; }

        [Column("topic_description")]
        [MaxLength(500)]
        public string? TopicDescription { get; set; }

        [Column("total_due")]
        public decimal? TotalDue { get; set; }

        [Column("paid_amount")]
        public decimal? PaidAmount { get; set; }

        [Column("payment_difference")]
        public decimal? PaymentDifference { get; set; }

        [Column("debt_balance")]
        public decimal? DebtBalance { get; set; }

        [Column("monthly_execution_balance")]
        public decimal? MonthlyExecutionBalance { get; set; }

        [Column("notes")]
        [MaxLength(1000)]
        public string? Notes { get; set; }

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
