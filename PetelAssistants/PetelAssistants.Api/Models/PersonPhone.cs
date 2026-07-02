using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("person_phones")]
    public class PersonPhone : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("person_id")]
        public int PersonId { get; set; }

        [Required]
        [Column("phone_type_id")]
        public int PhoneTypeId { get; set; }

        [Column("phone_number_prefix")]
        [MaxLength(7)]
        public string? PhoneNumberPrefix { get; set; }

        [Column("phone_number")]
        [MaxLength(100)]
        public string? PhoneNumber { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Column("start_date")]
        public DateOnly StartDate { get; set; }

        [Column("end_date")]
        public DateOnly? EndDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual Person Person { get; set; } = null!;
    }
}
