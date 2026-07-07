using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    public static class EntitlementKinds
    {
        public const string Institutional = "institutional";
        public const string Personal = "personal";
    }

    public static class HoursUnits
    {
        public const string Weekly = "weekly";
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
        [Column("entitlement_kind")]
        [MaxLength(20)]
        public string EntitlementKind { get; set; } = string.Empty;

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

        [Column("school_entity_id")]
        public int? SchoolEntityId { get; set; }

        [Column("class_name")]
        [MaxLength(100)]
        public string? ClassName { get; set; }

        [Column("pupil_external_id")]
        [MaxLength(100)]
        public string? PupilExternalId { get; set; }

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
