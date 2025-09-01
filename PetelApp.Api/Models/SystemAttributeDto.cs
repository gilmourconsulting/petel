namespace PetelApp.Api.Models
{
    /// <summary>
    /// DTO for system attributes following system attributes pattern
    /// Used for API responses and business logic operations
    /// </summary>
    public class SystemAttributeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int? ForeignId { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}