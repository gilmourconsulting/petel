using System.Text.Json.Serialization; // ADD THIS if using JSON attributes

namespace PetelApp.Api.Models
{
    /// <summary>
    /// DTO for system attributes following system attributes pattern
    /// Used for API responses and service layer operations
    /// </summary>
    public class SystemAttributeDto
    {
        public int Id { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
        public string AttributeType { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public string AllowedValues { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string ForeignId { get; set; } = string.Empty;
        public string Tenant { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}