using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PetelATH.Api.Data;

namespace PetelATH.Api.Models
{
    /// <summary>
    /// Represents a type of transaction account (e.g., external students fees, grants, etc.)
    /// </summary>
    [Table("transaction_account_types")]
    public class TransactionAccountType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Unique name identifier for the account type (e.g., "external_students_fees")
        /// </summary>
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Hebrew description for UI display (e.g., "אגרות תלמידי חוץ")
        /// </summary>
        [Required]
        [Column("description")]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this account type is active
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Display order for UI lists
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

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
        // Navigation Properties
        // ===================================================================

        /// <summary>
        /// Navigation property to the user who created this record
        /// </summary>
        public virtual User? CreatedByUser { get; set; }

        /// <summary>
        /// Navigation property to the user who last updated this record
        /// </summary>
        public virtual User? UpdatedByUser { get; set; }

        /// <summary>
        /// Navigation property to transaction accounts using this type
        /// </summary>
        public virtual ICollection<TransactionAccount> TransactionAccounts { get; set; } = new List<TransactionAccount>();
    }
}
