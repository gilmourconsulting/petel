// PetelApp.BlazorServer/DTOs/UserDTOs.cs
namespace PetelApp.BlazorServer.DTOs
{
    /// <summary>
    /// User data transfer object for display
    /// </summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public bool PasswordChangeRequired { get; set; }
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for creating new user
    /// </summary>
    public class CreateUserRequest
    {
        public int EntityId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool PasswordChangeRequired { get; set; } = true;
    }

    /// <summary>
    /// Request for updating user
    /// </summary>
    public class UpdateUserRequest
    {
        public int EntityId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool PasswordChangeRequired { get; set; }
    }

    /// <summary>
    /// Request for changing user password
    /// </summary>
    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response wrapper for users API calls
    /// </summary>
    public class UsersResponse
    {
        public bool Success { get; set; }
        public List<UserDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    /// <summary>
    /// Role data transfer object for display
    /// </summary>
    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int UserCount { get; set; }
        public int ActionCount { get; set; }
    }

    /// <summary>
    /// Request for creating new role
    /// </summary>
    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Request for updating role
    /// </summary>
    public class UpdateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Response wrapper for roles API calls
    /// </summary>
    public class RolesResponse
    {
        public bool Success { get; set; }
        public List<RoleDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    /// <summary>
    /// Role details with users and actions
    /// </summary>
    public class RoleDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<UserDto> Users { get; set; } = new();
        public List<RoleActionDto> Actions { get; set; } = new();
    }

    /// <summary>
    /// Role action association
    /// </summary>
    public class RoleActionDto
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public int ActionId { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public string ActionReference { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
        public string ActionTypeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Available action for assignment to roles
    /// </summary>
    public class AvailableActionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionTypeName { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    /// <summary>
    /// Request to assign action to role
    /// </summary>
    public class AssignActionRequest
    {
        public int RoleId { get; set; }
        public int ActionId { get; set; }
    }

    /// <summary>
    /// Request to assign role to user
    /// </summary>
    public class AssignRoleRequest
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }

    /// <summary>
    /// User summary statistics
    /// </summary>
    public class UserSummaryDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
    }

    /// <summary>
    /// Entity data transfer object
    /// </summary>
    public class EntityDto
    {
        public int Id { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string EntityName { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("entity_type_id")]
        public int? EntityTypeId { get; set; }
    }

    /// <summary>
    /// Response wrapper for entities API calls
    /// </summary>
    public class EntitiesResponse
    {
        public bool Success { get; set; }
        public List<EntityDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}