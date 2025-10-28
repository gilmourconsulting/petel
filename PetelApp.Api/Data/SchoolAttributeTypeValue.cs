using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{

[Table("school_attribute_types_values", Schema = "petel_schema")]
public class SchoolAttributeTypeValue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("school_attribute_id")]
    public int SchoolAttributeId { get; set; }

    [Column("value")]
    [MaxLength(50)]
    public string? Value { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("is_valid")]
    public bool IsValid { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; } = 10;

    // Navigation property
    [ForeignKey("SchoolAttributeId")]
    public SchoolAttributeType? SchoolAttributeType { get; set; }
}
}