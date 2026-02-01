using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelApp.Api.Data;

namespace PetelApp.Api.Models
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [ForeignKey("TransactionAccount")]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Required]
        [ForeignKey("TransactionType")]
        [Column("transaction_type_id")]
        public int TransactionTypeId { get; set; }

        [Required]
        [Column("transaction_date")]
        public DateTime TransactionDate { get; set; } = DateTime.Today;

        [Required]
        [Column("amount", TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column("description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [ForeignKey("RelatedTransaction")]
        [Column("related_transaction_id")]
        public int? RelatedTransactionId { get; set; }

        [ForeignKey("RelatedStudent")]
        [Column("related_student_id")]
        public int? RelatedStudentId { get; set; }

        [ForeignKey("SchoolYear")]
        [Column("school_year_id")]
        public int? SchoolYearId { get; set; }

        [Required]
        [ForeignKey("User")]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // Navigation properties
        public virtual TransactionAccount TransactionAccount { get; set; } = null!;
        public virtual TransactionType TransactionType { get; set; } = null!;
        public virtual Transaction? RelatedTransaction { get; set; }
        public virtual SchoolStudent? RelatedStudent { get; set; }
        public virtual HebrewYear? SchoolYear { get; set; }
        public virtual User User { get; set; } = null!;
        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
        public virtual ICollection<Transaction> RelatedTransactions { get; set; } = new List<Transaction>();
    }
}
