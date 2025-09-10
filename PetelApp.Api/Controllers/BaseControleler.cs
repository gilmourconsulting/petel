// PetelApp.Api/Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Base controller with simplified session management (no tenant validation)
    /// </summary>
    public class BaseController : ControllerBase
    {
        // Add UserSessionService for dependency injection
        protected UserSessionService? UserSessionService => 
            HttpContext.RequestServices.GetService<UserSessionService>();

        protected string? GetSessionId()
        {
            // Try to get session ID from Authorization header (Bearer token)
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length);
            }

            // Fallback: try to get from X-Session-ID header
            return Request.Headers["X-Session-ID"].FirstOrDefault();
        }

        protected UserSession? GetCurrentSession()
        {
            var sessionId = GetSessionId();
            if (string.IsNullOrEmpty(sessionId))
                return null;

            var session = UserSessionService?.GetUserSession(sessionId);
            if (session != null)
            {
                // Update last accessed timestamp on each request
                session.LastAccessedAt = DateTime.UtcNow;
            }
            return session;
        }

        protected string? GetCurrentUserId()
        {
            var session = GetCurrentSession();
            return session?.UserId;
        }

        // Tenant validation methods removed
    }
}