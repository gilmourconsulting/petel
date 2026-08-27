using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    /// <summary>
    /// SHARATIM — special-needs class counts per school per month (TopicCode 107,
    /// kept only when effective_date == calc_date). One row per school per month.
    /// </summary>
    [Table("meitar_sharatim")]
    public class MeitarSharatim : IEntityScoped
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
        [Column("calc_date")]
        public DateOnly CalcDate { get; set; }

        [Required]
        [Column("effective_date")]
        public DateOnly EffectiveDate { get; set; }

        [Column("institution_code")]
        [MaxLength(50)]
        public string? InstitutionCode { get; set; }

        [Column("institution_name")]
        [MaxLength(300)]
        public string? InstitutionName { get; set; }

        [Column("topic_code")]
        [MaxLength(50)]
        public string? TopicCode { get; set; }

        [Required]
        [Column("class_count")]
        public int ClassCount { get; set; }

        /// <summary>Best-effort match of institution_code to institutions.symbol for the tenant.</summary>
        [Column("institution_id")]
        public int? InstitutionId { get; set; }

        /// <summary>
        /// Hebrew year covering effective_date (shared_schema.hebrew_years.Id). Plain column —
        /// no cross-schema FK/navigation, same convention as Entitlement.HebrewYearId.
        /// </summary>
        [Column("hebrew_year_id")]
        public int? HebrewYearId { get; set; }

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
        public virtual Institution? Institution { get; set; }
    }
}
