using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    [Table("salaries")]
    public class Salary : IEntityScoped
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
        [Column("national_id")]
        [MaxLength(500)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [Column("department_id")]
        [MaxLength(50)]
        public string DepartmentId { get; set; } = string.Empty;

        [Column("department_name")]
        [MaxLength(200)]
        public string? DepartmentName { get; set; }

        [Required]
        [Column("position_percentage")]
        public decimal PositionPercentage { get; set; }

        [Required]
        [Column("total_salary")]
        public decimal TotalSalary { get; set; }

        [Column("matched_person_id")]
        public int? MatchedPersonId { get; set; }

        [Column("matched_allocation_id")]
        public int? MatchedAllocationId { get; set; }

        [Column("has_id_warning")]
        public bool HasIdWarning { get; set; }

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

        public virtual SalaryUploadProcess? Process { get; set; }
        public virtual Person? MatchedPerson { get; set; }
        public virtual EntitlementAllocation? MatchedAllocation { get; set; }
    }
}
