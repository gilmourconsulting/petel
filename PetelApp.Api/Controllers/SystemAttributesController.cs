using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;
using PetelApp.Api.Models;

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
        private readonly SystemAttributeLoaderHostedService _loaderService;
        private readonly ILogger<SystemAttributesController> _logger;

        public SystemAttributesController(
            SystemAttributeCache cache,
            SystemAttributeLoaderHostedService loaderService,
            ILogger<SystemAttributesController> logger)
        {
            _cache = cache;
            _loaderService = loaderService;
            _logger = logger;
        }

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
                var dtos = attributes.Select(a => new SystemAttributeDto
                {
                    Name = a.Name,
                    Value = a.Value,
                    Description = a.Description,
                    ValueType = a.ValueType
                }).ToList();

                _logger.LogDebug("Retrieved {Count} system attributes", dtos.Count);
                return Ok(dtos);
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

                var dto = new SystemAttributeDto
                {
                    Name = attribute.Name,
                    Value = attribute.Value,
                    Description = attribute.Description,
                    ValueType = attribute.ValueType
                };

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
                var dtos = attributes.Select(a => new SystemAttributeDto
                {
                    Name = a.Name,
                    Value = a.Value,
                    Description = a.Description,
                    ValueType = a.ValueType
                }).ToList();

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
                var dtos = attributes.Select(a => new SystemAttributeDto
                {
                    Name = a.Name,
                    Value = a.Value,
                    Description = a.Description,
                    ValueType = a.ValueType
                }).ToList();

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
        /// Reload system attributes from database - REQUIRES AUTHENTICATION
        /// Only for admin/maintenance purposes
        /// </summary>
        [HttpPost("reload")]
        [Authorize]
        public async Task<IActionResult> ReloadSystemAttributes()
        {
            try
            {
                await _loaderService.LoadAttributesAsync();
                _logger.LogInformation("System attributes reloaded by admin request");
                return Ok(new { 
                    message = "System attributes reloaded successfully",
                    lastLoaded = _cache.GetLastLoadedTime(),
                    attributeCount = _cache.GetAllAttributes().Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading system attributes");
                return StatusCode(500, new { message = "Error reloading system attributes" });
            }
        }

        /// <summary>
        /// Get cache statistics - REQUIRES AUTHENTICATION
        /// </summary>
        [HttpGet("cache-stats")]
        [Authorize]
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
    }
}