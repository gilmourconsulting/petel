using System.ComponentModel.DataAnnotations;

namespace PetelATH.Api.DTOs
{
    public class UpdateAlertStatusDto
    {
        [Required]
        public long AlertId { get; set; }

        [Required]
        public int EntityId { get; set; }

        [Required]
        public int Status { get; set; }
    }
}