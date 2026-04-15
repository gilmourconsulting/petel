using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelATH.Api.Data;

namespace PetelATH.Api.Models
{
    /// <summary>
    /// Represents a transaction account for managing financial relationships between entities.
    /// Example: School network (owner) holds account for council (related entity) for external student fees.
    /// </summary>
    [Table("transaction_accounts")]
    public class TransactionAccount
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Entity that owns this account (e.g., school network entity type 3 or 5)
        /// </summary>
        [Required]
        [ForeignKey("OwnerEntity")]
        [Column("owner_entity_id")]
        public int OwnerEntityId { get; set; }

        /// <summary>
        /// Entity for whom transactions are held (e.g., council entity type 2)
        /// </summary>
        [Required]
        [ForeignKey("RelatedEntity")]
        [Column("related_entity_id")]
        public int RelatedEntityId { get; set; }

        /// <summary>
        /// Type of account (from transaction_account_types, e.g., "external_students_fees")
        /// </summary>
        [Required]
        [ForeignKey("AccountType")]
        [Column("account_type_id")]
        public int AccountTypeId { get; set; }

        /// <summary>
        /// Descriptive name for the account
        /// </summary>
        [Required]
        [Column("account_name")]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Hebrew description for UI display
        /// </summary>
        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Current account balance (positive = credit, negative = debit)
        /// </summary>
        [Column("balance")]
        public decimal Balance { get; set; } = 0.00m;

        /// <summary>
        /// Whether the account is active
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===================================================================
        // Audit Fields (REQUIRED)
        // ===================================================================

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_user")]
        public int? CreatedUser { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("update_user")]
        public int? UpdateUser { get; set; }

        // ===================================================================
        // Navigation Properties (REQUIRED for proper EF Core functionality)
        // ===================================================================

        /// <summary>
        /// Navigation property to the entity that owns this account
        /// </summary>
        public virtual Entity OwnerEntity { get; set; } = null!;

        /// <summary>
        /// Navigation property to the entity for whom transactions are held
        /// </summary>
        public virtual Entity RelatedEntity { get; set; } = null!;

        /// <summary>
        /// Navigation property to the account type
        /// </summary>
        public virtual TransactionAccountType AccountType { get; set; } = null!;

        /// <summary>
        /// Collection of transactions associated with this account
        /// </summary>
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        /// <summary>
        /// Navigation property to the user who created this record
        /// </summary>
        public virtual User? CreatedByUser { get; set; }

        /// <summary>
        /// Navigation property to the user who last updated this record
        /// </summary>
        public virtual User? UpdatedByUser { get; set; }
    }
}
