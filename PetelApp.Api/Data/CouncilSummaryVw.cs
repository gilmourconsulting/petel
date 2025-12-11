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

        [Column("council_short_name")]
        [MaxLength(25)]
        public string? CouncilShortName { get; set; }

        [Column("council_long_name")]
        [MaxLength(50)]
        public string? CouncilLongName { get; set; }

        [Column("year_id")]
        public int YearId { get; set; }

        [Column("number_of_students")]
        public long NumberOfStudents { get; set; }

        [Column("total_requested_amount")]
        public decimal TotalRequestedAmount { get; set; }

        // Computed property for display
        [NotMapped]
        public string CouncilName => CouncilShortName ?? CouncilLongName ?? "לא ידוע";
    }
}