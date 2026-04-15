using AspNetCoreRateLimit;
using PetelATH.Api.Services;
using Microsoft.Extensions.Options;

namespace PetelATH.Api.Configuration
{
    /// <summary>
    /// Dynamic rate limiting configuration that loads from database
    /// Updates rate limits based on database configuration without requiring restart
    /// </summary>
    public static class DatabaseRateLimitConfiguration
    {
        /// <summary>
        /// Add database-driven rate limiting with dynamic configuration
        /// </summary>
        public static IServiceCollection AddDatabaseRateLimiting(this IServiceCollection services)
        {
            // Add memory cache for rate limit counters
            services.AddMemoryCache();

            // Register database configuration service
            services.AddScoped<DatabaseConfigurationService>();

            // Add rate limiting with initial config (will be overridden from database)
            services.Configure<IpRateLimitOptions>(options =>
            {
                options.EnableEndpointRateLimiting = true;
                options.StackBlockedRequests = false;
                options.RealIpHeader = "X-Forwarded-For";
                options.ClientIdHeader = "X-ClientId";
                options.HttpStatusCode = 429;
                options.QuotaExceededMessage = "מכסת הבקשות הושגה. אנא נסה שוב מאוחר יותר";
                options.GeneralRules = new List<RateLimitRule>();
            });

            services.Configure<IpRateLimitPolicies>(options =>
            {
                options.IpRules = new List<IpRateLimitPolicy>();
            });

                        // Register rate limit configuration (required by AspNetCoreRateLimit library)
            services.AddSingleton<IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();

            // Add rate limit stores
            services.AddInMemoryRateLimiting();

            // Register IP rate limit configuration resolver
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            // Custom configuration service that loads from database
            services.AddSingleton<DatabaseRateLimitConfigurationService>();

            return services;
        }
    }

    /// <summary>
    /// Rate limit configuration service that loads rules from database
    /// </summary>
    public class DatabaseRateLimitConfigurationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseRateLimitConfigurationService> _logger;
        private PetelATH.Api.Services.RateLimitConfig? _cachedConfig;
        private DateTime _lastLoad = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(2);

        public DatabaseRateLimitConfigurationService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseRateLimitConfigurationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<IpRateLimitOptions> GetIpRateLimitOptionsAsync()
        {
            var config = await GetRateLimitConfigAsync();

            var options = new IpRateLimitOptions
            {
                EnableEndpointRateLimiting = true,
                StackBlockedRequests = false,
                RealIpHeader = "X-Forwarded-For",
                ClientIdHeader = "X-ClientId", 
                HttpStatusCode = 429,
                QuotaExceededMessage = "מכסת הבקשות הושגה. אנא נסה שוב מאוחר יותר",
                IpWhitelist = new List<string> { "127.0.0.1", "::1" }, // Always allow localhost
                GeneralRules = new List<RateLimitRule>()
            };

            if (config.Enabled)
            {
                // Login endpoint limits
                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "POST:/api/auth/login",
                    Period = config.LoginPeriod,
                    Limit = config.LoginLimit
                });

                // OTP validation limits
                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "POST:/api/otp/validate",
                    Period = config.OtpPeriod,
                    Limit = config.OtpLimit
                });

                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "POST:/api/otp/verify-setup",
                    Period = config.OtpPeriod,
                    Limit = config.OtpLimit
                });

                // Method-specific limits
                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "POST:/api/*",
                    Period = config.ApiPeriod,
                    Limit = config.PostLimit
                });

                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "PUT:/api/*",
                    Period = config.ApiPeriod,
                    Limit = config.PutLimit
                });

                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "DELETE:/api/*",
                    Period = config.ApiPeriod,
                    Limit = config.DeleteLimit
                });

                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "GET:/api/*",
                    Period = config.ApiPeriod,
                    Limit = config.GetLimit
                });

                // General hourly limit
                options.GeneralRules.Add(new RateLimitRule
                {
                    Endpoint = "*",
                    Period = "1h",
                    Limit = config.HourlyLimit
                });
            }

            return options;
        }

        public async Task<IpRateLimitPolicies> GetIpRateLimitPoliciesAsync()
        {
            // No custom IP policies for now
            return new IpRateLimitPolicies
            {
                IpRules = new List<IpRateLimitPolicy>()
            };
        }

        /// <summary>
        /// Get current rate limit configuration for external access
        /// </summary>
        public async Task<PetelATH.Api.Services.RateLimitConfig> GetCurrentConfigAsync()
        {
            return await GetRateLimitConfigAsync();
        }

        private async Task<PetelATH.Api.Services.RateLimitConfig> GetRateLimitConfigAsync()
        {
            // Use cached config if still valid
            if (_cachedConfig != null && DateTime.UtcNow - _lastLoad < _cacheExpiry)
            {
                return _cachedConfig;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<DatabaseConfigurationService>();
                
                _cachedConfig = await configService.GetRateLimitConfigAsync();
                _lastLoad = DateTime.UtcNow;

                _logger.LogInformation("Rate limiting configuration loaded from database: Enabled={Enabled}, LoginLimit={LoginLimit}", 
                    _cachedConfig.Enabled, _cachedConfig.LoginLimit);

                return _cachedConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rate limit configuration from database, using defaults");
                
                // Return safe default configuration
                return new PetelATH.Api.Services.RateLimitConfig
                {
                    Enabled = false, // Disable on error to be safe
                    LoginLimit = 10,
                    LoginPeriod = "15m",
                    OtpLimit = 5,
                    OtpPeriod = "15m",
                    ApiLimit = 120,
                    ApiPeriod = "1m",
                    HourlyLimit = 2000,
                    PostLimit = 60,
                    PutLimit = 40,
                    DeleteLimit = 20,
                    GetLimit = 120
                };
            }
        }
    }

    /// <summary>
    /// Middleware to check if rate limiting should be enabled based on database configuration
    /// </summary>
    public class DatabaseRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseRateLimitMiddleware> _logger;

        public DatabaseRateLimitMiddleware(
            RequestDelegate next,
            IServiceProvider serviceProvider,
            ILogger<DatabaseRateLimitMiddleware> logger)
        {
            _next = next;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Check maintenance mode
                using var scope = _serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<DatabaseConfigurationService>();
                
                var maintenanceMode = await configService.GetConfigAsync("System_MaintenanceMode", false);
                if (maintenanceMode)
                {
                    var message = await configService.GetConfigAsync("System_MaintenanceMessage", 
                        "המערכת בתחזוקה. אנא נסו שוב מאוחר יותר.");
                    
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync(message ?? "המערכת בתחזוקה");
                    return;
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in database rate limit middleware");
                await _next(context);
            }
        }
    }
}