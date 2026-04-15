using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Petel.BlazorCore.Services
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

        public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
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
            catch (JSDisconnectedException)
            {
                // Circuit disconnected - component is being disposed
                // This is normal during navigation or page close
                _logger.LogDebug("Circuit disconnected during token retrieval");
                return null;
            }
            catch (TaskCanceledException)
            {
                // Operation cancelled - respect cancellation
                _logger.LogDebug("Token retrieval cancelled");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve token from storage");
            }

            return null;
        }

        public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            try
            {
                await _sessionStorage.SetAsync("authToken", token);
                _cachedToken = token;
                _logger.LogInformation("Token stored successfully");
            }
            catch (JSDisconnectedException)
            {
                _logger.LogDebug("Circuit disconnected during token storage");
                // Still cache in memory even if storage fails
                _cachedToken = token;
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Token storage cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store token");
                throw;
            }
        }

        public async Task ClearTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _sessionStorage.DeleteAsync("authToken");
                _cachedToken = null;
                _logger.LogInformation("Token cleared");
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected - clear cache anyway
                _logger.LogDebug("Circuit disconnected during token clear");
                _cachedToken = null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Token clear cancelled");
                _cachedToken = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear token");
                // Clear cache even if storage clear fails
                _cachedToken = null;
            }
        }

        public bool HasCachedToken()
        {
            return !string.IsNullOrEmpty(_cachedToken);
        }
    }
}
