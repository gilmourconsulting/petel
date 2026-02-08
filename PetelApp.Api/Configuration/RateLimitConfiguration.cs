using AspNetCoreRateLimit;

namespace PetelApp.Api.Configuration
{
    /// <summary>
    /// Extension methods for configuring rate limiting services
    /// </summary>
    public static class RateLimitConfiguration
    {
        /// <summary>
        /// Configures rate limiting with in-memory storage
        /// </summary>
        public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            // Needed to store rate limit counters and rules
            services.AddMemoryCache();

            // Load configuration from appsettings
            services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

            // Inject counter and rules stores
            services.AddInMemoryRateLimiting();

            // Configuration
            services.AddSingleton<IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();

            return services;
        }
    }
}
