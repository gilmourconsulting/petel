using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Implementation of IAuthService for user authentication
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;

        public AuthService(ILogger<AuthService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Verifies a user's password
        /// </summary>
        public async Task<bool> VerifyPasswordAsync(User user, string password)
        {
            if (user == null || string.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                // In a real system, use a proper password hashing library like BCrypt
                // This is a simplified implementation for demo purposes
                string hashedInput = await HashPasswordAsync(password);
                
                // Store passwords should be hashed in the database
                // For now, we're comparing with the stored password directly
                // In production, replace this with proper password verification
                bool passwordMatches = user.PasswordHash == hashedInput || user.PasswordHash == password;
                
                return passwordMatches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password for user {UserId}", user.Id);
                return false;
            }
        }

        /// <summary>
        /// Creates a hash of the provided password
        /// </summary>
        public async Task<string> HashPasswordAsync(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                // In a real system, use a proper password hashing library like BCrypt
                // This is a simplified implementation for demo purposes
                using var sha256 = SHA256.Create();
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                throw;
            }
        }
    }
}