// PetelApp.Api/Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Session; // Use existing Session namespace

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Base controller with session management following Authentication & Session Management pattern
    /// </summary>
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        protected readonly UserSessionService _userSessionService;
        protected readonly ILogger<BaseController> _logger;

        protected BaseController(UserSessionService userSessionService, ILogger<BaseController> logger)
        {
            _userSessionService = userSessionService;
            _logger = logger;
        }

        /// <summary>
        /// Get current session from auth token following Authentication & Session Management
        /// </summary>
        protected UserSession? GetCurrentSession()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("Missing or invalid authorization header");
                    return null;
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                // Use existing UserSessionService method
                var session = _userSessionService.GetUserSession(token);
                
                if (session == null)
                {
                    _logger.LogWarning("No active session found for token");
                    return null;
                }

                _logger.LogDebug("Session retrieved for user {UserId} in entity {EntityId}", 
                    session.UserId, session.EntityId);
                
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current session");
                return null;
            }
        }

        /// <summary>
        /// Get current entity ID from session following Entity-Based Request Flow
        /// </summary>
        protected string? GetCurrentEntityId()
        {
            var session = GetCurrentSession();
            return session?.EntityId;
        }

        /// <summary>
        /// Get current user ID from session following Entity-Based Request Flow
        /// </summary>
        protected string? GetCurrentUserId()
        {
            var session = GetCurrentSession();
            return session?.UserId;
        }

        /// <summary>
        /// Get session ID from Authorization header (Bearer token)
        /// </summary>
        protected string? GetSessionId()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }
            return null;
        }
    }
}