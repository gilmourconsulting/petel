using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelAssistants.Api.Tenancy;

namespace PetelAssistants.Api.Models
{
    public static class InstitutionTypes
    {
        public const string School = "school";
        public const string Kindergarten = "kindergarten";
    }

    public static class SchoolLevels
    {
        public const string Elementary = "elementary";
        public const string HighSchool = "high_school";

        /// <summary>Ministry שלב חינוך phrase for elementary (Excel / UI).</summary>
        public const string ElementaryDisplay = "יסודי בלבד";

        /// <summary>Ministry שלב חינוך phrase for high school / חט״ב+עליונה (Excel / UI).</summary>
        public const string HighSchoolDisplay = "חט\"ב + עליונה";

        /// <summary>Ministry שלב חינוך phrase for kindergartens (Excel / UI).</summary>
        public const string KindergartenDisplay = "גן ילדים בלבד";

        public static string GetDisplayName(string? schoolLevel, string? institutionType = null)
        {
            if (string.Equals(institutionType, InstitutionTypes.Kindergarten, StringComparison.OrdinalIgnoreCase))
                return KindergartenDisplay;

            if (string.Equals(schoolLevel, Elementary, StringComparison.OrdinalIgnoreCase))
                return ElementaryDisplay;
            if (string.Equals(schoolLevel, HighSchool, StringComparison.OrdinalIgnoreCase))
                return HighSchoolDisplay;

            return "—";
        }
    }

    [Table("institutions")]
    public class Institution : IEntityScoped
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Israeli educational institution code (סמל מוסד).</summary>
        [Column("symbol")]
        [MaxLength(20)]
        public string? Symbol { get; set; }

        [Required]
        [Column("institution_type")]
        [MaxLength(20)]
        public string InstitutionType { get; set; } = string.Empty;

        [Column("school_level")]
        [MaxLength(20)]
        public string? SchoolLevel { get; set; }

        [Column("is_special_education")]
        public bool IsSpecialEducation { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }
    }
}
