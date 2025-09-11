using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;

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
                // Hash the provided password with Base64 encoding
                string hashedPassword = await HashPasswordAsync(password);
                
                // Compare with the stored hash
                return string.Equals(user.PasswordHash, hashedPassword, StringComparison.OrdinalIgnoreCase);
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
                throw new ArgumentNullException(nameof(password));
            }

            // Move CPU-intensive hashing to a background thread
            return await Task.Run(() => HashPasswordBase64(password));
        }

        private string HashPasswordBase64(string password)
        {
            // Use SHA-256 hash algorithm
            using (var sha256 = SHA256.Create())
            {
                // Convert the password string to a byte array
                var bytes = Encoding.UTF8.GetBytes(password);
                
                // Compute the hash
                var hashBytes = sha256.ComputeHash(bytes);
                
                // Convert the hash to a Base64 string
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}