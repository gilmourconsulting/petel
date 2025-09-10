// PetelApp.Api/Middleware/TenantMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Session;

namespace PetelApp.Api.Middleware
{
    /// <summary>
    /// Middleware disabled - multi-tenant requirement dropped
    /// Original functionality preserved as comments for reference
    /// </summary>
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;

        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserSessionService userSessionService)
        {
            // Original tenant validation removed - just pass through to next middleware
            await _next(context);
        }
    }
}