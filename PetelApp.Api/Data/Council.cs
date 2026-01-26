using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelApp.Api.Services;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Entity model for petel_schema.councils table
    /// Represents Israeli municipalities/councils (רשויות)
    /// </summary>
    [Table("councils")]
    public class Council
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("council_code")]
        public int CouncilCode { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(25)]
        public string Name { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("user_id")]
        public int UserId { get; set; } = 0;

        // Computed property for backward compatibility
        [NotMapped]
        public string ShortName => Name;

        // ✅ Computed property for efficient Hebrew text matching (in-memory)
        [NotMapped]
        public string NormalizedName => GlobalFunctions.PureHebrewText(Name);
    }
}
