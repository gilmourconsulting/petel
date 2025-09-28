using System;

namespace PetelApp.Api.Models
{
    /// <summary>
    /// Data transfer object for system attributes following the System Attributes Pattern
    /// Matches the exact database structure
    /// </summary>
    public class SystemAttributeDto
    {
        /// <summary>
        /// Unique identifier for the system attribute
        /// </summary>
        public int Id { get; set; }
        
      
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// The value of the attribute
        /// </summary>
        public string Value { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of the attribute value (string, number, boolean, etc.)
        /// </summary>
        public string ValueType { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of the attribute
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// User who last updated the attribute
        /// </summary>
        public string UpdateUser { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional foreign key reference
        /// </summary>
        public int? ForeignId { get; set; }
        
        /// <summary>
        /// When the attribute was created
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        
        /// <summary>
        /// When the attribute was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}