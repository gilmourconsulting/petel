using Petel.BlazorCore.Models;

namespace Petel.BlazorCore.Services
{
    /// <summary>
    /// Service for managing user session state across components
    /// Provides centralized access to session data
    /// </summary>
    public class SessionStateService
    {
        private readonly ApiService _apiService;
        private readonly AuthenticationService _authService;
        private readonly ILogger<SessionStateService> _logger;
        private SessionData? _cachedSession;
        private DateTime? _lastFetch;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1);

        public event Action? OnSessionChanged;

        public SessionStateService(
            ApiService apiService,
            AuthenticationService authService,
            ILogger<SessionStateService> logger)
        {
            _apiService = apiService;
            _authService = authService;
            _logger = logger;
        }

        public async Task<SessionData?> GetSessionAsync(bool forceRefresh = false)
        {
            var now = DateTime.UtcNow;

            // Return cached data if valid
            if (!forceRefresh &&
                _cachedSession != null &&
                _lastFetch != null &&
                (now - _lastFetch.Value) < _cacheDuration)
            {
                _logger.LogDebug("Returning cached session data");
                return _cachedSession;
            }

            _logger.LogDebug("Fetching fresh session data from server");

            try
            {
                var session = await _apiService.GetAsync<SessionData>("session");
                
                if (session != null)
                {
                    _cachedSession = session;
                    _lastFetch = now;
                    OnSessionChanged?.Invoke();
                    
                    _logger.LogInformation("Session data cached for user {UserId}", session.UserId);
                }

                return session;
            }
            catch (HttpStatusException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Authentication failed when fetching session - Token invalid or missing, redirecting to login");
                // Token is invalid or missing - redirect to login
                await _authService.LogoutAsync();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch session data");
                return null;
            }
        }

        public void ClearSession()
        {
            _cachedSession = null;
            _lastFetch = null;
            OnSessionChanged?.Invoke();
            _logger.LogInformation("Session cleared");
        }

        public string? GetEntityId() => _cachedSession?.EntityId;
        public string? GetUserId() => _cachedSession?.UserId;
        public string? GetUsername() => _cachedSession?.Username;
        public string? GetEntityName() => _cachedSession?.EntityName;
    }
}
