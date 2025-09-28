using PetelApp.Api.Data;
using PetelApp.Api.Models.DTOs;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Authentication service interface following Authentication & Session Management pattern
    /// </summary>
    public interface IAuthService
    {
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

        /// <summary>
        /// Login user and create session
        /// </summary>
        Task<LoginResponseDto> LoginAsync(LoginRequestDto loginRequest);
    }
}