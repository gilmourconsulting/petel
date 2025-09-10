using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// System attribute entity for dynamic configuration following system attributes pattern
    /// Maps to petel_schema.system_attributes table with multi-tenant support
    /// </summary>
    [Table("system_attributes", Schema = "petel_schema")]
    public class SystemAttribute
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [MaxLength(255)]
        public string? Name { get; set; }

        [Column("value")]
        public string? Value { get; set; }

        [Column("value_type")]
        [MaxLength(100)]
        public string? ValueType { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("foreign_id")]
        public int? ForeignId { get; set; }

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // [Column("tenant_id")]
        //public string? Tenant { get; set; }

        // Compatibility properties for service layer - NOT mapped to database
        [NotMapped]
        public string AttributeName => Name ?? string.Empty;

        [NotMapped]
        public string AttributeValue => Value ?? string.Empty;

        [NotMapped]
        public string AttributeType => ValueType ?? string.Empty;

        [NotMapped]
        public string DefaultValue { get; set; } = string.Empty;

        [NotMapped]
        public string AllowedValues { get; set; } = string.Empty;

        [NotMapped]
        public string Category { get; set; } = string.Empty;

        [NotMapped]
        public bool IsRequired { get; set; } = false;

        [NotMapped]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public int SortOrder { get; set; } = 0;

        [NotMapped]
        public string CreatedBy { get; set; } = string.Empty;

        [NotMapped]
        public string UpdatedBy => UpdateUser?.ToString() ?? string.Empty;
    }
}