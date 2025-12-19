using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentPricingController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly StudentPricingService _pricingService;

        public StudentPricingController(
            AppDbContext context,
            StudentPricingService pricingService,
            UserSessionService userSessionService,
            ILogger<StudentPricingController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _pricingService = pricingService;
        }

        /// <summary>
        /// Calculate pricing elements for a student
        /// </summary>
        [HttpPost("calculate/{schoolStudentId}")]
        public async Task<IActionResult> CalculateStudentPricing(int schoolStudentId, [FromQuery] bool save = false)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("🔢 Starting pricing calculation for student: {StudentId} (Save: {Save})", 
                    schoolStudentId, save);

                // Calculate pricing
                var result = await _pricingService.CalculateStudentPricing(schoolStudentId);

                if (!result.Success && result.CalculatedElements.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "לא ניתן לחשב תמחור עבור תלמיד זה",
                        errors = result.Errors
                    });
                }

                // Save if requested
                if (save && result.CalculatedElements.Count > 0)
                {
                    var saved = await _pricingService.SavePricingElements(
                        schoolStudentId, 
                        result.CalculatedElements);

                    if (!saved)
                    {
                        return StatusCode(500, new
                        {
                            success = false,
                            message = "חישוב התמחור הצליח אך השמירה נכשלה"
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = save ? "תמחור חושב ונשמר בהצלחה" : "תמחור חושב בהצלחה",
                    data = new
                    {
                        schoolStudentId = result.SchoolStudentId,
                        elementsCount = result.CalculatedElements.Count,
                        totalPrice = result.CalculatedElements.Sum(e => e.Price),
                        elements = result.CalculatedElements,
                        warnings = result.Errors
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating pricing for student {StudentId}", schoolStudentId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בחישוב תמחור",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get existing pricing elements for a student
        /// </summary>
        [HttpGet("{schoolStudentId}")]
        public async Task<IActionResult> GetStudentPricingElements(int schoolStudentId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("📊 Fetching pricing elements for student: {StudentId}", schoolStudentId);

                var pricingElements = await _context.SchoolStudentPricingElements
                    .Where(pe => pe.StudentId == schoolStudentId)
                    .Join(
                        _context.SpecialNeedsPricingElements,
                        spe => spe.PricingElementId,
                        snpe => snpe.Id,
                        (spe, snpe) => new
                        {
                            spe.Id,
                            spe.StudentId,
                            spe.PricingElementId,
                            PricingElementName = snpe.ElementName,
                            spe.Price
                        })
                    .OrderBy(pe => pe.PricingElementName)
                    .ToListAsync();

                _logger.LogInformation("✅ Found {Count} pricing elements for student {StudentId}", 
                    pricingElements.Count, schoolStudentId);

                return Ok(new
                {
                    success = true,
                    data = pricingElements,
                    totalPrice = pricingElements.Sum(pe => pe.Price)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching pricing elements for student {StudentId}", schoolStudentId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת נתוני תמחור"
                });
            }
        }

        /// <summary>
        /// Calculate pricing for all students in a school year
        /// </summary>
        [HttpPost("calculate-school/{schoolYearId}")]
        public async Task<IActionResult> CalculateSchoolPricing(int schoolYearId, [FromQuery] bool save = false)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("🏫 Starting bulk pricing calculation for school year: {SchoolYearId}", 
                    schoolYearId);

                // Get all students for this school year
                var students = await _context.SchoolStudents
                    .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion)
                    .Select(s => s.Id)
                    .ToListAsync();

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var studentId in students)
                {
                    var result = await _pricingService.CalculateStudentPricing(studentId);
                    
                    if (result.Success && save)
                    {
                        await _pricingService.SavePricingElements(studentId, result.CalculatedElements);
                    }

                    results.Add(new
                    {
                        studentId,
                        success = result.Success,
                        elementsCount = result.CalculatedElements.Count,
                        totalPrice = result.CalculatedElements.Sum(e => e.Price),
                        errors = result.Errors
                    });

                    if (result.Success)
                        successCount++;
                    else
                        failCount++;
                }

                return Ok(new
                {
                    success = true,
                    message = $"חישוב תמחור הושלם: {successCount} הצליחו, {failCount} נכשלו",
                    data = new
                    {
                        totalStudents = students.Count,
                        successCount,
                        failCount,
                        saved = save,
                        results
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating school pricing for year {SchoolYearId}", schoolYearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בחישוב תמחור לבית הספר"
                });
            }
        }
    }
}