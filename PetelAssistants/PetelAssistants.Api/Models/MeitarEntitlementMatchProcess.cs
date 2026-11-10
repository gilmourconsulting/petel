using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    public static class MeitarEntitlementMatchSources
    {
        public const string Mucarim = "mucarim";
    }

    public static class MeitarEntitlementMatchTriggers
    {
        public const string Retrieve = "retrieve";
        public const string PeriodRecheck = "period_recheck";
        public const string Entitlement = "entitlement";
    }

    [Table("meitar_entitlement_match_processes")]
    public class MeitarEntitlementMatchProcess : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("source")]
        [MaxLength(30)]
        public string Source { get; set; } = MeitarEntitlementMatchSources.Mucarim;

        [Required]
        [Column("match_trigger")]
        [MaxLength(30)]
        public string MatchTrigger { get; set; } = MeitarEntitlementMatchTriggers.PeriodRecheck;

        [Column("period_year")]
        public int? PeriodYear { get; set; }

        [Column("period_month")]
        public int? PeriodMonth { get; set; }

        [Column("entitlement_id")]
        public int? EntitlementId { get; set; }

        [Column("disabled_count")]
        public int DisabledCount { get; set; }

        [Column("matched_count")]
        public int MatchedCount { get; set; }

        [Column("unmatched_line_count")]
        public int UnmatchedLineCount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual Entitlement? Entitlement { get; set; }

        public virtual ICollection<MeitarMucarimEntitlementMatch> Matches { get; set; }
            = new List<MeitarMucarimEntitlementMatch>();
    }
}
