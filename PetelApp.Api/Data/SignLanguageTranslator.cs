using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("sign_language_translators")]
    public class SignLanguageTranslator
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Required]
        [Column("person_id")]
        public int PersonId { get; set; }

        [Required]
        [Column("hours_employed")]
        public decimal HoursEmployed { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int UserId { get; set; }

        // ✅ Navigation properties (REQUIRED)
        [ForeignKey("SchoolYearId")]
        public virtual SchoolYear SchoolYear { get; set; } = null!;

        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; } = null!;
    }
}