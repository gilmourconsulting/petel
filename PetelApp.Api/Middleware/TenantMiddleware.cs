using Microsoft.Extensions.Primitives;

namespace PetelApp.Api.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;

        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var tenantId = GetTenantId(context);
            
            if (!string.IsNullOrEmpty(tenantId))
            {
                context.Items["TenantId"] = tenantId;
                context.Items["TenantContext"] = true;
                
                _logger.LogDebug("Tenant context set: {TenantId}", tenantId);
            }

            await _next(context);
        }

        private string GetTenantId(HttpContext context)
        {
            // Get tenant from X-Tenant-ID header (from your login form)
            if (context.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues tenantHeader))
            {
                return tenantHeader.FirstOrDefault();
            }

            // Get from query parameter
            if (context.Request.Query.TryGetValue("tenant", out StringValues tenantQuery))
            {
                return tenantQuery.FirstOrDefault();
            }

            // Get from JWT token if authenticated
            if (context.User.Identity.IsAuthenticated)
            {
                var tenantClaim = context.User.FindFirst("tenant_id");
                return tenantClaim?.Value;
            }

            return null;
        }
    }
}