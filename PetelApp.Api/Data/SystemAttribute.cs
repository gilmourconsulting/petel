using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// System attribute entity for dynamic configuration following system attributes pattern
    /// Maps to petel_schema.system_attributes table with multi-tenant support
    /// </summary>
    [Table("system_attributes")]
    public class SystemAttribute
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        
        [Column("value")]
        public string Value { get; set; } = string.Empty;
        
        [Column("value_type")]
        public string ValueType { get; set; } = string.Empty;
        
        [Column("description")]
        public string? Description { get; set; }
        
        [Column("update_user")]
        public int? UpdateUser { get; set; }
        
        [Column("foreign_id")]
        public int? ForeignId { get; set; }  // Changed from string to int?
        
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}