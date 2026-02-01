using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelApp.Api.Data;

namespace PetelApp.Api.Models
{
    [Table("transaction_detail_types")]
    public class TransactionDetailType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("description")]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation properties
        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
    }
}
