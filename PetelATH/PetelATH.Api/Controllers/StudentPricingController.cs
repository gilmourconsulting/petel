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
                        await _pricingService.PriceRelatedCouncilPeriodsAsync(schoolStudentId);

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

                    // ✅ Changes detected - check if new version was created or saved in place
                    if (!result.NewStudentId.HasValue)
                    {
                        return StatusCode(500, new
                        {
                            success = false,
                            message = result.InPlaceSave
                                ? "נכשל בשמירת תמחור על גרסת התלמיד"
                                : "נכשל ביצירת גרסת תלמיד חדשה"
                        });
                    }

                    var saved = await _pricingService.ReplacePricingElements(
                            result.NewStudentId.Value,
                            result.CalculatedElements);

                    if (!saved)
                    {
                        return StatusCode(500, new
                        {
                            success = false,
                            message = "חישוב התמחור הצליח אך השמירה נכשלה"
                        });
                    }

                    await _pricingService.PriceRelatedCouncilPeriodsAsync(schoolStudentId);
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

                var student = await _context.SchoolStudents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == schoolStudentId);

                if (student == null)
                {
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });
                }

                var billableStudents = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => s.MasterStudentId == student.MasterStudentId
                        && s.SchoolYearId == student.SchoolYearId
                        && (s.IsLastVersion || s.IncludeInCouncilSummary))
                    .ToListAsync();

                var lastVersion = billableStudents.FirstOrDefault(s => s.IsLastVersion) ?? student;
                var lastVersionId = lastVersion.Id;

                var billableIds = billableStudents.Select(s => s.Id).Distinct().ToList();
                if (!billableIds.Contains(schoolStudentId))
                    billableIds.Add(schoolStudentId);

                var councilIds = billableStudents
                    .Where(s => s.SendingCouncil.HasValue)
                    .Select(s => s.SendingCouncil!.Value)
                    .Distinct()
                    .ToList();

                var councilNames = councilIds.Count == 0
                    ? new Dictionary<int, string>()
                    : await _context.Councils
                        .AsNoTracking()
                        .Where(c => councilIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, c => c.Name);

                var allElements = await _context.SchoolStudentPricingElements
                    .Where(pe => billableIds.Contains(pe.StudentId))
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

                var elementsByStudent = allElements
                    .GroupBy(e => e.StudentId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var lastVersionElements = elementsByStudent.TryGetValue(lastVersionId, out var lastEls)
                    ? lastEls
                    : allElements.Where(_ => false).ToList();

                int? enrollmentMonths = lastVersion.EnrollmentMonths
                    ?? (lastVersion.StartDate.HasValue && lastVersion.EndDate.HasValue
                        ? CalculateEnrollmentMonthsForDisplay(lastVersion.StartDate.Value, lastVersion.EndDate.Value)
                        : (int?)null);

                var elementsTotal = lastVersionElements.Sum(pe => pe.Price);
                var elementsTotalFull = lastVersionElements.Sum(pe => pe.FullPrice);

                var periods = billableStudents
                    .OrderByDescending(s => s.IsLastVersion)
                    .ThenBy(s => s.StartDate)
                    .Select(s =>
                    {
                        var periodElements = elementsByStudent.TryGetValue(s.Id, out var els)
                            ? els
                            : allElements.Where(_ => false).ToList();
                        int? months = s.EnrollmentMonths
                            ?? (s.StartDate.HasValue && s.EndDate.HasValue
                                ? CalculateEnrollmentMonthsForDisplay(s.StartDate.Value, s.EndDate.Value)
                                : (int?)null);
                        string? councilName = s.SendingCouncil.HasValue
                            && councilNames.TryGetValue(s.SendingCouncil.Value, out var name)
                            ? name
                            : null;
                        return new
                        {
                            studentId = s.Id,
                            councilId = s.SendingCouncil,
                            councilName,
                            startDate = s.StartDate,
                            endDate = s.EndDate,
                            enrollmentMonths = months,
                            cost = s.Cost ?? 0m,
                            isLastVersion = s.IsLastVersion,
                            elementsTotal = periodElements.Sum(pe => pe.Price),
                            elements = periodElements
                        };
                    })
                    .ToList();

                _logger.LogInformation("✅ Found {Count} pricing elements for last version {StudentId}; {PeriodCount} billable periods",
                    lastVersionElements.Count, lastVersionId, periods.Count);

                return Ok(new
                {
                    success = true,
                    data = lastVersionElements,
                    summary = new
                    {
                        elementsTotalFull = elementsTotalFull,
                        elementsTotal = elementsTotal,
                        studentCost = lastVersion.Cost ?? 0,
                        enrollmentMonths = enrollmentMonths,
                        startDate = lastVersion.StartDate,
                        endDate = lastVersion.EndDate
                    },
                    periods,
                    totals = new
                    {
                        totalCost = periods.Sum(p => p.cost),
                        elementsTotal = periods.Sum(p => p.elementsTotal)
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
                    .Where(s => s.SchoolYearId == schoolYearId
                        && s.StatusId != 7
                        && (s.IsLastVersion || s.IncludeInCouncilSummary))
                    .Select(s => s.Id)
                    .ToListAsync();

                var skippedCount = await _context.SchoolStudents
                    .CountAsync(s => s.SchoolYearId == schoolYearId
                        && (s.IsLastVersion || s.IncludeInCouncilSummary)
                        && s.StatusId == 7);

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var studentId in students)
                {
                    var result = await _pricingService.CalculateStudentPricing(studentId);

                    if (result.Success && save && result.NewStudentId.HasValue)
                    {
                        await _pricingService.ReplacePricingElements(
                            result.NewStudentId.Value,
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
                        .Where(s => s.SchoolYearId == schoolYearId
                            && s.StatusId != 7
                            && (s.IsLastVersion || s.IncludeInCouncilSummary))
                        .Select(s => s.Id)
                        .ToListAsync();

                    var skipped = await _context.SchoolStudents
                        .CountAsync(s => s.SchoolYearId == schoolYearId
                            && (s.IsLastVersion || s.IncludeInCouncilSummary)
                            && s.StatusId == 7);

                    int successCount = 0, failCount = 0;

                    foreach (var studentId in students)
                    {
                        var result = await _pricingService.CalculateStudentPricing(studentId);

                        if (result.Success && request.Save && result.NewStudentId.HasValue)
                        {
                            await _pricingService.ReplacePricingElements(
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