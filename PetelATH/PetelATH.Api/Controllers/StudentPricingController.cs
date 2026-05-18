using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Session;
using PetelATH.Api.Services;

namespace PetelATH.Api.Controllers
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

                // Block pricing for students with "no external permit" status
                var studentStatusId = await _context.SchoolStudents
                    .Where(s => s.Id == schoolStudentId)
                    .Select(s => s.StatusId)
                    .FirstOrDefaultAsync();

                if (studentStatusId == 7)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "לא ניתן לחשב תמחור - תלמיד ללא אישור לימודי חוץ"
                    });
                }

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
                    // ✅ Check if no changes were detected
                    if (result.NoChangeDetected)
                    {
                        _logger.LogInformation("ℹ️ No changes detected - skipping save for student {StudentId}", schoolStudentId);
                        
                        return Ok(new
                        {
                            success = true,
                            message = "חישוב התמחור הושלם - לא נמצאו שינויים",
                            data = new
                            {
                                schoolStudentId = result.SchoolStudentId,
                                newStudentId = (int?)null,
                                noChangeDetected = true,
                                elementsCount = result.CalculatedElements.Count,
                                totalPrice = result.CalculatedElements.Sum(e => e.Price),
                                elements = result.CalculatedElements,
                                warnings = result.Errors
                            }
                        });
                    }

                    // ✅ Changes detected - check if new version was created
                    if (!result.NewStudentId.HasValue)
                    {
                        return StatusCode(500, new
                        {
                            success = false,
                            message = "נכשל ביצירת גרסת תלמיד חדשה"
                        });
                    }

                    var saved = await _pricingService.SavePricingElements(
                        result.NewStudentId.Value,  // ✅ Use NEW version ID
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
                        newStudentId = result.NewStudentId,
                        noChangeDetected = result.NoChangeDetected,
                        enrollmentMonths = result.EnrollmentMonths,
                        elementsCount = result.CalculatedElements.Count,
                        totalFullPrice = result.CalculatedElements.Sum(e => e.FullPrice),
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

                 // ✅ Get student information to retrieve cost and dates
                var student = await _context.SchoolStudents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == schoolStudentId);

                if (student == null)
                {
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });
                }

                
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
                            spe.FullPrice,
                            spe.Price,
                            spe.DeterminingFactor,       
                            spe.Hours,     
                            SortOrder = snpe.SortOrder
                        })
                    .OrderBy(pe => pe.SortOrder)
                    .ThenBy(pe => pe.PricingElementName)
                    .ToListAsync();

                _logger.LogInformation("✅ Found {Count} pricing elements for student {StudentId}", 
                    pricingElements.Count, schoolStudentId);

                // ✅ Calculate enrollment months (prefer stored value, fallback to calculate)
                int? enrollmentMonths = student.EnrollmentMonths
                    ?? (student.StartDate.HasValue && student.EndDate.HasValue
                        ? CalculateEnrollmentMonthsForDisplay(student.StartDate.Value, student.EndDate.Value)
                        : (int?)null);

                // ✅ Calculate sum of pricing elements
                var elementsTotal = pricingElements.Sum(pe => pe.Price);
                var elementsTotalFull = pricingElements.Sum(pe => pe.FullPrice);

                return Ok(new
                {
                    success = true,
                    data = pricingElements,
                    summary = new
                    {
                        elementsTotalFull = elementsTotalFull,       // ✅ Sum of full annual prices
                        elementsTotal = elementsTotal,               // ✅ Sum of prorated prices
                        studentCost = student.Cost ?? 0,             // ✅ Final cost from student record
                        enrollmentMonths = enrollmentMonths,         // ✅ Months enrolled
                        startDate = student.StartDate,
                        endDate = student.EndDate
                    }
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
        /// Calculate enrollment months for display (same logic as StudentPricingService)
        /// </summary>
        private int CalculateEnrollmentMonthsForDisplay(DateOnly startDate, DateOnly endDate)
        {
            if (endDate < startDate)
            {
                return 0;
            }

            // Check if full year
            if (startDate.Month == 9 && startDate.Day == 1 && 
                endDate.Month == 8 && endDate.Day == 31)
            {
                return 12;
            }

            // Adjust start date: if before 16th, use 1st of that month, otherwise 1st of next month
            var effectiveStartDate = startDate.Day < 16
                ? new DateOnly(startDate.Year, startDate.Month, 1)
                : new DateOnly(startDate.Year, startDate.Month, 1).AddMonths(1);

            // Adjust end date: if after 15th, use last day of that month, otherwise last day of previous month
            var effectiveEndDate = endDate.Day > 15
                ? new DateOnly(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month))
                : new DateOnly(endDate.Year, endDate.Month, 1).AddDays(-1);

            // If effective end is before effective start, no full months qualify
            if (effectiveEndDate < effectiveStartDate)
            {
                return 0;
            }

            // Calculate the number of full months
            int months = 0;
            var current = effectiveStartDate;

            while (current <= effectiveEndDate)
            {
                months++;
                current = current.AddMonths(1);
            }

            return months;
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

                // Get all students for this school year, excluding those with "no external permit" status
                var students = await _context.SchoolStudents
                    .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion && s.StatusId != 7)
                    .Select(s => s.Id)
                    .ToListAsync();

                var skippedCount = await _context.SchoolStudents
                    .CountAsync(s => s.SchoolYearId == schoolYearId && s.IsLastVersion && s.StatusId == 7);

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var studentId in students)
                {
                    var result = await _pricingService.CalculateStudentPricing(studentId);
                    
                    if (result.Success && save && result.NewStudentId.HasValue)
                    {
                                await _pricingService.SavePricingElements(
                                    result.NewStudentId.Value,  // ✅ Use NEW version ID
                                    result.CalculatedElements);
                    }

                    results.Add(new
                    {
                        studentId,
                        newStudentId = result.NewStudentId,
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
                        skippedCount,
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

        /// <summary>
        /// Calculate pricing for all students across multiple schools (by schoolYearId list)
        /// </summary>
        [HttpPost("calculate-multiple-schools")]
        public async Task<IActionResult> CalculateMultipleSchoolsPricing(
            [FromBody] MultipleSchoolsPricingRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (request.SchoolYearIds == null || request.SchoolYearIds.Count == 0)
                    return BadRequest(new { success = false, message = "לא נבחרו בתי ספר" });

                _logger.LogInformation("🏫 Starting bulk pricing for {Count} schools (save={Save})",
                    request.SchoolYearIds.Count, request.Save);

                var schoolSummaries = new List<object>();
                int totalSuccess = 0, totalFail = 0, totalSkipped = 0;

                foreach (var schoolYearId in request.SchoolYearIds)
                {
                    var students = await _context.SchoolStudents
                        .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion && s.StatusId != 7)
                        .Select(s => s.Id)
                        .ToListAsync();

                    var skipped = await _context.SchoolStudents
                        .CountAsync(s => s.SchoolYearId == schoolYearId && s.IsLastVersion && s.StatusId == 7);

                    int successCount = 0, failCount = 0;

                    foreach (var studentId in students)
                    {
                        var result = await _pricingService.CalculateStudentPricing(studentId);

                        if (result.Success && request.Save && result.NewStudentId.HasValue)
                        {
                            await _pricingService.SavePricingElements(
                                result.NewStudentId.Value,
                                result.CalculatedElements);
                        }

                        if (result.Success) successCount++; else failCount++;
                    }

                    totalSuccess += successCount;
                    totalFail += failCount;
                    totalSkipped += skipped;

                    schoolSummaries.Add(new
                    {
                        schoolYearId,
                        totalStudents = students.Count,
                        successCount,
                        failCount,
                        skippedCount = skipped
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"חישוב תמחור הושלם: {totalSuccess} הצליחו, {totalFail} נכשלו, {totalSkipped} דולגו",
                    data = new
                    {
                        totalSuccess,
                        totalFail,
                        totalSkipped,
                        saved = request.Save,
                        schools = schoolSummaries
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating pricing for multiple schools");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בחישוב תמחור לבתי הספר"
                });
            }
        }
    }

    public class MultipleSchoolsPricingRequest
    {
        public List<int> SchoolYearIds { get; set; } = new();
        public bool Save { get; set; } = true;
    }
}