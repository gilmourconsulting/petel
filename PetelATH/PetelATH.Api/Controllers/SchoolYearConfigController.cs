// PetelATH.Api/Controllers/SchoolYearConfigController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
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
                    .OrderBy(e => e.SortOrder)
                    .ThenBy(e => e.ElementName)
                    .Select(e => new
                    {
                        id = e.Id,
                        yearId = e.YearId,
                        name = e.ElementName,
                        title = e.Title,
                        description = e.Description,
                        sortOrder = e.SortOrder,
                        calculationLevel = e.CalculationLevel,
                        attributeToCheck = e.AttributeToCheck,
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
                    ElementName = request.Name.Trim(),
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim(),
                    SortOrder = request.SortOrder ?? 0,
                    CalculationLevel = request.CalculationLevel?.Trim(),
                    AttributeToCheck = request.AttributeToCheck?.Trim(),
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

        /// <summary>
        /// Get pricing steps for a specific category
        /// </summary>
        [HttpGet("pricing-steps/{categoryId}")]
        public async Task<IActionResult> GetPricingSteps(int categoryId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Get the category to verify it exists and get pricingElement
                var category = await _context.SpecialNeedsPricingCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == categoryId);

                if (category == null)
                {
                    return NotFound(new { success = false, message = "קטגוריה לא נמצאה" });
                }

                // Get steps for this pricing element and category
                var steps = await _context.SpecialNeedsPricingSteps
                    .AsNoTracking()
                    .Where(s => s.PricingElement == category.PricingElement && s.Category == category.Category)
                    .OrderBy(s => s.ObjectCheck)
                    .ThenBy(s => s.ObjectElementCheck)
                    .Select(s => new
                    {
                        id = s.Id,
                        pricingElement = s.PricingElement,
                        category = s.Category,
                        objectCheck = s.ObjectCheck,
                        objectElementCheck = s.ObjectElementCheck,
                        objectElementValue = s.ObjectElementValue,
                        price = s.Price
                    })
                    .ToListAsync();

                return Ok(steps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pricing steps for category {CategoryId}", categoryId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת שלבי תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new pricing step
        /// </summary>
        [HttpPost("pricing-steps")]
        public async Task<IActionResult> AddPricingStep([FromBody] AddPricingStepRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.ObjectCheck) ||
                    string.IsNullOrWhiteSpace(request.ObjectElementCheck) ||
                    string.IsNullOrWhiteSpace(request.ObjectElementValue))
                {
                    return BadRequest(new { success = false, message = "כל השדות הם חובה" });
                }

                // Check for duplicates
                var exists = await _context.SpecialNeedsPricingSteps
                    .AnyAsync(s => s.PricingElement == request.PricingElement &&
                                   s.Category == request.Category &&
                                   s.ObjectCheck == request.ObjectCheck.Trim() &&
                                   s.ObjectElementCheck == request.ObjectElementCheck.Trim() &&
                                   s.ObjectElementValue == request.ObjectElementValue.Trim());

                if (exists)
                {
                    return BadRequest(new { success = false, message = "שלב תמחור זה כבר קיים" });
                }

                var step = new SpecialNeedsPricingStep
                {
                    PricingElement = request.PricingElement,
                    Category = request.Category,
                    ObjectCheck = request.ObjectCheck.Trim(),
                    ObjectElementCheck = request.ObjectElementCheck.Trim(),
                    ObjectElementValue = request.ObjectElementValue.Trim(),
                    Price = request.Price,
                    UserId = int.Parse(session.UserId)
                };

                _context.SpecialNeedsPricingSteps.Add(step);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "שלב התמחור נוסף בהצלחה",
                    id = step.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pricing step");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת שלב תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update a pricing step
        /// </summary>
        [HttpPut("pricing-steps/{id}")]
        public async Task<IActionResult> UpdatePricingStep(int id, [FromBody] UpdatePricingStepRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var step = await _context.SpecialNeedsPricingSteps
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (step == null)
                {
                    return NotFound(new { success = false, message = "שלב לא נמצא" });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.ObjectCheck) ||
                    string.IsNullOrWhiteSpace(request.ObjectElementCheck) ||
                    string.IsNullOrWhiteSpace(request.ObjectElementValue))
                {
                    return BadRequest(new { success = false, message = "כל השדות הם חובה" });
                }

                step.ObjectCheck = request.ObjectCheck.Trim();
                step.ObjectElementCheck = request.ObjectElementCheck.Trim();
                step.ObjectElementValue = request.ObjectElementValue.Trim();
                step.Price = request.Price;
                step.UserId = int.Parse(session.UserId);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "שלב התמחור עודכן בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pricing step {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון שלב תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a pricing step
        /// </summary>
        [HttpDelete("pricing-steps/{id}")]
        public async Task<IActionResult> DeletePricingStep(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var step = await _context.SpecialNeedsPricingSteps
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (step == null)
                {
                    return NotFound(new { success = false, message = "שלב לא נמצא" });
                }

                _context.SpecialNeedsPricingSteps.Remove(step);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "שלב התמחור נמחק בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pricing step {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת שלב תמחור",
                    error = ex.Message
                });
            }
        }



        /// <summary>
        /// Get tracks for a specific year
        /// </summary>
        [HttpGet("tracks/{yearId}")]
        public async Task<IActionResult> GetTracks(int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var tracks = await _context.Tracks
                    .AsNoTracking()
                    .Where(t => t.YearId == yearId)
                    .OrderBy(t => t.TrackName)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.TrackName,
                        yearId = t.YearId,
                        externalCode = t.ExternalCode,
                        availableForClasses = t.AvailableForClasses,
                        createdAt = t.CreatedAt
                    })
                    .ToListAsync();

                return Ok(tracks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tracks for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מסלולים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new track
        /// </summary>
        [HttpPost("tracks")]
        public async Task<IActionResult> AddTrack([FromBody] AddTrackRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Check for duplicate name in the same year
                var exists = await _context.Tracks
                    .AnyAsync(t => t.YearId == request.YearId && t.TrackName == request.Name);

                if (exists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"מסלול בשם '{request.Name}' כבר קיים לשנה זו"
                    });
                }

                var track = new Track
                {
                    TrackName = request.Name,
                    YearId = request.YearId,
                    ExternalCode = request.ExternalCode,
                    AvailableForClasses = request.AvailableForClasses,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "מסלול נוסף בהצלחה",
                    id = track.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding track");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update an existing track
        /// </summary>
        [HttpPut("tracks/{id}")]
        public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var track = await _context.Tracks.FindAsync(id);
                if (track == null)
                {
                    return NotFound(new { success = false, message = "מסלול לא נמצא" });
                }

                track.TrackName = request.Name;
                track.ExternalCode = request.ExternalCode;
                track.AvailableForClasses = request.AvailableForClasses;
                track.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "מסלול עודכן בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating track {TrackId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a track
        /// </summary>
        [HttpDelete("tracks/{id}")]
        public async Task<IActionResult> DeleteTrack(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var track = await _context.Tracks.FindAsync(id);
                if (track == null)
                {
                    return NotFound(new { success = false, message = "מסלול לא נמצא" });
                }

                _context.Tracks.Remove(track);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "מסלול נמחק בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting track {TrackId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get track levels for a specific track
        /// </summary>
        [HttpGet("tracks-levels/{trackId}")]
        public async Task<IActionResult> GetTrackLevels(int trackId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var levels = await _context.TrackLevels
                    .AsNoTracking()
                    .Where(tl => tl.SchoolTrackId == trackId)
                    .OrderBy(tl => tl.LevelName)
                    .Select(tl => new
                    {
                        id = tl.Id,
                        schoolTrackId = tl.SchoolTrackId,
                        level = tl.LevelName,
                        minHours = tl.MinHours,
                        maxHours = tl.MaxHours,
                        availableForClasses = tl.AvailableForClasses
                    })
                    .ToListAsync();

                return Ok(levels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading track levels for track {TrackId}", trackId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רמות מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new track level
        /// </summary>
        [HttpPost("tracks-levels")]
        public async Task<IActionResult> AddTrackLevel([FromBody] AddTrackLevelRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var level = new TrackLevel
                {
                    SchoolTrackId = request.SchoolTrackId,
                    LevelName = request.Level,
                    MinHours = request.MinHours,
                    MaxHours = request.MaxHours,
                    AvailableForClasses = request.AvailableForClasses
                };

                _context.TrackLevels.Add(level);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "רמת מסלול נוספה בהצלחה",
                    id = level.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding track level");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת רמת מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update an existing track level
        /// </summary>
        [HttpPut("tracks-levels/{id}")]
        public async Task<IActionResult> UpdateTrackLevel(int id, [FromBody] UpdateTrackLevelRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var level = await _context.TrackLevels.FindAsync(id);
                if (level == null)
                {
                    return NotFound(new { success = false, message = "רמת מסלול לא נמצאה" });
                }

                level.LevelName = request.Level;
                level.MinHours = request.MinHours;
                level.MaxHours = request.MaxHours;
                level.AvailableForClasses = request.AvailableForClasses;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "רמת מסלול עודכנה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating track level {LevelId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון רמת מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a track level
        /// </summary>
        [HttpDelete("tracks-levels/{id}")]
        public async Task<IActionResult> DeleteTrackLevel(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var level = await _context.TrackLevels.FindAsync(id);
                if (level == null)
                {
                    return NotFound(new { success = false, message = "רמת מסלול לא נמצאה" });
                }

                _context.TrackLevels.Remove(level);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "רמת מסלול נמחקה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting track level {LevelId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת רמת מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get track pricing for a specific track level
        /// </summary>
        [HttpGet("tracks-pricing/{levelId}")]
        public async Task<IActionResult> GetTrackPricing(int levelId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var pricing = await _context.TracksPricing
                    .AsNoTracking()
                    .Where(tp => tp.LevelId == levelId)
                    .OrderBy(tp => tp.Category)
                    .Select(tp => new
                    {
                        id = tp.Id,
                        schoolTrackId = tp.SchoolTrackId,
                        price = tp.Price,
                        category = tp.Category,
                        levelId = tp.LevelId
                    })
                    .ToListAsync();

                return Ok(pricing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading track pricing for level {LevelId}", levelId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תמחור מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add new track pricing
        /// </summary>
        [HttpPost("tracks-pricing")]
        public async Task<IActionResult> AddTrackPricing([FromBody] AddTrackPricingRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var pricing = new TrackPricing
                {
                    SchoolTrackId = request.SchoolTrackId,
                    Price = request.Price,
                    Category = request.Category,
                    LevelId = request.LevelId
                };

                _context.TracksPricing.Add(pricing);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "תמחור מסלול נוסף בהצלחה",
                    id = pricing.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding track pricing");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת תמחור מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update existing track pricing
        /// </summary>
        [HttpPut("tracks-pricing/{id}")]
        public async Task<IActionResult> UpdateTrackPricing(int id, [FromBody] UpdateTrackPricingRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var pricing = await _context.TracksPricing.FindAsync(id);
                if (pricing == null)
                {
                    return NotFound(new { success = false, message = "תמחור מסלול לא נמצא" });
                }

                pricing.Price = request.Price;
                pricing.Category = request.Category;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "תמחור מסלול עודכן בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating track pricing {PricingId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון תמחור מסלול",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete track pricing
        /// </summary>
        [HttpDelete("tracks-pricing/{id}")]
        public async Task<IActionResult> DeleteTrackPricing(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var pricing = await _context.TracksPricing.FindAsync(id);
                if (pricing == null)
                {
                    return NotFound(new { success = false, message = "תמחור מסלול לא נמצא" });
                }

                _context.TracksPricing.Remove(pricing);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "תמחור מסלול נמחק בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting track pricing {PricingId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת תמחור מסלול",
                    error = ex.Message
                });
            }
        }

    

        // ==================== Export/Import Methods ====================

        /// <summary>
        /// Export all year configuration to JSON file
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportYearConfiguration([FromQuery] int yearId)
        {
            try
            {
          
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }


                _logger.LogInformation("Exporting configuration for year ID: {YearId}", yearId);

                var config = new SchoolYearConfigExport
                {
                    ExportDate = DateTime.UtcNow,
                    YearId = yearId
                };

                // Get year info
                var year = await _context.SchoolYears
                    .AsNoTracking()
                    .Where(y => y.Id == yearId)
                    .Select(y => new { y.YearName })
                    .FirstOrDefaultAsync();

                if (year == null)
                {
                    return NotFound(new { success = false, message = "שנת לימודים לא נמצאה" });
                }

                config.YearName = year.YearName;

                // Export Pricing Elements with Categories and Steps
                var pricingElements = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(pe => pe.YearId == yearId)
                    .OrderBy(pe => pe.ElementName)
                    .ToListAsync();

                foreach (var element in pricingElements)
                {
                    var exportElement = new PricingElementExport
                    {
                        Name = element.ElementName,
                        Title = element.Title,
                        Description = element.Description,
                        CalculationLevel = element.CalculationLevel,
                        AttributeToCheck = element.AttributeToCheck,
                        Categories = new List<PricingCategoryExport>()
                    };

                    // Get categories for this element
                    var categories = await _context.SpecialNeedsPricingCategories
                        .AsNoTracking()
                        .Where(pc => pc.PricingElement == element.Id)
                        .OrderBy(pc => pc.Category)
                        .ToListAsync();

                    foreach (var category in categories)
                    {
                        var exportCategory = new PricingCategoryExport
                        {
                            Category = category.Category,
                            IsLowestLevel = category.IsLowestLevel.GetValueOrDefault(),
                            Price = category.Price,
                            Steps = new List<PricingStepExport>()
                        };

                        // Get steps for this category
                        var steps = await _context.SpecialNeedsPricingSteps
                            .AsNoTracking()
                            .Where(ps => ps.PricingElement == element.Id && ps.Category == category.Category)
                            .OrderBy(ps => ps.ObjectCheck)
                            .ThenBy(ps => ps.ObjectElementCheck)
                            .ThenBy(ps => ps.ObjectElementValue)
                            .ToListAsync();

                        foreach (var step in steps)
                        {
                            exportCategory.Steps.Add(new PricingStepExport
                            {
                                ObjectCheck = step.ObjectCheck,
                                ObjectElementCheck = step.ObjectElementCheck,
                                ObjectElementValue = step.ObjectElementValue,
                                Price = step.Price
                            });
                        }

                        exportElement.Categories.Add(exportCategory);
                    }

                    config.PricingElements.Add(exportElement);
                }

                // Export Document Types
                var documentTypes = await _context.DocumentTypes
                    .AsNoTracking()
                    .Where(dt => dt.YearId == yearId)
                    .OrderBy(dt => dt.Name)
                    .ToListAsync();

                foreach (var dt in documentTypes)
                {
                    config.DocumentTypes.Add(new DocumentTypeExport
                    {
                        TypeName = dt.Name
                    });
                }

                // Export Study Programs
                var studyPrograms = await _context.AdditionalStudyProgramsPricing
                    .AsNoTracking()
                    .Where(sp => sp.YearId == yearId)
                    .OrderBy(sp => sp.Students)
                    .ToListAsync();

                foreach (var sp in studyPrograms)
                {
                    config.StudyPrograms.Add(new StudyProgramExport
                    {
                        Students = sp.Students,
                        Price = sp.Price
                    });
                }

                // Export Tracks with Levels and Pricing
                var tracks = await _context.Tracks
                    .AsNoTracking()
                    .Where(t => t.YearId == yearId)
                    .OrderBy(t => t.TrackName)
                    .ToListAsync();

                foreach (var track in tracks)
                {
                    var exportTrack = new TrackExport
                    {
                        TrackName = track.TrackName,
                        Description = null,
                        Levels = new List<TrackLevelExport>()
                    };

                    // Get levels for this track
                    var levels = await _context.TrackLevels
                        .AsNoTracking()
                        .Where(tl => tl.SchoolTrackId == track.Id)
                        .OrderBy(tl => tl.LevelName)
                        .ToListAsync();

                    foreach (var level in levels)
                    {
                        var exportLevel = new TrackLevelExport
                        {
                            LevelName = level.LevelName ?? "",
                            Description = null,
                            Pricing = new List<TrackPricingExport>()
                        };

                        // Get pricing for this level
                        var pricing = await _context.TracksPricing
                            .AsNoTracking()
                            .Where(tp => tp.SchoolTrackId == track.Id && tp.LevelId == level.Id)
                            .OrderBy(tp => tp.Category)
                            .ToListAsync();

                        foreach (var p in pricing)
                        {
                            exportLevel.Pricing.Add(new TrackPricingExport
                            {
                                Category = p.Category ?? 0,
                                Price = p.Price
                            });
                        }

                        exportTrack.Levels.Add(exportLevel);
                    }

                    config.Tracks.Add(exportTrack);
                }

                // Serialize to JSON with formatting
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(config, options);
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);

                return File(bytes, "application/json", $"school_year_config_{config.YearName}_{DateTime.Now:yyyyMMdd}.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting year configuration for year ID: {YearId}", yearId);
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא הגדרות", error = ex.Message });
            }
        }

        /// <summary>
        /// Import year configuration from JSON file
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> ImportYearConfiguration(IFormFile file, [FromForm] int yearId, [FromForm] bool clearExisting = true)
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "לא נבחר קובץ" });
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "יש להעלות קובץ JSON בלבד" });
            }

            try
            {
                _logger.LogInformation("Importing configuration for year ID: {YearId}, ClearExisting: {ClearExisting}", yearId, clearExisting);

                // Read and parse JSON
                string jsonContent;
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    jsonContent = await reader.ReadToEndAsync();
                }

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var config = System.Text.Json.JsonSerializer.Deserialize<SchoolYearConfigExport>(jsonContent, options);
                if (config == null)
                {
                    return BadRequest(new { success = false, message = "קובץ JSON לא תקין" });
                }

                var errors = new List<string>();
                var imported = new ImportResult();

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Clear existing data if requested
                    if (clearExisting)
                    {
                        await ClearYearConfiguration(yearId);
                    }

                    // Import Pricing Elements, Categories, and Steps
                    foreach (var elementExport in config.PricingElements)
                    {
                        try
                        {
                            // Check if element already exists for this year
                            var existingElement = await _context.SpecialNeedsPricingElements
                                .FirstOrDefaultAsync(e => e.ElementName == elementExport.Name && e.YearId == yearId);

                            if (existingElement != null)
                            {
                                _logger.LogInformation("Pricing element '{ElementName}' already exists for year {YearId}, skipping", elementExport.Name, yearId);
                                continue;
                            }

                            var element = new SpecialNeedsPricingElement
                            {
                                YearId = yearId,
                                ElementName = elementExport.Name,
                                Title = elementExport.Title,
                                Description = elementExport.Description,
                                CalculationLevel = elementExport.CalculationLevel,
                                AttributeToCheck = elementExport.AttributeToCheck,
                                UserId = int.Parse(session.UserId),
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.SpecialNeedsPricingElements.Add(element);
                            await _context.SaveChangesAsync();
                            imported.PricingElements++;

                            // Import categories
                            foreach (var categoryExport in elementExport.Categories)
                            {
                                var category = new SpecialNeedsPricingCategory
                                {
                                    PricingElement = element.Id,
                                    Category = categoryExport.Category,
                                    IsLowestLevel = categoryExport.IsLowestLevel,
                                    Price = categoryExport.Price
                                };

                                _context.SpecialNeedsPricingCategories.Add(category);
                                await _context.SaveChangesAsync();
                                imported.PricingCategories++;

                                // Import steps
                                foreach (var stepExport in categoryExport.Steps)
                                {
                                    var step = new SpecialNeedsPricingStep
                                    {
                                        PricingElement = element.Id,
                                        Category = category.Category,
                                        ObjectCheck = stepExport.ObjectCheck,
                                        ObjectElementCheck = stepExport.ObjectElementCheck,
                                        ObjectElementValue = stepExport.ObjectElementValue,
                                        Price = stepExport.Price
                                    };

                                    _context.SpecialNeedsPricingSteps.Add(step);
                                    imported.PricingSteps++;
                                }

                                await _context.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"שגיאה בייבוא רכיב תמחור '{elementExport.Name}': {ex.Message}");
                            _logger.LogError(ex, "Error importing pricing element: {ElementName}", elementExport.Name);
                        }
                    }

                    // Import Document Types
                    foreach (var dtExport in config.DocumentTypes)
                    {
                        try
                        {
                            // Check if document type already exists for this year
                            var existing = await _context.DocumentTypes
                                .FirstOrDefaultAsync(d => d.Name == dtExport.TypeName && d.YearId == yearId);

                            if (existing == null)
                            {
                                var documentType = new DocumentType
                                {
                                    Name = dtExport.TypeName,
                                    YearId = yearId,
                                    Level = "Year"
                                };

                                _context.DocumentTypes.Add(documentType);
                                await _context.SaveChangesAsync();
                                imported.DocumentTypes++;
                            }
                            else
                            {
                                _logger.LogInformation("Document type '{TypeName}' already exists for year {YearId}, skipping", dtExport.TypeName, yearId);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"שגיאה בייבוא סוג מסמך '{dtExport.TypeName}': {ex.Message}");
                            _logger.LogError(ex, "Error importing document type: {TypeName}", dtExport.TypeName);
                        }
                    }

                    // Import Study Programs
                    foreach (var spExport in config.StudyPrograms)
                    {
                        try
                        {
                            var studyProgram = new AdditionalStudyProgramsPricing
                            {
                                YearId = yearId,
                                Students = spExport.Students,
                                Price = spExport.Price,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.AdditionalStudyProgramsPricing.Add(studyProgram);
                            imported.StudyPrograms++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"שגיאה בייבוא תוכנית לימוד עם {spExport.Students} תלמידים: {ex.Message}");
                            _logger.LogError(ex, "Error importing study program: {Students}", spExport.Students);
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Import Tracks with Levels and Pricing
                    foreach (var trackExport in config.Tracks)
                    {
                        try
                        {
                            var track = new Track
                            {
                                YearId = yearId,
                                TrackName = trackExport.TrackName,
                                ExternalCode = trackExport.ExternalCode,
                                AvailableForClasses = trackExport.AvailableForClasses,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.Tracks.Add(track);
                            await _context.SaveChangesAsync();
                            imported.Tracks++;

                            // Import levels
                            foreach (var levelExport in trackExport.Levels)
                            {
                                var level = new TrackLevel
                                {
                                    SchoolTrackId = track.Id,
                                    LevelName = levelExport.LevelName,
                                    MinHours = levelExport.MinHours,
                                    MaxHours = levelExport.MaxHours,
                                    AvailableForClasses = levelExport.AvailableForClasses
                                };

                                _context.TrackLevels.Add(level);
                                await _context.SaveChangesAsync();
                                imported.TrackLevels++;

                                // Import pricing
                                foreach (var pricingExport in levelExport.Pricing)
                                {
                                    var pricing = new TrackPricing
                                    {
                                        SchoolTrackId = track.Id,
                                        LevelId = level.Id,
                                        Category = pricingExport.Category,
                                        Price = pricingExport.Price
                                    };

                                    _context.TracksPricing.Add(pricing);
                                    imported.TrackPricing++;
                                }

                                await _context.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"שגיאה בייבוא מגמה '{trackExport.TrackName}': {ex.Message}");
                            _logger.LogError(ex, "Error importing track: {TrackName}", trackExport.TrackName);
                        }
                    }

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "ייבוא הושלם בהצלחה",
                        imported = imported,
                        errors = errors.Count > 0 ? errors : null
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during import");
                    return StatusCode(500, new { success = false, message = "שגיאה בייבוא - השינויים בוטלו", error = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing year configuration");
                return StatusCode(500, new { success = false, message = "שגיאה בייבוא הגדרות", error = ex.Message });
            }
        }

        /// <summary>
        /// Clear all configuration for a year
        /// </summary>
        private async Task ClearYearConfiguration(int yearId)
        {
            _logger.LogInformation("Clearing existing configuration for year ID: {YearId}", yearId);

            // Get all pricing elements for this year
            var elementIds = await _context.SpecialNeedsPricingElements
                .Where(pe => pe.YearId == yearId)
                .Select(pe => pe.Id)
                .ToListAsync();

            if (elementIds.Any())
            {
                // Get all category IDs
                var categoryIds = await _context.SpecialNeedsPricingCategories
                    .Where(pc => elementIds.Contains(pc.PricingElement))
                    .Select(pc => pc.Id)
                    .ToListAsync();

                if (categoryIds.Any())
                {
                    // Delete pricing steps
                    var steps = await _context.SpecialNeedsPricingSteps
                        .Where(ps => elementIds.Contains(ps.PricingElement))
                        .ToListAsync();
                    _context.SpecialNeedsPricingSteps.RemoveRange(steps);

                    // Delete pricing categories
                    var categories = await _context.SpecialNeedsPricingCategories
                        .Where(pc => categoryIds.Contains(pc.Id))
                        .ToListAsync();
                    _context.SpecialNeedsPricingCategories.RemoveRange(categories);
                }

                // Delete pricing elements
                var elements = await _context.SpecialNeedsPricingElements
                    .Where(pe => elementIds.Contains(pe.Id))
                    .ToListAsync();
                _context.SpecialNeedsPricingElements.RemoveRange(elements);
            }

            // Delete document types
            var documentTypes = await _context.DocumentTypes
                .Where(dt => dt.YearId == yearId)
                .ToListAsync();
            _context.DocumentTypes.RemoveRange(documentTypes);

            // Delete study programs
            var studyPrograms = await _context.AdditionalStudyProgramsPricing
                .Where(sp => sp.YearId == yearId)
                .ToListAsync();
            _context.AdditionalStudyProgramsPricing.RemoveRange(studyPrograms);

            // Get all tracks for this year
            var trackIds = await _context.Tracks
                .Where(t => t.YearId == yearId)
                .Select(t => t.Id)
                .ToListAsync();

            if (trackIds.Any())
            {
                // Get all level IDs
                var levelIds = await _context.TrackLevels
                    .Where(tl => trackIds.Contains(tl.SchoolTrackId))
                    .Select(tl => tl.Id)
                    .ToListAsync();

                if (levelIds.Any())
                {
                    // Delete track pricing
                    var trackPricing = await _context.TracksPricing
                        .Where(tp => levelIds.Contains(tp.LevelId.Value))
                        .ToListAsync();
                    _context.TracksPricing.RemoveRange(trackPricing);

                    // Delete track levels
                    var trackLevels = await _context.TrackLevels
                        .Where(tl => levelIds.Contains(tl.Id))
                        .ToListAsync();
                    _context.TrackLevels.RemoveRange(trackLevels);
                }

                // Delete tracks
                var tracks = await _context.Tracks
                    .Where(t => trackIds.Contains(t.Id))
                    .ToListAsync();
                _context.Tracks.RemoveRange(tracks);
            }

            await _context.SaveChangesAsync();
        }

        // ==================== Study Programs Endpoints ====================

        /// <summary>
        /// Get additional study programs for a specific year
        /// </summary>
        [HttpGet("study-programs")]
        public async Task<IActionResult> GetStudyPrograms([FromQuery] int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Fetching study programs pricing for yearId={YearId}", yearId);

                var programs = await _context.AdditionalStudyProgramsPricing
                    .AsNoTracking()
                    .Where(p => p.YearId == yearId)
                    .OrderBy(p => p.Students)
                    .Select(p => new
                    {
                        id = p.Id,
                        students = p.Students,
                        price = p.Price,
                        createdAt = p.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} study programs pricing entries for yearId={YearId}", programs.Count, yearId);

                return Ok(programs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading study programs for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תוכניות לימוד",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new study program
        /// </summary>
        [HttpPost("study-programs")]
        public async Task<IActionResult> AddStudyProgram([FromBody] AddStudyProgramRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (request.Students <= 0)
                {
                    return BadRequest(new { success = false, message = "מספר תלמידים חייב להיות גדול מאפס" });
                }

                var program = new AdditionalStudyProgramsPricing
                {
                    YearId = request.YearId,
                    Students = request.Students,
                    Price = request.Price,
                    UserId = int.Parse(session.UserId),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AdditionalStudyProgramsPricing.Add(program);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Study program pricing added for year {YearId} with {Students} students", request.YearId, request.Students);

                return Ok(new
                {
                    success = true,
                    message = "תוכנית הלימודים נוספה בהצלחה",
                    id = program.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding study program");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בהוספת תוכנית לימודים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update an existing study program
        /// </summary>
        [HttpPut("study-programs/{id}")]
        public async Task<IActionResult> UpdateStudyProgram(int id, [FromBody] UpdateStudyProgramRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var program = await _context.AdditionalStudyProgramsPricing
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (program == null)
                {
                    return NotFound(new { success = false, message = "רשומת תמחור לא נמצאה" });
                }

                if (request.Students <= 0)
                {
                    return BadRequest(new { success = false, message = "מספר תלמידים חייב להיות גדול מאפס" });
                }

                program.Students = request.Students;
                program.Price = request.Price;
                program.UserId = int.Parse(session.UserId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Study program {Id} updated", id);

                return Ok(new
                {
                    success = true,
                    message = "תוכנית הלימודים עודכנה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating study program {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון תוכנית לימודים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a study program
        /// </summary>
        [HttpDelete("study-programs/{id}")]
        public async Task<IActionResult> DeleteStudyProgram(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var program = await _context.AdditionalStudyProgramsPricing
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (program == null)
                {
                    return NotFound(new { success = false, message = "רשומת תמחור לא נמצאה" });
                }

                _context.AdditionalStudyProgramsPricing.Remove(program);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Study program {Id} deleted", id);

                return Ok(new
                {
                    success = true,
                    message = "תוכנית הלימודים נמחקה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting study program {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת תוכנית לימודים",
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
        public int? SortOrder { get; set; }
        public string? CalculationLevel { get; set; }
        public string? AttributeToCheck { get; set; }
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

    public class AddPricingStepRequest
    {
        public int PricingElement { get; set; }
        public int Category { get; set; }
        public string ObjectCheck { get; set; } = string.Empty;
        public string ObjectElementCheck { get; set; } = string.Empty;
        public string ObjectElementValue { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    public class UpdatePricingStepRequest
    {
        public string ObjectCheck { get; set; } = string.Empty;
        public string ObjectElementCheck { get; set; } = string.Empty;
        public string ObjectElementValue { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    // Tracks DTOs
    public class AddTrackRequest
    {
        public int YearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class UpdateTrackRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class AddTrackLevelRequest
    {
        public int SchoolTrackId { get; set; }
        public string? Level { get; set; }
        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class UpdateTrackLevelRequest
    {
        public string? Level { get; set; }
        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class AddTrackPricingRequest
    {
        public int SchoolTrackId { get; set; }
        public decimal? Price { get; set; }
        public int? Category { get; set; }
        public int? LevelId { get; set; }
    }

    public class UpdateTrackPricingRequest
    {
        public decimal? Price { get; set; }
        public int? Category { get; set; }
    }

    // Study Programs Pricing DTOs
    public class AddStudyProgramRequest
    {
        public int YearId { get; set; }
        public int Students { get; set; }
        public decimal? Price { get; set; }
    }

    public class UpdateStudyProgramRequest
    {
        public int Students { get; set; }
        public decimal? Price { get; set; }
    }
}