using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelApp.Api.Data;

namespace PetelApp.Api.Models
{
    [Table("transaction_details")]
    public class TransactionDetail
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Transaction")]
        [Column("transaction_id")]
        public int TransactionId { get; set; }

        [Required]
        [ForeignKey("DetailType")]
        [Column("detail_type_id")]
        public int DetailTypeId { get; set; }

        [Required]
        [Column("description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("amount", TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation properties
        public virtual Transaction Transaction { get; set; } = null!;
        public virtual TransactionDetailType DetailType { get; set; } = null!;
    }
}
