using System.ComponentModel.DataAnnotations;

namespace PetelAssistants.Api.DTOs
{
    public class BudgetHourValueDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public decimal HourValue { get; set; }
    }

    public class UpsertBudgetHourValueRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int HebrewYearId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal HourValue { get; set; }
    }
}
