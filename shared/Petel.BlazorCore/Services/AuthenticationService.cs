using Microsoft.AspNetCore.Components;

namespace Petel.BlazorCore.Services
{
    /// <summary>
    /// Authentication service to check authentication state and redirect to login if needed
    /// Implements security patterns from original vanilla JS implementation
    /// </summary>
    public class AuthenticationService
    {
        private readonly TokenService _tokenService;
        private readonly NavigationManager _navigationManager;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            TokenService tokenService,
            NavigationManager navigationManager,
            ILogger<AuthenticationService> logger)
        {
            _tokenService = tokenService;
            _navigationManager = navigationManager;
            _logger = logger;
        }

        /// <summary>
        /// Check if user is authenticated
        /// Returns true if valid auth token exists, false otherwise
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                var isAuthenticated = !string.IsNullOrEmpty(token);

                _logger.LogDebug("Authentication check: {IsAuthenticated}, TokenLength: {TokenLength}",
                    isAuthenticated,
                    token?.Length ?? 0);

                return isAuthenticated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        /// <summary>
        /// Ensure user is authenticated, redirect to login if not
        /// Returns true if authenticated, false if redirected
        /// </summary>
        public async Task<bool> EnsureAuthenticatedAsync()
        {
            var isAuthenticated = await IsAuthenticatedAsync();

            if (!isAuthenticated)
            {
                _logger.LogWarning("User not authenticated, redirecting to login");
                _navigationManager.NavigateTo("/login", forceLoad: true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Logout user - clear token and redirect to login
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                _logger.LogInformation("User logout initiated");
                await _tokenService.ClearTokenAsync();
                _navigationManager.NavigateTo("/login", forceLoad: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                // Force navigation even if error occurs
                _navigationManager.NavigateTo("/login", forceLoad: true);
            }
        }
    }
}
