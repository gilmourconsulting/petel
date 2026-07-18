using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("salary_upload_warnings")]
    public class SalaryUploadWarning : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("process_id")]
        public int ProcessId { get; set; }

        [Required]
        [Column("salary_id")]
        public int SalaryId { get; set; }

        [Required]
        [Column("warning_type")]
        [MaxLength(50)]
        public string WarningType { get; set; } = string.Empty;

        [Required]
        [Column("message")]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        public virtual SalaryUploadProcess? Process { get; set; }
        public virtual Salary? Salary { get; set; }
    }
}
