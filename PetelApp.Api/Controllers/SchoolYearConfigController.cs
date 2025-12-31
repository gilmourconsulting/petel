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

        #region Tracks Management

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

        #endregion
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
}