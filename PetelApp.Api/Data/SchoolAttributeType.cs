using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
[Table("school_attributes_types", Schema = "petel_schema")]
public class SchoolAttributeType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }


    [Column("year_id")]
    public int YearId { get; set; }

    [Column("name")]
    [MaxLength(25)]
    public string Name { get; set; } = string.Empty;

    [Column("hebrew_name")]
    public string? HebrewName { get; set; }

    [Column("attribute_value_type")]
    [MaxLength(25)]
    public string? AttributeValueType { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}}