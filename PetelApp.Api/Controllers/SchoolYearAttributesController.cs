using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolYearAttributesController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolYearAttributesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolYearAttributesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get all attributes for a specific school year
        /// </summary>
        [HttpGet("year/{yearId}")]
        public async Task<IActionResult> GetAttributesByYear(int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Loading attributes for year {YearId}", yearId);

                var attributes = await _context.SchoolYearAttributes
                    .AsNoTracking()
                    .Where(sya => sya.YearId == yearId)
                    .Select(sya => new
                    {
                        sya.Id,
                        sya.YearId,
                        sya.Name,
                        sya.Description,
                        sya.Value,
                        sya.CreatedAt,
                        sya.CreatedUser,
                        sya.UpdatedAt,
                        sya.UpdateUser
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} attributes for year {YearId}", attributes.Count, yearId);

                return Ok(new { success = true, data = attributes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attributes for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מאפייני שנה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get a specific attribute value by year and attribute name
        /// </summary>
        [HttpGet("year/{yearId}/attribute/{attributeName}")]
        public async Task<IActionResult> GetAttributeValue(int yearId, string attributeName)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Loading attribute {AttributeName} for year {YearId}", attributeName, yearId);

                var attribute = await _context.SchoolYearAttributes
                    .AsNoTracking()
                    .Where(sya => sya.YearId == yearId && sya.Name == attributeName)
                    .Select(sya => new
                    {
                        sya.Id,
                        sya.YearId,
                        sya.Name,
                        sya.Description,
                        sya.Value
                    })
                    .FirstOrDefaultAsync();

                if (attribute == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"מאפיין '{attributeName}' לא נמצא עבור שנה {yearId}"
                    });
                }

                return Ok(new { success = true, data = attribute });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attribute {AttributeName} for year {YearId}", attributeName, yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מאפיין שנה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create or update a school year attribute
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrUpdateAttribute([FromBody] SchoolYearAttributeRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate required fields
                if (request.YearId <= 0)
                    return BadRequest(new { success = false, message = "מזהה שנה נדרש" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "שם מאפיין נדרש" });

                if (string.IsNullOrWhiteSpace(request.Value))
                    return BadRequest(new { success = false, message = "ערך מאפיין נדרש" });

                // Get user ID from session
                int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

                // Check if attribute exists
                var existingAttribute = await _context.SchoolYearAttributes
                    .Where(sya => sya.YearId == request.YearId && sya.Name == request.Name)
                    .FirstOrDefaultAsync();

                if (existingAttribute != null)
                {
                    // Update existing
                    existingAttribute.Value = request.Value;
                    if (!string.IsNullOrWhiteSpace(request.Description))
                        existingAttribute.Description = request.Description;
                    existingAttribute.UpdatedAt = DateTime.UtcNow;
                    existingAttribute.UpdateUser = userId;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated attribute {Name} for year {YearId}", request.Name, request.YearId);

                    return Ok(new
                    {
                        success = true,
                        message = "מאפיין עודכן בהצלחה",
                        data = new
                        {
                            existingAttribute.Id,
                            existingAttribute.YearId,
                            existingAttribute.Name,
                            existingAttribute.Description,
                            existingAttribute.Value
                        }
                    });
                }
                else
                {
                    // Create new
                    var newAttribute = new SchoolYearAttribute
                    {
                        YearId = request.YearId,
                        Name = request.Name,
                        Description = request.Description,
                        Value = request.Value,
                        CreatedAt = DateTime.UtcNow,
                        CreatedUser = userId,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUser = userId
                    };

                    _context.SchoolYearAttributes.Add(newAttribute);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created attribute {Name} for year {YearId}", request.Name, request.YearId);

                    return Ok(new
                    {
                        success = true,
                        message = "מאפיין נוצר בהצלחה",
                        data = new
                        {
                            newAttribute.Id,
                            newAttribute.YearId,
                            newAttribute.Name,
                            newAttribute.Description,
                            newAttribute.Value
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating attribute");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בשמירת מאפיין",
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request model for creating/updating school year attributes
    /// </summary>
    public class SchoolYearAttributeRequest
    {
        public int YearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
