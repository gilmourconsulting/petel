using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace PetelApp.BlazorServer.Services
{
    /// <summary>
    /// Service for managing JWT authentication tokens
    /// Stores tokens in protected browser storage (encrypted session storage)
    /// </summary>
    public class TokenService
    {
        private readonly ProtectedSessionStorage _sessionStorage;
        private readonly ILogger<TokenService> _logger;
        private string? _cachedToken;

        public TokenService(
            ProtectedSessionStorage sessionStorage,
            ILogger<TokenService> logger)
        {
            _sessionStorage = sessionStorage;
            _logger = logger;
        }

        public async Task<string?> GetTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken))
            {
                return _cachedToken;
            }

            try
            {
                var result = await _sessionStorage.GetAsync<string>("authToken");
                if (result.Success)
                {
                    _cachedToken = result.Value;
                    return result.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve token from storage");
            }

            return null;
        }

        public async Task SetTokenAsync(string token)
        {
            try
            {
                await _sessionStorage.SetAsync("authToken", token);
                _cachedToken = token;
                _logger.LogInformation("Token stored successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store token");
                throw;
            }
        }

        public async Task ClearTokenAsync()
        {
            try
            {
                await _sessionStorage.DeleteAsync("authToken");
                _cachedToken = null;
                _logger.LogInformation("Token cleared");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear token");
            }
        }

        public bool HasCachedToken()
        {
            return !string.IsNullOrEmpty(_cachedToken);
        }
    }
}
