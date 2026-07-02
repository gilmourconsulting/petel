using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("persons")]
    public class Person : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("id_number")]
        [MaxLength(100)]
        public string IdNumber { get; set; } = string.Empty;

        [Column("id_type")]
        public int IdType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual ICollection<PersonDetail> Details { get; set; } = new List<PersonDetail>();
        public virtual ICollection<PersonAddress> Addresses { get; set; } = new List<PersonAddress>();
        public virtual ICollection<PersonPhone> Phones { get; set; } = new List<PersonPhone>();
    }
}
