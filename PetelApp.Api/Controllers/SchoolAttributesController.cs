using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class SchoolAttributesController : BaseController
{
    private readonly AppDbContext _context;
    private readonly SchoolAttributeCache _attributeCache;


    public SchoolAttributesController(
        AppDbContext context,
        SchoolAttributeCache attributeCache,
        UserSessionService userSessionService,
        ILogger<SchoolAttributesController> logger)
        : base(userSessionService, logger)
    {
        _context = context;
        _attributeCache = attributeCache;
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
                .Select(a => new
            {
                a.Id,
                a.SchoolYearId,
                a.SchoolAttributeId,
                a.Value,
                a.Version,
                a.IsLastVersion,
                a.UserId
            })
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
    /// Update school attributes with versioning
    /// Creates new version for each changed attribute, marks old version as not last
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateSchoolAttributes([FromBody] UpdateSchoolAttributesRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Updating school attributes for school year {SchoolYearId}", 
                request.SchoolYearId
            );

            if (request.Attributes == null || !request.Attributes.Any())
            {
                return BadRequest(new { success = false, message = "לא נמצאו שירותים לעדכון" });
            }
            // Get user ID from session following Authentication & Session Management
            var session = GetCurrentSession();
            var userId = int.TryParse(session?.UserId, out int parsedUserId) ? parsedUserId : 0;
            
            var updatedRecords = new List<object>();

            foreach (var attr in request.Attributes)
            {
                _logger.LogInformation(
                    "Processing attribute {AttributeId} with ID {Id}, Version {Version}",
                    attr.AttributeId, attr.Id, attr.Version
                );

                // If no existing record (id is null), create new
                if (attr.Id == null)
                {
                    var newAttribute = new SchoolAttribute
                    {
                        SchoolYearId = request.SchoolYearId,
                        SchoolAttributeId = attr.AttributeId,
                        Value = attr.Value,
                        Version = 1,
                        IsLastVersion = true,
                        UserId = userId  // Set user ID from session for new records
                    };

                    _context.SchoolAttributes.Add(newAttribute);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Created new attribute record with ID {Id}",
                        newAttribute.Id
                    );

                    updatedRecords.Add(new
                    {
                        id = newAttribute.Id,
                        attributeId = newAttribute.SchoolAttributeId,
                        value = newAttribute.Value,
                        version = newAttribute.Version
                    });

                    continue;
                }

                // Find existing record
            var existingRecord = await _context.SchoolAttributes
                .Where(a => 
                    a.Id == attr.Id && 
                    a.Version == attr.Version &&
                    a.IsLastVersion)
                .Select(a => new
                {
                    //Entity = a,
                    a.Id,
                    a.SchoolYearId,
                    a.SchoolAttributeId,
                    a.Value,
                    a.Version

                })
                .FirstOrDefaultAsync();

                if (existingRecord == null)
                {
                    _logger.LogWarning(
                        "Attribute record not found or version mismatch: ID {Id}, Version {Version}",
                        attr.Id, attr.Version
                    );
                    return Conflict(new
                    {
                        success = false,
                        message = "הרשומה השתנתה על ידי משתמש אחר. אנא רענן את המסך ונסה שוב.",
                        attributeId = attr.AttributeId
                    });
                }

                // Check if value actually changed
                if (existingRecord.Value == attr.Value)
                {
                    _logger.LogInformation(
                        "No change detected for attribute {AttributeId}, skipping",
                        attr.AttributeId
                    );
                    
                    updatedRecords.Add(new
                    {
                        id = existingRecord.Id,
                        attributeId = existingRecord.SchoolAttributeId,
                        value = existingRecord.Value,
                        version = existingRecord.Version
                    });
                    
                    continue;
                }

                // Step 1: Mark existing record as not last version
            //    existingRecord.IsLastVersion = false;
               // existingRecord.UpdatedAt = DateTime.UtcNow;

            // Load the entity for update (without timetz columns by using Find which loads by PK)
            // Then update only the fields we need
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE petel_schema.school_attributes SET is_last_version = false WHERE id = {0}",
                attr.Id
            );

                // Step 2: Create new version
                var newVersion = new SchoolAttribute
                {
                    SchoolYearId = existingRecord.SchoolYearId,
                    SchoolAttributeId = existingRecord.SchoolAttributeId,
                    Value = attr.Value,
                    Version = existingRecord.Version + 1,
                    IsLastVersion = true,            
                    UserId = userId  // Set user ID from session for new version
                };

                _context.SchoolAttributes.Add(newVersion);

                // Step 3: Save changes
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created new version {NewVersion} for attribute {AttributeId}, old record ID {OldId}, new record ID {NewId}",
                    newVersion.Version, attr.AttributeId, existingRecord.Id, newVersion.Id
                );

                // Step 4: Retrieve the new record with generated ID
            // Step 4: Return new record data (ID populated by EF Core after SaveChanges)
            updatedRecords.Add(new
            {
                id = newVersion.Id,
                attributeId = newVersion.SchoolAttributeId,
                value = newVersion.Value,
                version = newVersion.Version
            });
            }

            return Ok(new
            {
                success = true,
                message = "שירותי בית הספר עודכנו בהצלחה",
                data = updatedRecords
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating school attributes for school year {SchoolYearId}", request.SchoolYearId);
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בעדכון שירותי בית הספר",
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

public class UpdateSchoolAttributesRequest
{
    public int SchoolYearId { get; set; }
    public List<AttributeUpdate> Attributes { get; set; } = new();
}

public class AttributeUpdate
{
    public int? Id { get; set; }              // Existing record ID (null for new)
    public int AttributeId { get; set; }      // school_attribute_id
    public string? Value { get; set; }        // New value
    public int Version { get; set; }          // Current version (for optimistic locking)
}