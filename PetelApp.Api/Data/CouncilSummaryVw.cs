// PetelApp.Api/Data/CouncilSummaryVw.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// View model for petel_schema.council_summary_vw
    /// Provides summary of students and costs per council per year
    /// </summary>
    [Table("council_summary_vw")]
    public class CouncilSummaryVw
    {
        [Column("council_id")]
        public int CouncilId { get; set; }

        [Column("council_name")]  // Changed from council_short_name
        [MaxLength(25)]
        public string? CouncilName { get; set; }

        [Column("year_id")]
        public int YearId { get; set; }

        [Column("number_of_students")]
        public long NumberOfStudents { get; set; }

        [Column("total_requested_amount")]
        public decimal TotalRequestedAmount { get; set; }

        [Column("owner_id")]
        public int? OwnerId { get; set; }

        [Column("owner_name")]
        [MaxLength(255)]
        public string? OwnerName { get; set; }
    }
}