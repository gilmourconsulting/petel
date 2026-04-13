using System.Timers;
using Timer = System.Timers.Timer;

namespace PetelATH.BlazorServer.Services
{
    /// <summary>
    /// Session timeout manager for Blazor
    /// Tracks user idle time and automatically logs out after configured timeout
    /// Based on original session-timeout.js implementation
    /// </summary>
    public class SessionTimeoutService : IDisposable
    {
        private readonly AuthenticationService _authService;
        private readonly ApiService _apiService;
        private readonly ILogger<SessionTimeoutService> _logger;
        
        private Timer? _idleTimer;
        private Timer? _warningTimer;
        private DateTime _lastActivityTime;
        private bool _warningShown;
        private int _idleTimeoutMinutes = 10; // Default: 10 minutes
        private int _warningTimeMinutes = 2;  // Show warning 2 minutes before timeout

        public event Action? OnShowWarning;
        public event Action? OnHideWarning;
        public event Action? OnAutoLogout;

        public bool IsWarningShown => _warningShown;
        public int RemainingMinutes => _warningTimeMinutes;

        public SessionTimeoutService(
            AuthenticationService authService,
            ApiService apiService,
            ILogger<SessionTimeoutService> logger)
        {
            _authService = authService;
            _apiService = apiService;
            _logger = logger;
            _lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initialize timeout manager and load configuration
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Load timeout configuration from backend
                await LoadTimeoutConfigAsync();

                // Start idle timer
                ResetIdleTimer();

                _logger.LogInformation("Session timeout initialized: {TimeoutMinutes} minutes", _idleTimeoutMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing session timeout, using defaults");
                // Use default timeout if config fails to load
                ResetIdleTimer();
            }
        }

        /// <summary>
        /// Load timeout configuration from backend
        /// </summary>
        private async Task LoadTimeoutConfigAsync()
        {
            try
            {
                var config = await _apiService.GetAsync<TimeoutConfigDto>("session/timeout-config");
                if (config != null)
                {
                    _idleTimeoutMinutes = config.TimeoutMinutes;
                    _logger.LogInformation("Loaded timeout config: {TimeoutMinutes} minutes", _idleTimeoutMinutes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load timeout config, using default");
            }
        }

        /// <summary>
        /// Handle user activity - resets idle timer
        /// </summary>
        public void OnUserActivity()
        {
            _lastActivityTime = DateTime.UtcNow;
            ResetIdleTimer();

            // Hide warning if shown
            if (_warningShown)
            {
                HideWarning();
            }
        }

        /// <summary>
        /// Reset idle timer
        /// </summary>
        private void ResetIdleTimer()
        {
            // Dispose existing timers
            _idleTimer?.Dispose();
            _warningTimer?.Dispose();

            // Set warning timer (X minutes before logout)
            var warningDelay = (_idleTimeoutMinutes - _warningTimeMinutes) * 60 * 1000;
            _warningTimer = new Timer(warningDelay);
            _warningTimer.Elapsed += (sender, e) => ShowWarning();
            _warningTimer.AutoReset = false;
            _warningTimer.Start();

            // Set logout timer
            var logoutDelay = _idleTimeoutMinutes * 60 * 1000;
            _idleTimer = new Timer(logoutDelay);
            _idleTimer.Elapsed += async (sender, e) => await PerformAutoLogoutAsync();
            _idleTimer.AutoReset = false;
            _idleTimer.Start();
        }

        /// <summary>
        /// Show timeout warning
        /// </summary>
        private void ShowWarning()
        {
            if (_warningShown) return;

            _warningShown = true;
            _logger.LogWarning("Showing session timeout warning");
            OnShowWarning?.Invoke();
        }

        /// <summary>
        /// Hide timeout warning
        /// </summary>
        private void HideWarning()
        {
            if (!_warningShown) return;

            _warningShown = false;
            _logger.LogInformation("Hiding session timeout warning");
            OnHideWarning?.Invoke();
        }

        /// <summary>
        /// Continue session - user clicked "Continue" on warning
        /// </summary>
        public void ContinueSession()
        {
            _logger.LogInformation("User continued session");
            OnUserActivity();
        }

        /// <summary>
        /// Perform auto logout due to inactivity
        /// </summary>
        private async Task PerformAutoLogoutAsync()
        {
            _logger.LogWarning("Auto logout due to inactivity");
            HideWarning();
            OnAutoLogout?.Invoke();
            await _authService.LogoutAsync();
        }

        /// <summary>
        /// Manual logout - user clicked "Logout Now" on warning
        /// </summary>
        public async Task LogoutNowAsync()
        {
            _logger.LogInformation("User initiated logout from warning");
            HideWarning();
            await _authService.LogoutAsync();
        }

        /// <summary>
        /// Stop timeout manager
        /// </summary>
        public void Stop()
        {
            _idleTimer?.Dispose();
            _warningTimer?.Dispose();
            _idleTimer = null;
            _warningTimer = null;
            _logger.LogInformation("Session timeout manager stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        private class TimeoutConfigDto
        {
            public int TimeoutMinutes { get; set; }
        }
    }
}
