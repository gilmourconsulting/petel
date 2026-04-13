using Hangfire.Dashboard;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using PetelATH.Api.Session; // Add this namespace reference

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Authorization filter for Hangfire dashboard access following the Security Patterns
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // In development mode, allow access to Hangfire dashboard
            // In production, implement proper authentication check
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Example: Check for admin role in user session
            // Get the user session from the HttpContext (requires session middleware)
            var sessionId = httpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }

            // Get required services from HttpContext
            var userSessionService = httpContext.RequestServices.GetService<UserSessionService>();
            if (userSessionService == null)
            {
                return false;
            }

            var session = userSessionService.GetUserSession(sessionId);
            if (session == null)
            {
                return false;
            }

            // Check if user has admin role
            var roles = session.Roles;
            return roles != null && roles.Contains(1);
        }
    }
}