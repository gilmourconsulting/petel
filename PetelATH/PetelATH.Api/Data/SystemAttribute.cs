using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
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
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [Column("value")]
        [MaxLength(100)]  // 
        public string Value { get; set; } = string.Empty;
        
        [Column("value_type")]
        [MaxLength(100)]  //    
        public string ValueType { get; set; } = string.Empty;
        
        [Column("description")]
        [MaxLength(50)]
        public string? Description { get; set; }
        
        [Column("update_user")]
        public int? UpdateUser { get; set; }
        
        [Column("foreign_id")]
        public int? ForeignId { get; set; }
        
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}