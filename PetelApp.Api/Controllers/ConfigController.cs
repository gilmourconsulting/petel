using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]  // ✅ No auth needed for public config
    public class ConfigController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigController> _logger;

        public ConfigController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<ConfigController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Get client-safe configuration
        /// ✅ SECURE: Uses SystemAttributes for dynamic URL configuration
        /// </summary>
        [HttpGet("client")]
        public async Task<IActionResult> GetClientConfig()
        {
            try
            {
                // ✅ Get API Base URL from SystemAttributes table (id=2)
                var apiUrlAttribute = await _context.SystemAttributes
                    .Where(sa => sa.Id == 2)
                    .FirstOrDefaultAsync();

                string apiBaseUrl;

                if (apiUrlAttribute != null && !string.IsNullOrWhiteSpace(apiUrlAttribute.Value))
                {
                    // Use value from database
                    apiBaseUrl = apiUrlAttribute.Value.TrimEnd('/');
                    _logger.LogInformation("API Base URL loaded from SystemAttributes: {Url}", apiBaseUrl);
                }
                else
                {
                    // Fallback to appsettings or default
                    apiBaseUrl = _configuration["ClientConfig:ApiBaseUrl"] 
                                ?? "http://localhost:5082";
                    _logger.LogWarning("API Base URL not found in SystemAttributes (id=2), using default: {Url}", apiBaseUrl);
                }

                return Ok(new
                {
                    apiBaseUrl = apiBaseUrl,
                    environment = _configuration["Environment"] ?? "development"
                    // ❌ DON'T include: connection strings, secrets, internal URLs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading client configuration");
                
                // ✅ SECURE: Return safe fallback, don't expose error details
                return Ok(new
                {
                    apiBaseUrl = "http://localhost:5082",  // Safe development default
                    environment = "development"
                });
            }
        }
    }
}