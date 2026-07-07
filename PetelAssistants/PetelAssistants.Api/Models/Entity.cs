using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("entities")]
    public class Entity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("entity_type_id")]
        public int? EntityTypeId { get; set; }

        [Column("parent_entity_id")]
        public int? ParentEntityId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public virtual EntityType? EntityType { get; set; }
        public virtual Entity? ParentEntity { get; set; }
        public virtual ICollection<Entity> ChildEntities { get; set; } = new List<Entity>();
    }
}
