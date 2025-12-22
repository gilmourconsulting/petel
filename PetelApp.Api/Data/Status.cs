using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("statuses")]
    public class Status
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("object")]
        [MaxLength(25)]
        public string? Object { get; set; }

        [Column("name")]
        [MaxLength(25)]
        public string? Name { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("sort_order")]
        public int? SortOrder { get; set; }

        // Navigation property
        public virtual ICollection<SchoolStudent> Students { get; set; } = new List<SchoolStudent>();
    }
}