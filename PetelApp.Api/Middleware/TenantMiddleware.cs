// PetelApp.Api/Middleware/TenantMiddleware.cs
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
                
                _logger.LogInformation("Tenant context set: {TenantId}", tenantId);
            }
            else
            {
                // For login endpoint, tenant ID might come in the request body
                // For other endpoints, we might require tenant context
                var path = context.Request.Path.Value?.ToLower();
                
                if (!IsPublicEndpoint(path))
                {
                    _logger.LogWarning("No tenant context found for protected endpoint: {Path}", path);
                }
            }

            await _next(context);
        }

        private string? GetTenantId(HttpContext context)
        {
            // Priority order for tenant identification:
            
            // 1. X-Tenant-ID header (from your login form)
            if (context.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues tenantHeader))
            {
                var tenantId = tenantHeader.FirstOrDefault();
                if (!string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogDebug("Found tenant ID in X-Tenant-ID header: {TenantId}", tenantId);
                    return tenantId;
                }
            }

            // 2. Query parameter (for URLs like /api/data?tenant=123)
            if (context.Request.Query.TryGetValue("tenant", out StringValues tenantQuery))
            {
                var tenantId = tenantQuery.FirstOrDefault();
                if (!string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogDebug("Found tenant ID in query parameter: {TenantId}", tenantId);
                    return tenantId;
                }
            }

            // 3. JWT token claim (for authenticated requests)
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = context.User.FindFirst("tenant_id");
                if (tenantClaim != null && !string.IsNullOrEmpty(tenantClaim.Value))
                {
                    _logger.LogDebug("Found tenant ID in JWT claim: {TenantId}", tenantClaim.Value);
                    return tenantClaim.Value;
                }
            }

            // 4. Subdomain (if using tenant1.yourapp.com format)
            var host = context.Request.Host.Host;
            if (host.Contains(".") && !host.StartsWith("www.") && !host.StartsWith("localhost"))
            {
                var subdomain = host.Split('.')[0];
                if (int.TryParse(subdomain, out _)) // If subdomain is numeric tenant ID
                {
                    _logger.LogDebug("Found tenant ID in subdomain: {TenantId}", subdomain);
                    return subdomain;
                }
            }

            // Log available headers for debugging
            var headers = context.Request.Headers
                .Where(h => h.Key.StartsWith("X-") || h.Key.ToLower().Contains("tenant"))
                .ToDictionary(h => h.Key, h => h.Value.ToString());
            
            if (headers.Any())
            {
                _logger.LogDebug("Available headers for tenant identification: {Headers}", 
                    string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")));
            }
            else
            {
                _logger.LogDebug("No tenant-related headers found");
            }

            return null;
        }

        private bool IsPublicEndpoint(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var publicPaths = new[]
            {
                "/api/auth/login",
                "/api/entities",
                "/api/test",
                "/api/health",
                "/swagger",
                "/favicon.ico",
                "/_vs/browserlink"  // Visual Studio browser link
            };

            return publicPaths.Any(p => path.StartsWith(p));
        }
    }
}