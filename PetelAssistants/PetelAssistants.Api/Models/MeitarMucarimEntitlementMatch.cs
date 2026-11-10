using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("meitar_mucarim_entitlement_matches")]
    public class MeitarMucarimEntitlementMatch : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("meitar_mucarim_id")]
        public int MeitarMucarimId { get; set; }

        [Required]
        [Column("entitlement_id")]
        public int EntitlementId { get; set; }

        [Required]
        [Column("match_process_id")]
        public int MatchProcessId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("disabled_at")]
        public DateTime? DisabledAt { get; set; }

        [Column("disable_user")]
        public int? DisableUser { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual MeitarMucarim? MeitarMucarim { get; set; }
        public virtual Entitlement? Entitlement { get; set; }
        public virtual MeitarEntitlementMatchProcess? MatchProcess { get; set; }
    }
}
