using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{

[Table("school_attribute_types_values")]
public class SchoolAttributeTypeValue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("school_attribute_id")]
    public int SchoolAttributeTypeId { get; set; }

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
    [ForeignKey("SchoolAttributeTypeId")]
    public SchoolAttributeType? SchoolAttributeType { get; set; }
}
}