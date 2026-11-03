using System.ComponentModel.DataAnnotations;

namespace PetelAssistants.Api.DTOs
{
    public class HebrewYearMonthStudyDaysDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        /// <summary>Null when no row exists yet for this month.</summary>
        public decimal? StudyDays { get; set; }
    }

    public class HebrewYearMonthStudyDaysItemRequest
    {
        [Range(1900, 2100)]
        public int PeriodYear { get; set; }

        [Range(1, 12)]
        public int PeriodMonth { get; set; }

        /// <summary>Null clears / skips persistence for that month.</summary>
        [Range(0, 31)]
        public decimal? StudyDays { get; set; }
    }

    public class UpsertHebrewYearMonthStudyDaysRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int HebrewYearId { get; set; }

        [Required]
        public List<HebrewYearMonthStudyDaysItemRequest> Items { get; set; } = new();
    }
}
