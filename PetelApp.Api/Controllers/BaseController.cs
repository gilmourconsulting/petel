// PetelApp.Api/Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Base controller providing session access for authenticated endpoints
    /// Uses token-based session authentication (token in Authorization header)
    /// </summary>
    public class BaseController : ControllerBase
    {
        protected readonly UserSessionService _userSessionService;
        protected readonly ILogger _logger;

        // Constructor to allow controllers to pass their ILogger<T> and the shared UserSessionService
        public BaseController(UserSessionService userSessionService, ILogger logger)
        {
            //_userSession_service_check:
            _userSessionService = userSessionService;
            _logger = logger;
        }

        // Helper to get current UserSession from Authorization header (Bearer token)
        protected UserSession? GetCurrentSession()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader?.Replace("Bearer ", "").Trim();
            if (string.IsNullOrEmpty(token)) return null;
            return _userSessionService.GetUserSession(token!);
        }

        /// <summary>
        /// Get current user ID from session
        /// </summary>
        protected string? GetCurrentUserId() => GetCurrentSession()?.UserId;
        /// <summary>
        /// Get current entity ID from session
        /// </summary>
        protected string? GetCurrentEntityId() => GetCurrentSession()?.EntityId;
        /// <summary>
        /// Check if current request has valid session
        /// </summary>
        protected bool IsAuthenticated() => GetCurrentSession() != null;
    }
}
