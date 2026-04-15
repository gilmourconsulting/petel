using PetelATH.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Service for loading runtime configuration from database system attributes
    /// Provides caching and type-safe access to database-driven configuration
    /// </summary>
    public class DatabaseConfigurationService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DatabaseConfigurationService> _logger;
        private const int CACHE_EXPIRY_MINUTES = 5; // Cache config for 5 minutes
        
        public DatabaseConfigurationService(
            AppDbContext context,
            IMemoryCache cache,
            ILogger<DatabaseConfigurationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Get configuration value with caching and type conversion
        /// </summary>
        public async Task<T?> GetConfigAsync<T>(string key, T? defaultValue = default)
        {
            var cacheKey = $"config_{key}";
            
            if (_cache.TryGetValue(cacheKey, out T? cachedValue))
            {
                return cachedValue;
            }

            try
            {
                var attribute = await _context.SystemAttributes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Name == key);

                if (attribute == null)
                {
                    _logger.LogWarning("Configuration key '{Key}' not found in database, using default value", key);
                    return defaultValue;
                }

                var convertedValue = ConvertValue<T>(attribute.Value, attribute.ValueType, defaultValue);
                
                // Cache for 5 minutes
                _cache.Set(cacheKey, convertedValue, TimeSpan.FromMinutes(CACHE_EXPIRY_MINUTES));
                
                return convertedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration key '{Key}' from database", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Get multiple configuration values at once
        /// </summary>
        public async Task<Dictionary<string, object?>> GetConfigBatchAsync(params string[] keys)
        {
            var result = new Dictionary<string, object?>();
            var uncachedKeys = new List<string>();

            // Check cache first
            foreach (var key in keys)
            {
                var cacheKey = $"config_{key}";
                if (_cache.TryGetValue(cacheKey, out object? cachedValue))
                {
                    result[key] = cachedValue;
                }
                else
                {
                    uncachedKeys.Add(key);
                }
            }

            // Load uncached keys from database
            if (uncachedKeys.Count > 0)
            {
                try
                {
                    var attributes = await _context.SystemAttributes
                        .AsNoTracking()
                        .Where(a => uncachedKeys.Contains(a.Name))
                        .ToListAsync();

                    foreach (var attr in attributes)
                    {
                        var convertedValue = ConvertValue<object>(attr.Value, attr.ValueType);
                        result[attr.Name] = convertedValue;
                        
                        // Cache individual values
                        _cache.Set($"config_{attr.Name}", convertedValue, TimeSpan.FromMinutes(CACHE_EXPIRY_MINUTES));
                    }

                    // Add null for missing keys
                    foreach (var missingKey in uncachedKeys.Except(attributes.Select(a => a.Name)))
                    {
                        result[missingKey] = null;
                        _logger.LogWarning("Configuration key '{Key}' not found in database", missingKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading configuration batch from database");
                    foreach (var key in uncachedKeys)
                    {
                        result[key] = null;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Update configuration value in database and clear cache
        /// </summary>
        public async Task<bool> SetConfigAsync(string key, object value, string? description = null)
        {
            try
            {
                var attribute = await _context.SystemAttributes
                    .FirstOrDefaultAsync(a => a.Name == key);

                var valueType = GetValueType(value);
                var stringValue = value?.ToString() ?? "";

                if (attribute != null)
                {
                    attribute.Value = stringValue;
                    attribute.ValueType = valueType;
                    if (!string.IsNullOrEmpty(description))
                        attribute.Description = description;
                    attribute.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    attribute = new SystemAttribute
                    {
                        Name = key,
                        Value = stringValue,
                        ValueType = valueType,
                        Description = description ?? key,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemAttributes.Add(attribute);
                }

                await _context.SaveChangesAsync();
                
                // Clear cache
                _cache.Remove($"config_{key}");
                _logger.LogInformation("Configuration '{Key}' updated to '{Value}'", key, value);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration key '{Key}' in database", key);
                return false;
            }
        }

        /// <summary>
        /// Clear all configuration cache
        /// </summary>
        public void ClearCache()
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }
            _logger.LogInformation("Configuration cache cleared");
        }

        /// <summary>
        /// Get rate limiting configuration for AspNetCoreRateLimit
        /// </summary>
        public async Task<RateLimitConfig> GetRateLimitConfigAsync()
        {
            var config = await GetConfigBatchAsync(
                "RateLimit_Enabled",
                "RateLimit_LoginLimit",
                "RateLimit_LoginPeriod", 
                "RateLimit_OtpLimit",
                "RateLimit_OtpPeriod",
                "RateLimit_ApiLimit",
                "RateLimit_ApiPeriod",
                "RateLimit_HourlyLimit",
                "RateLimit_PostLimit",
                "RateLimit_PutLimit", 
                "RateLimit_DeleteLimit",
                "RateLimit_GetLimit"
            );

            return new RateLimitConfig
            {
                Enabled = Convert.ToBoolean(config["RateLimit_Enabled"] ?? false),
                LoginLimit = Convert.ToInt32(config["RateLimit_LoginLimit"] ?? 10),
                LoginPeriod = config["RateLimit_LoginPeriod"]?.ToString() ?? "15m",
                OtpLimit = Convert.ToInt32(config["RateLimit_OtpLimit"] ?? 5), 
                OtpPeriod = config["RateLimit_OtpPeriod"]?.ToString() ?? "15m",
                ApiLimit = Convert.ToInt32(config["RateLimit_ApiLimit"] ?? 120),
                ApiPeriod = config["RateLimit_ApiPeriod"]?.ToString() ?? "1m",
                HourlyLimit = Convert.ToInt32(config["RateLimit_HourlyLimit"] ?? 2000),
                PostLimit = Convert.ToInt32(config["RateLimit_PostLimit"] ?? 60),
                PutLimit = Convert.ToInt32(config["RateLimit_PutLimit"] ?? 40),
                DeleteLimit = Convert.ToInt32(config["RateLimit_DeleteLimit"] ?? 20),
                GetLimit = Convert.ToInt32(config["RateLimit_GetLimit"] ?? 120)
            };
        }

        /// <summary>
        /// Get security configuration
        /// </summary>
        public async Task<SecurityConfig> GetSecurityConfigAsync()
        {
            var config = await GetConfigBatchAsync(
                "Security_OtpEnabled",
                "Security_SessionTimeoutMinutes",
                "Security_MaxPasswordAttempts",
                "Security_MaxOtpAttempts",
                "Security_PasswordExpirationMonths",
                "Security_OtpIssuer"
            );

            return new SecurityConfig
            {
                OtpEnabled = Convert.ToBoolean(config["Security_OtpEnabled"] ?? true),
                SessionTimeoutMinutes = Convert.ToInt32(config["Security_SessionTimeoutMinutes"] ?? 60),
                MaxPasswordAttempts = Convert.ToInt32(config["Security_MaxPasswordAttempts"] ?? 5),
                MaxOtpAttempts = Convert.ToInt32(config["Security_MaxOtpAttempts"] ?? 3),
                PasswordExpirationMonths = Convert.ToInt32(config["Security_PasswordExpirationMonths"] ?? 6),
                OtpIssuer = config["Security_OtpIssuer"]?.ToString() ?? "Petel External Students System"
            };
        }

        private T? ConvertValue<T>(string value, string valueType, T? defaultValue = default)
        {
            try
            {
                return valueType.ToLower() switch
                {
                    "boolean" => (T)(object)bool.Parse(value),
                    "integer" => (T)(object)int.Parse(value),
                    "decimal" => (T)(object)decimal.Parse(value),
                    "double" => (T)(object)double.Parse(value),
                    "string" => (T)(object)value,
                    _ => (T)(object)value
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert configuration value '{Value}' to type '{Type}', using default", value, typeof(T).Name);
                return defaultValue;
            }
        }

        private string GetValueType(object value)
        {
            return value switch
            {
                bool => "boolean",
                int => "integer", 
                decimal => "decimal",
                double => "double",
                _ => "string"
            };
        }
    }

    public class RateLimitConfig
    {
        public bool Enabled { get; set; }
        public int LoginLimit { get; set; }
        public string LoginPeriod { get; set; } = "15m";
        public int OtpLimit { get; set; }
        public string OtpPeriod { get; set; } = "15m";
        public int ApiLimit { get; set; }
        public string ApiPeriod { get; set; } = "1m";
        public int HourlyLimit { get; set; }
        public int PostLimit { get; set; }
        public int PutLimit { get; set; }
        public int DeleteLimit { get; set; }
        public int GetLimit { get; set; }
    }

    public class SecurityConfig
    {
        public bool OtpEnabled { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public int MaxPasswordAttempts { get; set; }
        public int MaxOtpAttempts { get; set; }
        public int PasswordExpirationMonths { get; set; }
        public string OtpIssuer { get; set; } = string.Empty;
    }
}