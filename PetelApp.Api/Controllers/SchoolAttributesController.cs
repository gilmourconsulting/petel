using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class SchoolAttributesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SchoolAttributeCache _attributeCache;
    private readonly ILogger<SchoolAttributesController> _logger;

    public SchoolAttributesController(
        AppDbContext context,
        SchoolAttributeCache attributeCache,
        ILogger<SchoolAttributesController> logger)
    {
        _context = context;
        _attributeCache = attributeCache;
        _logger = logger;
    }

    /// <summary>
    /// Get school attributes for a specific school year with formatted display values
    /// NO AUTHENTICATION REQUIRED - Similar to system attributes pattern
    /// </summary>
    [HttpGet("by-school-year/{schoolYearId}")]
    public async Task<IActionResult> GetBySchoolYear(int schoolYearId)
    {
        try
        {
            _logger.LogInformation(
                "Loading school attributes for school year {SchoolYearId}", 
                schoolYearId
            );

            // Get all attributes for this school year
            var attributes = await _context.SchoolAttributes
                .AsNoTracking()
                .Where(a => a.SchoolYearId == schoolYearId && a.IsLastVersion)
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} school attributes for school year {SchoolYearId}", 
                attributes.Count, 
                schoolYearId
            );

            // Format attributes with display values
            var formattedAttributes = attributes.Select(attr =>
            {
                var attributeType = _attributeCache.GetAttributeType(attr.SchoolAttributeId);
                if (attributeType == null)
                {
                    _logger.LogWarning(
                        "Attribute type not found in cache for ID {AttributeId}", 
                        attr.SchoolAttributeId
                    );
                    return null;
                }

                string displayValue = attr.Value ?? string.Empty;

                // For list types, lookup the display value from school_attribute_type_values
                if (attributeType.AttributeValueType == "List" && !string.IsNullOrEmpty(attr.Value))
                {
                    // Try to parse the value as an integer ID
                    if (int.TryParse(attr.Value, out int valueId))
                    {
                        // Get all values for this attribute type
                        var values = _attributeCache.GetAttributeValues(attributeType.Id);
                        
                        // Find matching value by ID
                        var matchingValue = values.FirstOrDefault(v => v.Id == valueId);
                        
                        if (matchingValue != null && !string.IsNullOrEmpty(matchingValue.Value))
                        {
                            displayValue = matchingValue.Value;
                            _logger.LogDebug(
                                "Mapped List value ID {ValueId} to '{DisplayValue}' for attribute {AttributeName}",
                                valueId, displayValue, attributeType.Name
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                "List value ID {ValueId} not found for attribute {AttributeName}",
                                valueId, attributeType.Name
                            );
                            displayValue = $"ID: {valueId} (לא נמצא)";
                        }
                    }
                    else
                    {
                        // Value is not an ID, try direct string match (fallback)
                        var values = _attributeCache.GetAttributeValues(attributeType.Id);
                        var matchingValue = values.FirstOrDefault(v => 
                            v.Value != null && v.Value.Equals(attr.Value, StringComparison.OrdinalIgnoreCase)
                        );
                        
                        if (matchingValue != null)
                        {
                            displayValue = matchingValue.Value ?? attr.Value;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "List value '{Value}' not found in lookup for attribute {AttributeName}",
                                attr.Value, attributeType.Name
                            );
                        }
                    }
                }
                // For boolean types, convert to Hebrew
                else if (attributeType.AttributeValueType == "Boolean")
                {
                    displayValue = attr.Value?.ToLower() == "true" || attr.Value == "1" 
                        ? "כן" 
                        : "לא";
                }

                return new
                {
                    id = attr.Id,
                    attributeId = attr.SchoolAttributeId,
                    name = attributeType.Name,
                    hebrewName = attributeType.HebrewName,
                    valueType = attributeType.AttributeValueType,
                    value = attr.Value,
                    displayValue = displayValue,
                    version = attr.Version,
                    updatedAt = attr.UpdatedAt
                };
            })
            .Where(a => a != null)
            .ToList();

            return Ok(new
            {
                success = true,
                data = formattedAttributes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving school attributes for school year {SchoolYearId}", schoolYearId);
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בטעינת שירותי בית הספר",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all attribute types (for reference/admin purposes)
    /// </summary>
    [HttpGet("types")]
    public IActionResult GetAttributeTypes()
    {
        try
        {
            var types = _attributeCache.GetAllAttributeTypes()
                .Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    hebrewName = t.HebrewName,
                    valueType = t.AttributeValueType,
                    createdAt = t.CreatedAt
                });

            return Ok(new
            {
                success = true,
                data = types
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attribute types");
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בטעינת סוגי שירותים",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get possible values for a specific attribute type (for dropdown population)
    /// NO AUTHENTICATION REQUIRED - Global configuration data
    /// </summary>
    [HttpGet("attribute-values/{attributeTypeId}")]
    public IActionResult GetAttributeTypeValues(int attributeTypeId)
    {
        try
        {
            _logger.LogInformation("Getting attribute values for type {AttributeTypeId}", attributeTypeId);
            
            var values = _attributeCache.GetAttributeValues(attributeTypeId)
                .OrderBy(v => v.SortOrder)
                .Select(v => new
                {
                    id = v.Id,
                    value = v.Value,
                    sortOrder = v.SortOrder
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = values
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attribute values for type {AttributeTypeId}", attributeTypeId);
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בטעינת ערכי שירות",
                error = ex.Message
            });
        }
    }
}