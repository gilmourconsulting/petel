using System.ComponentModel.DataAnnotations;

namespace PetelATH.Api.DTOs
{
    public class CreateAlertDto
    {
        [Required]
        [Range(1, 2, ErrorMessage = "Alert type must be 1 (system) or 2 (manual)")]
        public int AlertType { get; set; }

        [Required]
        [Range(1, 7, ErrorMessage = "Alert level must be 1 (system), 2 (school), or 3 (schoolchain)")]
        public int AlertLevel { get; set; }

        [Required]
        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public bool IsEvent { get; set; }

        public DateTime? EventDate { get; set; }

                // ✅ Distribution flags triggered by frontend
        public bool DistributeToOwned { get; set; } = false;
        public bool DistributeToSchools { get; set; } = false;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsEvent && !EventDate.HasValue)
            {
                yield return new ValidationResult(
                    "EventDate is required when IsEvent is true",
                    new[] { nameof(EventDate) });
            }
        }
    }
}