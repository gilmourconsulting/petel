using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
[Table("school_attributes")]
public class SchoolAttribute
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("school_year_id")]
    public int SchoolYearId { get; set; }

    [Column("school_attribute_type_id")]
    public int SchoolAttributeTypeId { get; set; }

    [Column("version")]
    public int Version { get; set; } = 0;

    [Column("value")]
    [MaxLength(50)]
    public string? Value { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("user_id")]
    public int UserId { get; set; } = 0;

    [Column("is_last_version")]
    public bool IsLastVersion { get; set; } = true;

    // Navigation properties
    [ForeignKey("SchoolYearId")]
    public SchoolYear? SchoolYear { get; set; }

    [ForeignKey("SchoolAttributeTypeId")]
    public SchoolAttributeType? SchoolAttributeType { get; set; }
}
}   