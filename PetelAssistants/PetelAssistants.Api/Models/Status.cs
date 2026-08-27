using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelAssistants.Api.Models
{
    [Table("statuses")]
    public class Status
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("object")]
        [MaxLength(50)]
        public string Object { get; set; } = string.Empty;

        [Required]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    public static class StatusObjects
    {
        public const string SalaryAnomaly = "salary_anomaly";
    }

    public static class SalaryAnomalyStatusCodes
    {
        public const string New = "new";
        public const string Settled = "settled";
        public const string Note = "note";
    }

    public static class SalaryAnomalyReasons
    {
        public const string UnmappedDepartment = "unmapped_department";
        public const string UnmatchedPerson = "unmatched_person";
        public const string NoAllocationForPeriod = "no_allocation_for_period";
        public const string TypeMismatch = "type_mismatch";
        public const string InvalidIdChecksum = "invalid_id_checksum";
    }
}
