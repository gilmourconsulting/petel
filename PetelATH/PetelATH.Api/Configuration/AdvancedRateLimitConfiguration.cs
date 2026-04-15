using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;

namespace PetelATH.Api.Configuration
{
    /// <summary>
    /// Advanced rate limiting configuration with client-based rules
    /// Supports different limits for different user types/roles
    /// </summary>
    public static class AdvancedRateLimitConfiguration
    {
        /// <summary>
        /// Configures advanced rate limiting with client-based rules and Redis support
        /// </summary>
        public static IServiceCollection AddAdvancedRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            // Check if rate limiting is enabled
            var rateLimitingEnabled = configuration.GetValue<bool>("Features:RateLimitingEnabled", false);
            if (!rateLimitingEnabled)
            {
                return services;
            }

            // Add memory cache for rate limit storage
            services.AddMemoryCache();

            // Configure IP-based rate limiting
            services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

            // Configure client-based rate limiting for authenticated users
            services.Configure<ClientRateLimitOptions>(configuration.GetSection("ClientRateLimiting"));
            services.Configure<ClientRateLimitPolicies>(configuration.GetSection("ClientRateLimitPolicies"));

            // Use in-memory storage (production should use distributed cache)
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                // TODO: Add Redis support for production - requires Microsoft.Extensions.Caching.StackExchangeRedis package
                // services.AddStackExchangeRedisCache(options =>
                // {
                //     options.Configuration = redisConnectionString;
                // });
                // services.AddDistributedRateLimiting();
                
                // For now, use in-memory storage
                services.AddInMemoryRateLimiting();
            }
            else
            {
                services.AddInMemoryRateLimiting();
            }

            // Add rate limit configuration
            services.AddSingleton<IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();

            // Add custom client ID resolver
            services.AddSingleton<IClientResolveContributor, CustomClientResolveContributor>();

            return services;
        }
    }

    /// <summary>
    /// Custom client resolver that extracts user ID from JWT token
    /// This allows different rate limits per user/role
    /// </summary>
    public class CustomClientResolveContributor : IClientResolveContributor
    {
        private readonly ILogger<CustomClientResolveContributor> _logger;

        public CustomClientResolveContributor(ILogger<CustomClientResolveContributor> logger)
        {
            _logger = logger;
        }

        public Task<string> ResolveClientAsync(HttpContext httpContext)
        {
            try
            {
                // Extract user ID from JWT claims
                var userIdClaim = httpContext.User?.FindFirst("UserId")?.Value;
                var roleClaim = httpContext.User?.FindFirst("Role")?.Value;

                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    // Different client IDs based on role for different rate limits
                    if (!string.IsNullOrEmpty(roleClaim))
                    {
                        return Task.FromResult($"user_{userIdClaim}_role_{roleClaim}");
                    }
                    
                    return Task.FromResult($"user_{userIdClaim}");
                }

                // Fall back to IP-based limiting for unauthenticated users
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return Task.FromResult($"anonymous_{ip}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve client ID for rate limiting");
                return Task.FromResult("unknown");
            }
        }
    }

    /// <summary>
    /// Custom middleware to add rate limiting headers
    /// </summary>
    public class RateLimitHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitHeadersMiddleware> _logger;

        public RateLimitHeadersMiddleware(RequestDelegate next, ILogger<RateLimitHeadersMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            // Add rate limit info to response headers (for debugging)
            if (context.Response.StatusCode == 429)
            {
                context.Response.Headers["Retry-After"] = "60";
                context.Response.Headers["X-RateLimit-Exceeded"] = "true";
                
                _logger.LogWarning("Rate limit exceeded for {ClientIP} {Endpoint}", 
                    context.Connection.RemoteIpAddress, 
                    $"{context.Request.Method}:{context.Request.Path}");
            }
        }
    }
}