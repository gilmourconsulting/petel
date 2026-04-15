using PetelATH.Api.Data;
using PetelATH.Api.DTOs;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Authentication service interface following Authentication & Session Management pattern
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Login user and create session following Frontend Token-Only Storage pattern
        /// Returns token that frontend stores in sessionStorage
        /// </summary>
        Task<LoginResponseDto> LoginAsync(LoginRequestDto loginRequest);
        
        /// <summary>
        /// Verify user password following Security Patterns
        /// </summary>
        Task<bool> VerifyPasswordAsync(User user, string password);
        
        /// <summary>
        /// Hash password for storage following Security Patterns
        /// </summary>
        Task<string> HashPasswordAsync(string password);
        
        /// <summary>
        /// Validate user credentials following Authentication & Session Management
        /// </summary>
        Task<User?> ValidateUserAsync(string username, string password, int entityId);
        Task<User?> ValidateUserAsync(int userId);
        
        Task<string> CompleteLoginAsync(User user, Entity userEntity);

        /// <summary>
        /// Update user password and reset expiration flags
        /// </summary>
        Task UpdateUserPasswordAsync(User user, string newPasswordHash);

        
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public User User { get; set; } = null!;
        public Entity Entity { get; set; } = null!;
        public string SessionId { get; set; } = string.Empty;
    }


}