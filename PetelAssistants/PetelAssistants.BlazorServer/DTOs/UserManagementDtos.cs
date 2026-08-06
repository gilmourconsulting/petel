namespace PetelAssistants.BlazorServer.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime PasswordChangedAt { get; set; }
        public bool PasswordChangeRequired { get; set; }
        public int EntityId { get; set; }
        public int FailedPasswordAttempts { get; set; }
        public int FailedOtpAttempts { get; set; }
        public bool OtpVerified { get; set; }
        public int? LockReasonId { get; set; }
        public string? LockReasonCode { get; set; }
        public string? LockReasonName { get; set; }
        public bool? LockReasonAllowForgotPassword { get; set; }
    }

    public class LockReasonDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AllowForgotPassword { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username              { get; set; } = string.Empty;
        public string Password              { get; set; } = string.Empty;
        public string? FirstName            { get; set; }
        public string? LastName             { get; set; }
        public string? Email                { get; set; }
        public string? Phone                { get; set; }
        public bool IsActive                { get; set; } = true;
        public bool PasswordChangeRequired  { get; set; } = true;
    }

    public class UpdateUserRequest
    {
        public string? FirstName            { get; set; }
        public string? LastName             { get; set; }
        public string? Email                { get; set; }
        public string? Phone                { get; set; }
        public bool IsActive                { get; set; }
        public bool PasswordChangeRequired  { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class LockUserRequest
    {
        public int? LockReasonId { get; set; }
    }

    public class UserSummaryDto
    {
        public int TotalUsers    { get; set; }
        public int ActiveUsers   { get; set; }
        public int InactiveUsers { get; set; }
        public int LockedUsers   { get; set; }
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EntityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int UserCount { get; set; }
        public int ActionCount { get; set; }
    }

    public class CreateRoleRequest
    {
        public string Name         { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Name         { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class RoleDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EntityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RoleUserDto> Users { get; set; } = new();
        public List<RoleActionDto> Actions { get; set; } = new();
    }

    public class RoleUserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class RoleActionDto
    {
        public int Id { get; set; }
        public int ActionId { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public string ActionDisplayName { get; set; } = string.Empty;
        public string? ActionReference { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
    }

    public class AvailableActionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Description { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    public class AvailableUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class AssignRoleRequest
    {
        public int UserId { get; set; }
    }

    public class AssignActionRequest
    {
        public int ActionId { get; set; }
    }

    public class RolesResponse
    {
        public bool Success { get; set; }
        public List<RoleDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    public class RoleDetailsData
    {
        public RoleSummaryDto Role { get; set; } = new();
        public List<RoleDetailsUserDto> Users { get; set; } = new();
        public List<RoleDetailsActionDto> Actions { get; set; } = new();
    }

    public class RoleSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RoleDetailsUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class RoleDetailsActionDto
    {
        public int Id { get; set; }
        public int ActionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
    }

    public class PickListItemDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string ActionTypeName { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    public class TenantDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? EntityTypeId { get; set; }
        public string? EntityTypeName { get; set; }
        public int UserCount { get; set; }
    }

    public class EntityTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateTenantRequest
    {
        public string Name         { get; set; } = string.Empty;
        public int?   EntityTypeId { get; set; }
    }

    public class UpdateTenantRequest
    {
        public string Name         { get; set; } = string.Empty;
        public int?   EntityTypeId { get; set; }
    }
}
