// PetelApp.Api/Controllers/SchoolYearConfigController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolYearConfigController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolYearConfigController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolYearConfigController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get all Hebrew years for dropdown
        /// </summary>
        [HttpGet("hebrew-years")]
        public async Task<IActionResult> GetHebrewYears()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var years = await _context.HebrewYears
                    .AsNoTracking()
                    .OrderByDescending(y => y.Id)
                    .Select(y => new
                    {
                        id = y.Id,
                        hebrewYear = y.HebrewYearText
                    })
                    .ToListAsync();

                return Ok(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Hebrew years");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת שנים עבריות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get pricing elements for a specific year
        /// </summary>
        [HttpGet("pricing-elements/{yearId}")]
        public async Task<IActionResult> GetPricingElements(int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var elements = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(e => e.YearId == yearId)
                    .OrderBy(e => e.ElementName)
                    .Select(e => new
                    {
                        id = e.Id,
                        yearId = e.YearId,
                        name = e.ElementName,  // ✅ Use ElementName property
                        title = e.Title,
                        description = e.Description,
                        createdAt = e.CreatedAt
                    })
                    .ToListAsync();

                return Ok(elements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pricing elements for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רכיבי תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new pricing element
        /// </summary>
        [HttpPost("pricing-elements")]
        public async Task<IActionResult> AddPricingElement([FromBody] AddPricingElementRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new { success = false, message = "שם וכותרת הם שדות חובה" });
                }

                // Check for duplicates
                var exists = await _context.SpecialNeedsPricingElements
                    .AnyAsync(e => e.YearId == request.YearId && e.ElementName == request.Name.Trim());  // ✅ Use ElementName

                if (exists)
                {
                    return BadRequest(new { success = false, message = "רכיב תמחור עם שם זה כבר קיים לשנה זו" });
                }

                var element = new SpecialNeedsPricingElement
                {
                    YearId = request.YearId,
                    ElementName = request.Name.Trim(),  // ✅ Use ElementName property
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim(),
                    UserId = int.Parse(session.UserId),
                    CreatedAt = DateTime.UtcNow
                };

                _context.SpecialNeedsPricingElements.Add(element);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "רכיב התמחור נוסף בהצלחה",
                    id = element.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pricing element");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת רכיב תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get pricing categories for a specific element
        /// </summary>
        [HttpGet("pricing-categories/{elementId}")]
        public async Task<IActionResult> GetPricingCategories(int elementId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var categories = await _context.SpecialNeedsPricingCategories
                    .AsNoTracking()
                    .Where(c => c.PricingElement == elementId)
                    .OrderBy(c => c.Category)
                    .Select(c => new
                    {
                        id = c.Id,
                        pricingElement = c.PricingElement,
                        category = c.Category,
                        isLowestLevel = c.IsLowestLevel,
                        price = c.Price
                    })
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pricing categories for element {ElementId}", elementId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת קטגוריות תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new pricing category
        /// </summary>
        [HttpPost("pricing-categories")]
        public async Task<IActionResult> AddPricingCategory([FromBody] AddPricingCategoryRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate category range
                if (request.Category < 1 || request.Category > 9)
                {
                    return BadRequest(new { success = false, message = "קטגוריה חייבת להיות בין 1 ל-9" });
                }

                // Check for duplicates
                var exists = await _context.SpecialNeedsPricingCategories
                    .AnyAsync(c => c.PricingElement == request.PricingElement && c.Category == request.Category);

                if (exists)
                {
                    return BadRequest(new { success = false, message = "קטגוריה זו כבר קיימת לרכיב תמחור זה" });
                }

                var category = new SpecialNeedsPricingCategory
                {
                    PricingElement = request.PricingElement,
                    Category = request.Category,
                    IsLowestLevel = request.IsLowestLevel,
                    Price = request.Price,
                    UserId = int.Parse(session.UserId)
                };

                _context.SpecialNeedsPricingCategories.Add(category);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "קטגוריית התמחור נוספה בהצלחה",
                    id = category.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pricing category");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת קטגוריית תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update a pricing category
        /// </summary>
        [HttpPut("pricing-categories/{id}")]
        public async Task<IActionResult> UpdatePricingCategory(int id, [FromBody] UpdatePricingCategoryRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var category = await _context.SpecialNeedsPricingCategories
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { success = false, message = "קטגוריה לא נמצאה" });
                }

                category.IsLowestLevel = request.IsLowestLevel;
                category.Price = request.Price;
                category.UserId = int.Parse(session.UserId);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "קטגוריית התמחור עודכנה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pricing category {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון קטגוריית תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a pricing category
        /// </summary>
        [HttpDelete("pricing-categories/{id}")]
        public async Task<IActionResult> DeletePricingCategory(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var category = await _context.SpecialNeedsPricingCategories
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { success = false, message = "קטגוריה לא נמצאה" });
                }

                _context.SpecialNeedsPricingCategories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "קטגוריית התמחור נמחקה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pricing category {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת קטגוריית תמחור",
                    error = ex.Message
                });
            }
        }
    }

    // Request DTOs
    public class AddPricingElementRequest
    {
        public int YearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class AddPricingCategoryRequest
    {
        public int PricingElement { get; set; }
        public int Category { get; set; }
        public bool? IsLowestLevel { get; set; }
        public decimal? Price { get; set; }
    }

    public class UpdatePricingCategoryRequest
    {
        public bool? IsLowestLevel { get; set; }
        public decimal? Price { get; set; }
    }
}