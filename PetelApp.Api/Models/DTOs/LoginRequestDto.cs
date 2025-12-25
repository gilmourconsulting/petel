using PetelApp.Api.Data;

namespace PetelApp.Api.DTOs
{
    /// <summary>
    /// Login request DTO following Authentication & Session Management pattern
    /// </summary>
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }

    /// <summary>
    /// Login response DTO following Frontend Token-Only Storage pattern
    /// Returns token that frontend stores in sessionStorage
    /// </summary>
    public class LoginResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public EntityDto? Entity { get; set; }
        public bool RequiresOtp { get; set; }
        public bool RequiresOtpSetup { get; set; }  // ✅ NEW
        public string? TempToken { get; set; }
    }
    
    /// <summary>
    /// User information DTO
    /// </summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime? LastLogin { get; set; }
    }
    
    /// <summary>
    /// Entity information DTO
    /// </summary>
    public class EntityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int EntityTypeId { get; set; }
        public string EntityTypeName { get; set; } = string.Empty;
    }
}