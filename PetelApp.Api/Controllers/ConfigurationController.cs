using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Controllers;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : BaseController
    {
        private readonly DatabaseConfigurationService _configService;

        public ConfigurationController(
            UserSessionService userSessionService,
            ILogger<ConfigurationController> logger,
            DatabaseConfigurationService configService)  
            : base(userSessionService, logger)
        {
            _configService = configService;
        }

        /// <summary>
        /// Get all configuration settings (admin only)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllConfiguration()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            // TODO: Add admin role check
            // if (!session.HasRole("admin")) return Forbid();

            try
            {
                var rateLimitConfig = await _configService.GetRateLimitConfigAsync();
                var securityConfig = await _configService.GetSecurityConfigAsync();

                var systemConfig = await _configService.GetConfigBatchAsync(
                    "Environment.Name",
                    "Environment.RateLimitMultiplier", 
                    "System.EnableDetailedLogging",
                    "System_MaintenanceMode",
                    "System_MaintenanceMessage"
                );

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        RateLimit = rateLimitConfig,
                        Security = securityConfig,
                        System = systemConfig
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת הגדרות" });
            }
        }

        /// <summary>
        /// Update configuration setting (admin only)
        /// </summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateConfiguration(string key, [FromBody] ConfigUpdateRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            // TODO: Add admin role check
            // if (!session.HasRole("admin")) return Forbid();

            try
            {
                var success = await _configService.SetConfigAsync(key, request.Value, request.Description);
                
                if (success)
                {
                    _logger.LogInformation("Configuration '{Key}' updated by user {UserId}", key, session.UserId);
                    return Ok(new { success = true, message = "הגדרה עודכנה בהצלחה" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "שגיאה בעדכון ההגדרה" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration key '{Key}'", key);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון ההגדרה" });
            }
        }

        /// <summary>
        /// Get rate limiting status and configuration
        /// </summary>
        [HttpGet("rate-limit")]
        public async Task<IActionResult> GetRateLimitConfig()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var config = await _configService.GetRateLimitConfigAsync();
                return Ok(new { success = true, data = config });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rate limit configuration");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת הגדרות" });
            }
        }

        /// <summary>
        /// Update rate limiting settings
        /// </summary>
        [HttpPut("rate-limit")]
        public async Task<IActionResult> UpdateRateLimitConfig([FromBody] RateLimitUpdateRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            // TODO: Add admin role check

            try
            {
                var updates = new Dictionary<string, object>
                {
                    ["Features.RateLimitingEnabled"] = request.Enabled,
                    ["RateLimit_LoginAttemptsLimit"] = request.LoginLimit,
                    ["RateLimit_OtpValidationLimit"] = request.OtpLimit,
                    ["RateLimit_ApiRequestsLimit"] = request.ApiLimit,
                    ["RateLimit_ApiHourlyLimit"] = request.HourlyLimit
                };

                var success = true;
                foreach (var update in updates)
                {
                    var result = await _configService.SetConfigAsync(update.Key, update.Value);
                    if (!result) success = false;
                }

                if (success)
                {
                    _logger.LogInformation("Rate limiting configuration updated by user {UserId}", session.UserId);
                    return Ok(new { success = true, message = "הגדרות Rate Limiting עודכנו בהצלחה" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "שגיאה בעדכון חלק מההגדרות" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rate limit configuration");
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון ההגדרות" });
            }
        }

        /// <summary>
        /// Clear configuration cache (admin only) 
        /// </summary>
        [HttpPost("clear-cache")]
        public IActionResult ClearConfigurationCache()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            // TODO: Add admin role check

            try
            {
                _configService.ClearCache();
                _logger.LogInformation("Configuration cache cleared by user {UserId}", session.UserId);
                return Ok(new { success = true, message = "מטמון הגדרות נוקה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing configuration cache");
                return StatusCode(500, new { success = false, message = "שגיאה בניקוי המטמון" });
            }
        }

        /// <summary>
        /// Toggle maintenance mode
        /// </summary>
        [HttpPost("maintenance")]
        public async Task<IActionResult> ToggleMaintenanceMode([FromBody] MaintenanceRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            // TODO: Add admin role check

            try
            {
                await _configService.SetConfigAsync("System_MaintenanceMode", request.Enabled);
                if (!string.IsNullOrEmpty(request.Message))
                {
                    await _configService.SetConfigAsync("System_MaintenanceMessage", request.Message);
                }

                _logger.LogInformation("Maintenance mode {Status} by user {UserId}", 
                    request.Enabled ? "enabled" : "disabled", session.UserId);

                return Ok(new { 
                    success = true, 
                    message = request.Enabled ? "מצב תחזוקה הופעל" : "מצב תחזוקה הופסק" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling maintenance mode");
                return StatusCode(500, new { success = false, message = "שגיאה בשינוי מצב תחזוקה" });
            }
        }
    }

    public class ConfigUpdateRequest
    {
        public object Value { get; set; } = default!;
        public string? Description { get; set; }
    }

    public class RateLimitUpdateRequest
    {
        public bool Enabled { get; set; }
        public int LoginLimit { get; set; }
        public int OtpLimit { get; set; }
        public int ApiLimit { get; set; }
        public int HourlyLimit { get; set; }
    }

    public class MaintenanceRequest
    {
        public bool Enabled { get; set; }
        public string? Message { get; set; }
    }
}