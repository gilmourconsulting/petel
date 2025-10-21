using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Models;
using System.Text.Json;

namespace PetelApp.Api.Controllers


{
    /// <summary>
    /// Controller for system attributes management
    /// System attributes are global configuration values available to all users without authentication
    /// DO NOT confuse with user session data - these are application-wide settings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : ControllerBase
    {
        private readonly SystemAttributeCache _cache;
        private readonly ILogger<SystemAttributesController> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SystemAttributesController(
            SystemAttributeCache cache,
            ILogger<SystemAttributesController> logger,
            IServiceProvider serviceProvider)
        {
            _cache = cache;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }


private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
        /// <summary>
        /// Get all system attributes - NO AUTHENTICATION REQUIRED
        /// Returns global configuration accessible to all users
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetAllSystemAttributes()
        {
            try
            {
                if (!_cache.IsLoaded())
                {
                    _logger.LogWarning("System attributes cache not loaded yet");
                    return StatusCode(503, new { message = "System attributes not loaded yet" });
                }

                var attributes = _cache.GetAllAttributes();
                var dtos = attributes.Select(a => MapToDto(a)).ToList();

                _logger.LogDebug("Retrieved {Count} system attributes", dtos.Count);
                return new JsonResult(dtos, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system attributes");
                return StatusCode(500, new { message = "Error retrieving system attributes" });
            }
        }

        /// <summary>
        /// Get system attribute by name - NO AUTHENTICATION REQUIRED
        /// </summary>
        [HttpGet("by-name/{name}")]
        [AllowAnonymous]
        public IActionResult GetSystemAttributeByName(string name)
        {
            try
            {
                if (!_cache.IsLoaded())
                {
                    return StatusCode(503, new { message = "System attributes not loaded yet" });
                }

                var attribute = _cache.GetAttributeByName(name);
                
                if (attribute == null)
                {
                    _logger.LogWarning("System attribute not found: {Name}", name);
                    return NotFound(new { message = $"System attribute '{name}' not found" });
                }

                var dto = MapToDto(attribute);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system attribute: {Name}", name);
                return StatusCode(500, new { message = "Error retrieving system attribute" });
            }
        }

        /// <summary>
        /// Get system attributes by foreign ID - NO AUTHENTICATION REQUIRED
        /// Used to group related attributes (e.g., all attributes for a specific entity type)
        /// </summary>
        [HttpGet("by-foreign-id/{foreignId}")]
        [AllowAnonymous]
        public IActionResult GetSystemAttributesByForeignId(int foreignId)
        {
            try
            {
                if (!_cache.IsLoaded())
                {
                    return StatusCode(503, new { message = "System attributes not loaded yet" });
                }

                var attributes = _cache.GetAttributesByForeignId(foreignId);
                var dtos = attributes.Select(a => MapToDto(a)).ToList();

                _logger.LogDebug("Retrieved {Count} system attributes for foreign_id {ForeignId}", 
                    dtos.Count, foreignId);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system attributes for foreign_id: {ForeignId}", foreignId);
                return StatusCode(500, new { message = "Error retrieving system attributes" });
            }
        }

        /// <summary>
        /// Get system attributes by value type - NO AUTHENTICATION REQUIRED
        /// </summary>
        [HttpGet("by-type/{valueType}")]
        [AllowAnonymous]
        public IActionResult GetSystemAttributesByType(string valueType)
        {
            try
            {
                if (!_cache.IsLoaded())
                {
                    return StatusCode(503, new { message = "System attributes not loaded yet" });
                }

                var attributes = _cache.GetAttributesByType(valueType);
                var dtos = attributes.Select(a => MapToDto(a)).ToList();

                _logger.LogDebug("Retrieved {Count} system attributes of type {ValueType}", 
                    dtos.Count, valueType);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system attributes by type: {ValueType}", valueType);
                return StatusCode(500, new { message = "Error retrieving system attributes" });
            }
        }

        /// <summary>
        /// Reload system attributes from database - NO AUTHENTICATION REQUIRED
        /// Per coding guidelines: System attributes are global config, no auth needed
        /// Admin operation to refresh cache from database
        /// </summary>
        [HttpPost("reload")]
        [AllowAnonymous]
        public async Task<IActionResult> ReloadSystemAttributes()
        {
            try
            {
                _logger.LogInformation("Reloading system attributes from database");
                
                // Create scope for database access
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Load fresh attributes from database
                var attributes = await context.SystemAttributes
                    .ToListAsync();
                
                // Map to DTOs
                var dtos = attributes.Select(a => MapToDto(a)).ToList();
                
                // Reload cache using existing LoadAttributes function
                _cache.LoadAttributes(attributes);
                
                _logger.LogInformation("Successfully reloaded {Count} system attributes from database", attributes.Count);
                
                return Ok(new { 
                    success = true,
                    message = "System attributes reloaded successfully from database",
                    count = attributes.Count,
                    lastLoaded = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading system attributes from database");
                return StatusCode(500, new { 
                    success = false,
                    message = "Error reloading system attributes from database" 
                });
            }
        }

        /// <summary>
        /// Get cache statistics - NO AUTHENTICATION REQUIRED
        /// Per coding guidelines: System attributes are global config, no auth needed
        /// </summary>
        [HttpGet("cache-stats")]
        [AllowAnonymous]
        public IActionResult GetCacheStats()
        {
            try
            {
                var lastLoaded = _cache.GetLastLoadedTime();
                var allAttributes = _cache.GetAllAttributes().ToList();
                var typeGroups = allAttributes.GroupBy(a => a.ValueType)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Ok(new
                {
                    isLoaded = _cache.IsLoaded(),
                    lastLoaded = lastLoaded,
                    totalAttributes = allAttributes.Count,
                    attributesByType = typeGroups,
                    status = _cache.IsLoaded() ? "active" : "not_loaded"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache stats");
                return StatusCode(500, new { message = "Error retrieving cache statistics" });
            }
        }

        /// <summary>
        /// Helper method to map SystemAttribute to SystemAttributeDto
        /// Reusable mapping logic - SINGLE SOURCE OF TRUTH
        /// </summary>
        private SystemAttributeDto MapToDto(SystemAttribute attribute)
        {
            return new SystemAttributeDto
            {
                Id = attribute.Id,
                Name = attribute.Name,
                Value = attribute.Value,
                ValueType = attribute.ValueType,
                Description = attribute.Description,
                UpdateUser = attribute.UpdateUser,
                ForeignId = attribute.ForeignId,
                CreatedAt = attribute.CreatedAt,
                UpdatedAt = attribute.UpdatedAt
            };
        }
    }
}