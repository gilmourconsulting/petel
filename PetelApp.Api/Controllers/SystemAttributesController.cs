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
        private readonly AppDbContext _context;

        public SystemAttributesController(
            SystemAttributeCache cache,
            ILogger<SystemAttributesController> logger,
            IServiceProvider serviceProvider,
            AppDbContext context)
        {
            _cache = cache;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _context = context;
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

                // ✅ Reload directly from database into cache
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var attributes = await dbContext.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();

                _cache.LoadAttributes(attributes);

                _logger.LogInformation("Successfully reloaded {Count} system attributes from database", attributes.Count);

                return Ok(new
                {
                    success = true,
                    message = "System attributes reloaded successfully from database",
                    lastLoaded = DateTime.UtcNow,
                    count = attributes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading system attributes from database");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error reloading system attributes from database"
                });
            }
        }

        /// <summary>
        /// Refresh system attributes cache - NO AUTHENTICATION REQUIRED
        /// This endpoint is called after school attributes are updated to refresh the cache
        /// Per coding guidelines: System attributes are global config, no auth needed
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshSystemAttributes()
        {
            try
            {
                _logger.LogInformation("Refreshing system attributes cache after school attributes update");

                // Reload directly from database into cache
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var attributes = await dbContext.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();

                _cache.LoadAttributes(attributes);

                _logger.LogInformation("Successfully refreshed {Count} system attributes", attributes.Count);

                return Ok(new
                {
                    success = true,
                    message = "System attributes cache refreshed successfully",
                    count = attributes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing system attributes cache");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error refreshing system attributes cache"
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

        /// <summary>
        /// Get characterizations - NO AUTHENTICATION REQUIRED
        /// Returns a list of special needs characterizations
        /// </summary>
        [HttpGet("characterizations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCharacterizations()
        {
            try
            {
                var characterizations = await _context.SpecialNeedsCharacterizations
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = characterizations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading characterizations");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת אפיונים"
                });
            }
        }

        /// <summary>
        /// Get councils - NO AUTHENTICATION REQUIRED
        /// </summary>
        [HttpGet("councils")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCouncils()
        {
            try
            {
                _logger.LogInformation("Loading councils list");

                var councils = await _context.Councils
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        councilName = c.Name,
                        councilCode = c.CouncilCode
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = councils
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading councils");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רשויות"
                });
            }
        }

        /// <summary>
        /// Create a new system attribute - REQUIRES AUTHENTICATION
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSystemAttribute([FromBody] CreateSystemAttributeRequest request)
        {
            try
            {
                // TODO: Add authorization check for system admin role

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "שם ההגדרה הוא שדה חובה"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Value))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "ערך ההגדרה הוא שדה חובה"
                    });
                }

                // Check if attribute with same name already exists
                var existingAttribute = await _context.SystemAttributes
                    .FirstOrDefaultAsync(a => a.Name == request.Name);

                if (existingAttribute != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"הגדרה בשם '{request.Name}' כבר קיימת"
                    });
                }

                // Validate data type
                if (!string.IsNullOrWhiteSpace(request.ValueType) &&
                    !ValidateDataType(request.Value, request.ValueType))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"ערך לא תקין עבור סוג {request.ValueType}"
                    });
                }

                // Create new attribute
                var newAttribute = new SystemAttribute
                {
                    Name = request.Name,
                    Value = request.Value,
                    ValueType = request.ValueType ?? "string",
                    Description = request.Description,
                    ForeignId = request.ForeignId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                    // UpdateUser = session.UserId // TODO: Add when auth is ready
                };

                _context.SystemAttributes.Add(newAttribute);
                await _context.SaveChangesAsync();

                // Reload cache to include new attribute
                await ReloadCacheFromDatabase();

                _logger.LogInformation(
                    "System attribute created: {Name} (ID: {Id})",
                    newAttribute.Name, newAttribute.Id);

                return Ok(new
                {
                    success = true,
                    message = "הגדרה נוצרה בהצלחה",
                    attribute = MapToDto(newAttribute)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating system attribute");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת הגדרה חדשה"
                });
            }
        }

        /// <summary>
        /// Update a system attribute value - REQUIRES AUTHENTICATION
        /// Admin operation to modify system configuration
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSystemAttribute(int id, [FromBody] UpdateSystemAttributeRequest request)
        {
            try
            {
                // TODO: Add authorization check for system admin role

                if (string.IsNullOrWhiteSpace(request.Value))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "ערך ההגדרה לא יכול להיות ריק"
                    });
                }

                var attribute = await _context.SystemAttributes
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (attribute == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "הגדרה לא נמצאה"
                    });
                }

                // Validate data type
                if (!ValidateDataType(request.Value, attribute.ValueType))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"ערך לא תקין עבור סוג {attribute.ValueType}"
                    });
                }

                // Update attribute
                attribute.Value = request.Value;
                attribute.UpdatedAt = DateTime.UtcNow;
                // attribute.UpdateUser = session.UserId; // TODO: Add when auth is ready

                await _context.SaveChangesAsync();

                // Reload cache to apply changes immediately
                await ReloadCacheFromDatabase();

                _logger.LogInformation(
                    "System attribute updated: {Name} (ID: {Id})",
                    attribute.Name, id);

                return Ok(new
                {
                    success = true,
                    message = "הגדרה עודכנה בהצלחה",
                    attribute = MapToDto(attribute)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system attribute {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון הגדרה"
                });
            }
        }

        /// <summary>
        /// Delete a system attribute - REQUIRES AUTHENTICATION
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSystemAttribute(int id)
        {
            try
            {
                // TODO: Add authorization check for system admin role

                var attribute = await _context.SystemAttributes
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (attribute == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "הגדרה לא נמצאה"
                    });
                }

                _context.SystemAttributes.Remove(attribute);
                await _context.SaveChangesAsync();

                // Reload cache to remove deleted attribute
                await ReloadCacheFromDatabase();

                _logger.LogInformation(
                    "System attribute deleted: {Name} (ID: {Id})",
                    attribute.Name, id);

                return Ok(new
                {
                    success = true,
                    message = "הגדרה נמחקה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting system attribute {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת הגדרה"
                });
            }
        }

        /// <summary>
        /// Helper method to reload cache from database
        /// Uses existing LoadAttributes method from SystemAttributeCache
        /// </summary>
        private async Task ReloadCacheFromDatabase()
        {
            try
            {
                var attributes = await _context.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();

                // ✅ Use existing LoadAttributes method
                _cache.LoadAttributes(attributes);

                _logger.LogInformation("Cache reloaded with {Count} attributes", attributes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading cache from database");
                throw;
            }
        }

        /// <summary>
        /// Validate value against data type
        /// </summary>
        private bool ValidateDataType(string value, string dataType)
        {
            return dataType?.ToLower() switch
            {
                "integer" => int.TryParse(value, out _),
                "boolean" => bool.TryParse(value, out _) ||
                             value == "true" || value == "false",
                "string" => true,
                "sensitive" => true, // Sensitive strings (keys, passwords)
                _ => true // Unknown types pass validation
            };
        }
    }

    /// <summary>
    /// Request model for creating a new system attribute
    /// </summary>
    public class CreateSystemAttributeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ValueType { get; set; }
        public string? Description { get; set; }
        public int? ForeignId { get; set; }
    }

    /// <summary>
    /// Request model for updating a system attribute
    /// </summary>
    public class UpdateSystemAttributeRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}