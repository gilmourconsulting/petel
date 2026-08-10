using System.ComponentModel.DataAnnotations;

namespace PetelAssistants.Api.DTOs
{
    public class ClassAssistantBudgetHoursDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public string SchoolLevel { get; set; } = string.Empty;
        public int ClassClassificationId { get; set; }
        public string ClassClassificationName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }

    public class UpsertClassAssistantBudgetHoursRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int HebrewYearId { get; set; }

        [Required]
        public List<ClassAssistantBudgetHoursLineRequest> Lines { get; set; } = new();
    }

    public class ClassAssistantBudgetHoursLineRequest
    {
        [Required]
        public string SchoolLevel { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int ClassClassificationId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Hours { get; set; }
    }
}
