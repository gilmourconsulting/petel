using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelATH.Api.Data
{
    /// <summary>
    /// Stores individual actions that can be secured
    /// Each action represents a specific operation: menu item, button, page action, etc.
    /// 
    /// Examples:
    /// - Menu Item Action: name="menu_students", reference="students"
    /// - Button Action: name="btn_add_student", reference="students_screen"
    /// - API Action: name="api_create_student", reference="students/create"
    /// </summary>
    [Table("actions")]
    public class SystemAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("display_name")]
        [MaxLength(150)]
        public string? DisplayName { get; set; }

        [Column("description")]
        [MaxLength(255)]
        public string? Description { get; set; }

        /// <summary>
        /// Reference to type of action (menu_item, button, page_action, api_endpoint, report)
        /// 

        [Column("onclick_name")]
        [MaxLength(100)]
        public string? OnclickName { get; set; }

        /// </summary>
        [Required]
        [ForeignKey("ActionType")]
        [Column("action_type_id")]
        public int ActionTypeId { get; set; }

        /// <summary>
        /// Reference identifier - for menu items: menu name, for buttons: screen name or button ID
        /// </summary>
        [Column("reference")]
        [MaxLength(200)]
        public string? Reference { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int UserId { get; set; } = 0;

        // Navigation properties
        public virtual ActionType ActionType { get; set; } = null!;
        public virtual ICollection<RolesAction> RolesActions { get; set; } = new List<RolesAction>();
    }
}