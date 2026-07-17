using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    public static class HoursUnits
    {
        public const string Weekly  = "weekly";
        public const string Monthly = "monthly";
    }

    [Table("entitlements")]
    public class Entitlement : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("hebrew_year_id")]
        public int HebrewYearId { get; set; }

        [Required]
        [Column("assistant_type_id")]
        public int AssistantTypeId { get; set; }

        [Required]
        [Column("start_date")]
        public DateOnly StartDate { get; set; }

        [Required]
        [Column("end_date")]
        public DateOnly EndDate { get; set; }

        [Required]
        [Column("hours")]
        public decimal Hours { get; set; }

        [Required]
        [Column("hours_unit")]
        [MaxLength(10)]
        public string HoursUnit { get; set; } = HoursUnits.Weekly;

        [Required]
        [Column("ministry_participation_pct")]
        public decimal MinistryParticipationPct { get; set; }

        [Column("institution_id")]
        public int? InstitutionId { get; set; }

        [Column("class_name")]
        [MaxLength(100)]
        public string? ClassName { get; set; }

        public virtual Institution? Institution { get; set; }

        // Personal entitlement fields — all three are set together or all null
        // NOTE: PupilIdNumber is stored AES-encrypted; column length is set in AppDbContext, not here.
        [Column("pupil_id_number")]
        public string? PupilIdNumber { get; set; }

        [Column("pupil_first_name")]
        [MaxLength(100)]
        public string? PupilFirstName { get; set; }

        [Column("pupil_last_name")]
        [MaxLength(100)]
        public string? PupilLastName { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }
    }
}
