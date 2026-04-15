// PetelATH.Api/Services/StudentPricingService.cs
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    public class StudentPricingService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StudentPricingService> _logger;
        private readonly StudentService _studentService;

        public StudentPricingService(
            AppDbContext context,
            ILogger<StudentPricingService> logger,
            StudentService studentService)
        {
            _context = context;
            _logger = logger;
            _studentService = studentService;
        }

        /// <summary>
        /// Retrieve current pricing elements for a student
        /// </summary>
        private async Task<List<CalculatedPricingElement>> GetCurrentPricingElements(int schoolStudentId)
        {
            try
            {
                var currentElements = await _context.SchoolStudentPricingElements
                    .AsNoTracking()
                    .Where(pe => pe.StudentId == schoolStudentId)
                    .Join(
                        _context.SpecialNeedsPricingElements,
                        pe => pe.PricingElementId,
                        spe => spe.Id,
                        (pe, spe) => new CalculatedPricingElement
                        {
                            PricingElementId = pe.PricingElementId,
                            PricingElementName = spe.ElementName,
                            Price = pe.Price,
                            DisabilityCategory = 0, // Not needed for comparison
                            DeterminingFactor = pe.DeterminingFactor,
                            Hours = pe.Hours
                        })
                    .OrderBy(e => e.PricingElementId)
                    .ThenBy(e => e.DeterminingFactor)
                    .ToListAsync();

                _logger.LogDebug("📋 Retrieved {Count} current pricing elements for student {StudentId}",
                    currentElements.Count, schoolStudentId);

                return currentElements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving current pricing elements for student {StudentId}", schoolStudentId);
                return new List<CalculatedPricingElement>();
            }
        }

        /// <summary>
        /// Compare two lists of pricing elements to check if they are identical
        /// </summary>
        private bool ArePricingElementsEqual(
            List<CalculatedPricingElement> current,
            List<CalculatedPricingElement> calculated)
        {
            if (current.Count != calculated.Count)
            {
                _logger.LogDebug("Element count mismatch: Current={CurrentCount}, Calculated={CalculatedCount}",
                    current.Count, calculated.Count);
                return false;
            }

            // Sort both lists for comparison (by element ID, then determining factor)
            var sortedCurrent = current
                .OrderBy(e => e.PricingElementId)
                .ThenBy(e => e.DeterminingFactor ?? "")
                .ToList();

            var sortedCalculated = calculated
                .OrderBy(e => e.PricingElementId)
                .ThenBy(e => e.DeterminingFactor ?? "")
                .ToList();

            // Compare each element
            for (int i = 0; i < sortedCurrent.Count; i++)
            {
                var curr = sortedCurrent[i];
                var calc = sortedCalculated[i];

                if (curr.PricingElementId != calc.PricingElementId ||
                    curr.Price != calc.Price ||
                    curr.DeterminingFactor != calc.DeterminingFactor ||
                    curr.Hours != calc.Hours)
                {
                    _logger.LogDebug("Element difference detected at index {Index}: " +
                        "Current=[Id:{CurrId}, Price:{CurrPrice:C}, Factor:{CurrFactor}, Hours:{CurrHours}], " +
                        "Calculated=[Id:{CalcId}, Price:{CalcPrice:C}, Factor:{CalcFactor}, Hours:{CalcHours}]",
                        i,
                        curr.PricingElementId, curr.Price, curr.DeterminingFactor, curr.Hours,
                        calc.PricingElementId, calc.Price, calc.DeterminingFactor, calc.Hours);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calculate pricing elements for a student
        /// </summary>
        public async Task<PricingCalculationResult> CalculateStudentPricing(int schoolStudentId)
        {
            var result = new PricingCalculationResult
            {
                SchoolStudentId = schoolStudentId,
                CalculatedElements = new List<CalculatedPricingElement>(),
                Errors = new List<string>()
            };

            try
            {
                // Step 1: Retrieve current pricing elements (before calculation)
                _logger.LogInformation("📊 Starting pricing calculation for student ID: {StudentId}", schoolStudentId);
                var currentPricingElements = await GetCurrentPricingElements(schoolStudentId);
                decimal currentTotalCost = currentPricingElements.Sum(e => e.Price);

                // Step 2: Get student record
                var student = await _context.SchoolStudents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == schoolStudentId);

                if (student == null)
                {
                    result.Errors.Add($"Student with ID {schoolStudentId} not found");
                    return result;
                }

                if (!student.DisabilityCategory.HasValue)
                {
                    result.Errors.Add($"Student {schoolStudentId} has no disability category assigned");
                    return result;
                }

                int disabilityCategory = student.DisabilityCategory.Value;
                _logger.LogInformation("✅ Student found. Disability category: {Category}", disabilityCategory);

                // Step 3: Get school year to find year_id
                var schoolYear = await _context.SchoolYears
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sy => sy.Id == student.SchoolYearId);

                if (schoolYear == null)
                {
                    result.Errors.Add($"School year with ID {student.SchoolYearId} not found");
                    return result;
                }

                int yearId = schoolYear.YearId;
                _logger.LogInformation("✅ School year found. Year ID: {YearId}", yearId);

                // Step 4: Get school details for attribute lookups
                var school = await _context.Schools
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == student.SchoolYearId && s.IsLastVersion)
                    .FirstOrDefaultAsync();

                if (school == null)
                {
                    result.Errors.Add($"School not found for school year {student.SchoolYearId}");
                    return result;
                }

                // Get school attributes for step-based pricing and validation
                var schoolAttributes = await _context.SchoolAttributes
                    .AsNoTracking()
                    .Where(sa => sa.SchoolYearId == student.SchoolYearId && sa.IsLastVersion)
                    .Include(sa => sa.SchoolAttributeType)
                    .ToListAsync();

                _logger.LogInformation("✅ School found with {AttributeCount} attributes", schoolAttributes.Count);

                // Step 5: Get all pricing elements for this year (sorted by sort_order)
                var pricingElements = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(pe => pe.YearId == yearId)
                    .OrderBy(pe => pe.SortOrder)
                    .ThenBy(pe => pe.ElementName)
                    .ToListAsync();

                _logger.LogInformation("📋 Found {Count} pricing elements for year {YearId}",
                    pricingElements.Count, yearId);

                // Step 6: Process each pricing element
                foreach (var element in pricingElements)
                {
                    try
                    {
                        // ✅ REQUIREMENT 1: Check attribute_to_check before calculation
                        if (!string.IsNullOrWhiteSpace(element.AttributeToCheck))
                        {
                            var shouldSkip = await ShouldSkipElementDueToAttribute(
                                element.AttributeToCheck,
                                schoolAttributes);

                            if (shouldSkip)
                            {
                                // ✅ REQUIREMENT 2: Only log, don't add to errors (not shown to user)
                                _logger.LogDebug("⏭️ Skipping element '{Name}' - attribute '{Attribute}' check failed",
                                    element.ElementName, element.AttributeToCheck);
                                continue;
                            }
                        }

                        // ✅ Special handling for "Tracks" elements - may return multiple items
                        if (element.Title?.Contains("Tracks", StringComparison.OrdinalIgnoreCase) == true ||
                            element.Title?.Contains("מגמות", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (student.ClassId > 0 && student.ClassId.HasValue)
                            {
                                var trackElements = await CalculateAllTrackPrices(
                                    element,
                                    disabilityCategory,
                                    student.ClassId.Value);

                                if (trackElements.Count > 0)
                                {
                                    result.CalculatedElements.AddRange(trackElements);
                                    _logger.LogInformation("✅ Added {Count} track prices for element '{Name}'",
                                        trackElements.Count, element.ElementName);
                                }
                                else
                                {
                                    _logger.LogDebug("⏭️ No track pricing found for element '{Name}'", element.ElementName);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Student has no class assigned, cannot calculate track pricing");
                            }
                            continue; // Move to next element
                        }


                        
                                                // ✅ Special handling for "Additional Studies" elements
                                                if (element.Title?.Contains("Additional Studies", StringComparison.OrdinalIgnoreCase) == true ||
                                                    element.Title?.Contains("תל\"ן", StringComparison.OrdinalIgnoreCase) == true ||
                                                    element.ElementName?.Contains("Additional Studies", StringComparison.OrdinalIgnoreCase) == true)
                                                {
                                                    if (student.ClassId.HasValue)
                                                    {
                                                        var additionalStudiesElement = await CalculateAdditionalStudiesPrice(
                                                            element,
                                                            disabilityCategory,
                                                            student.ClassId.Value,
                                                            student.SchoolYearId);
                        
                                                        if (additionalStudiesElement != null)
                                                        {
                                                            result.CalculatedElements.Add(additionalStudiesElement);
                                                            _logger.LogInformation("✅ Additional Studies price calculated: {Price:C}",
                                                                additionalStudiesElement.Price);
                                                        }
                                                        else
                                                        {
                                                            _logger.LogDebug("⏭️ No additional studies found for class {ClassId}", student.ClassId.Value);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger.LogWarning("⚠️ Student has no class assigned, cannot calculate Additional Studies pricing");
                                                    }
                                                    continue; // Move to next element
                                                }

                        // Regular element processing
                        var calculatedElement = await CalculatePriceForElement(
                            element,
                            disabilityCategory,
                            school,
                            schoolAttributes,
                            student);

                        if (calculatedElement != null)
                        {
                            result.CalculatedElements.Add(calculatedElement);

                            _logger.LogInformation("✅ Element '{Name}' calculated: {Price:C}",
                                element.ElementName, calculatedElement.Price);
                        }
                        else
                        {
                            // ✅ REQUIREMENT 2: Only log, don't show to user
                            _logger.LogDebug("⏭️ No price found for element '{Name}' (ID: {Id})",
                                element.ElementName, element.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error calculating element '{element.ElementName}': {ex.Message}");
                        _logger.LogError(ex, "❌ Error calculating element '{Name}'", element.ElementName);
                    }
                }

                result.Success = result.CalculatedElements.Count > 0;

                // Step 7: Compare with current pricing elements before saving
                if (result.Success)
                {
                    var totalCost = result.CalculatedElements.Sum(e => e.Price);
                    var proratedCost = CalculateProratedCost(totalCost, student);
                    var currentProratedCost = CalculateProratedCost(currentTotalCost, student);

                    _logger.LogInformation("💰 Total cost before proration: {TotalCost:C}, After proration: {ProratedCost:C}",
                        totalCost, proratedCost);

                    // Compare calculated elements with current elements
                    bool elementsEqual = ArePricingElementsEqual(currentPricingElements, result.CalculatedElements);
                    bool costEqual = proratedCost == currentProratedCost;

                    if (elementsEqual && costEqual)
                    {
                        // No changes detected - skip saving
                        result.NoChangeDetected = true;
                        _logger.LogInformation("✅ No pricing changes detected for student {StudentId}. " +
                            "Current cost: {CurrentCost:C}, Calculated cost: {CalculatedCost:C}. Skipping save.",
                            schoolStudentId, currentProratedCost, proratedCost);
                    }
                    else
                    {
                        // Changes detected - create new version
                        _logger.LogInformation("🔄 Pricing changes detected for student {StudentId}. " +
                            "Elements equal: {ElementsEqual}, Cost equal: {CostEqual}. Creating new version.",
                            schoolStudentId, elementsEqual, costEqual);

                        int status = result.Errors.Count == 0 ? 2 : 6;

                        var newStudentId = await _studentService.CreateNewStudentVersionAsync(
                            schoolStudentId,
                            newVersion =>
                            {
                                newVersion.Cost = proratedCost;
                                newVersion.StatusId = status;
                            });

                        if (newStudentId.HasValue)
                        {
                            result.NewStudentId = newStudentId.Value;
                            _logger.LogInformation("✅ Created new student version {NewId} with cost {Cost:C}",
                                newStudentId.Value, proratedCost);
                        }
                        else
                        {
                            result.Errors.Add("Failed to create new student version");
                            result.Success = false;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Critical error in pricing calculation for student {StudentId}",
                    schoolStudentId);
                result.Errors.Add($"Critical error: {ex.Message}");
                return result;
            }
        }


               /// <summary>
        /// Calculate prorated cost based on student's enrollment period
        /// Full year: September 1st to August 31st
        /// Partial year: Calculate months and prorate
        /// </summary>
        /// <param name="totalCost">The full year cost</param>
        /// <param name="student">Student with StartDate and EndDate</param>
        /// <returns>Prorated cost based on enrollment period</returns>
        private decimal CalculateProratedCost(decimal totalCost, SchoolStudent student)
        {
            // If dates are missing, return full cost
            if (!student.StartDate.HasValue || !student.EndDate.HasValue)
            {
                _logger.LogWarning("⚠️ Student missing start or end date, using full cost");
                return totalCost;
            }

            var startDate = student.StartDate.Value;
            var endDate = student.EndDate.Value;

            // Check if this is a full year (September 1 to August 31)
            // Note: Year can differ (e.g., Sep 2024 to Aug 2025)
            if (startDate.Month == 9 && startDate.Day == 1 && 
                endDate.Month == 8 && endDate.Day == 31)
            {
                _logger.LogDebug("✅ Full year enrollment (Sep 1 - Aug 31), using complete cost");
                return totalCost;
            }

            // Calculate number of months
            int monthsEnrolled = CalculateEnrollmentMonths(startDate, endDate);

            if (monthsEnrolled <= 0)
            {
                _logger.LogWarning("⚠️ Calculated 0 or negative months, using full cost. Start: {Start}, End: {End}",
                    startDate, endDate);
                return totalCost;
            }

            if (monthsEnrolled >= 12)
            {
                _logger.LogDebug("✅ Enrollment period >= 12 months, using full cost");
                return totalCost;
            }

            // Prorate the cost
            var proratedCost = totalCost * monthsEnrolled / 12m;

            _logger.LogDebug("📅 Enrollment: {Start} to {End} = {Months} months. " +
                           "Full cost: {FullCost:C}, Prorated: {ProratedCost:C}",
                startDate, endDate, monthsEnrolled, totalCost, proratedCost);

            return proratedCost;
        }

        /// <summary>
        /// Calculate the number of months enrolled based on start and end dates
        /// Rules:
        /// - Start date before 16th of month: Count that month
        /// - End date after 15th of month: Count that month
        /// </summary>
        /// <param name="startDate">Enrollment start date</param>
        /// <param name="endDate">Enrollment end date</param>
        /// <returns>Number of months enrolled</returns>
        private int CalculateEnrollmentMonths(DateOnly startDate, DateOnly endDate)
        {
            if (endDate < startDate)
            {
                _logger.LogWarning("⚠️ End date {End} is before start date {Start}", endDate, startDate);
                return 0;
            }

            // Adjust start date: if before 16th, use 1st of that month, otherwise 1st of next month
            var effectiveStartDate = startDate.Day < 16
                ? new DateOnly(startDate.Year, startDate.Month, 1)
                : new DateOnly(startDate.Year, startDate.Month, 1).AddMonths(1);

            // Adjust end date: if after 15th, use last day of that month, otherwise last day of previous month
            var effectiveEndDate = endDate.Day > 15
                ? new DateOnly(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month))
                : new DateOnly(endDate.Year, endDate.Month, 1).AddDays(-1);

            // If effective end is before effective start, student doesn't qualify for any full month
            if (effectiveEndDate < effectiveStartDate)
            {
                _logger.LogDebug("⚠️ After date adjustments, no full months qualify. " +
                               "Start: {Start} -> {EffStart}, End: {End} -> {EffEnd}",
                    startDate, effectiveStartDate, endDate, effectiveEndDate);
                return 0;
            }

            // Calculate the number of full months between effective dates
            int months = 0;
            var current = effectiveStartDate;

            while (current <= effectiveEndDate)
            {
                months++;
                current = current.AddMonths(1);
            }

            _logger.LogDebug("📊 Enrollment calculation: " +
                           "Original dates: {Start} to {End}, " +
                           "Effective dates: {EffStart} to {EffEnd}, " +
                           "Months: {Months}",
                startDate, endDate, effectiveStartDate, effectiveEndDate, months);

            return months;
        }

                /// <summary>
        /// Calculate price for "Additional Studies" element by summing approved amounts for student's class
        /// </summary>
        private async Task<CalculatedPricingElement?> CalculateAdditionalStudiesPrice(
            SpecialNeedsPricingElement element,
            int category,
            int classId,
            int schoolYearId)
        {
            try
            {
                _logger.LogDebug("📚 Calculating Additional Studies pricing for class {ClassId}, school year {SchoolYearId}",
                    classId, schoolYearId);

                // Get all additional study programs for this class (last version only)
                var additionalPrograms = await _context.SchoolAdditionalStudyPrograms
                    .AsNoTracking()
                    .Where(p => p.ClassId == classId &&
                                p.SchoolYearId == schoolYearId &&
                                p.IsLastVersion == true &&
                                p.ApprovedAmount.HasValue)
                    .ToListAsync();

                if (additionalPrograms.Count == 0)
                {
                    _logger.LogDebug("⚠️ No additional study programs found for class {ClassId}", classId);
                    return null;
                }

                // Sum all approved amounts
                var totalApprovedAmount = additionalPrograms.Sum(p => p.ApprovedAmount!.Value);

                _logger.LogDebug("✅ Found {Count} additional study programs with total approved amount: {Amount:C}",
                    additionalPrograms.Count, totalApprovedAmount);

                // Build determining factor showing count of programs
                var determiningFactor = $"{additionalPrograms.Count} תוכניות לימוד נוספות";

                return new CalculatedPricingElement
                {
                    PricingElementId = element.Id,
                    PricingElementName = element.ElementName,
                    Price = totalApprovedAmount,
                    DisabilityCategory = category,
                    DeterminingFactor = determiningFactor,
                    Hours = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating Additional Studies price for class {ClassId}", classId);
                return null;
            }
        }

        /// <summary>
        /// ✅ REQUIREMENT 1: Check if element should be skipped based on attribute_to_check
        /// Returns true if attribute is null/0/false (depending on value type)
        /// </summary>
        private async Task<bool> ShouldSkipElementDueToAttribute(
            string attributeName,
            List<SchoolAttribute> schoolAttributes)
        {
            var attribute = schoolAttributes
                .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.Equals(attributeName, StringComparison.OrdinalIgnoreCase) == true);

            if (attribute == null)
            {
                _logger.LogDebug("⚠️ Attribute '{Attribute}' not found - skipping element", attributeName);
                return true; // Skip if attribute doesn't exist
            }

            if (attribute.SchoolAttributeType == null)
            {
                return true;
            }

            var valueType = attribute.SchoolAttributeType.AttributeValueType?.ToLower();
            var value = attribute.Value?.Trim();

            // Check based on value type
            switch (valueType)
            {
                case "boolean":
                case "bool":
                    // Skip if false, null, "0", or "false"
                    if (string.IsNullOrWhiteSpace(value) ||
                        value == "0" ||
                        value.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    break;

                case "integer":
                case "int":
                case "number":
                    // Skip if null, "0", or cannot parse
                    if (string.IsNullOrWhiteSpace(value) ||
                        !int.TryParse(value, out int intValue) ||
                        intValue == 0)
                    {
                        return true;
                    }
                    break;


                    case "decimal":
                    case "float":
                    case "double":
                        // Skip if null, "0", "0.0", or cannot parse
                        if (string.IsNullOrWhiteSpace(value) ||
                            !decimal.TryParse(value, out decimal decimalValue) ||
                            decimalValue == 0)
                        {
                            return true;
                        }
                        break;

                default:
                    // For text/varchar/other types, skip only if null or empty
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return true;
                    }
                    break;
            }

            return false; // Don't skip - attribute has valid value
        }

   
   /// <summary>
/// Calculate price for a single pricing element
/// ✅ UPDATED: Returns CalculatedPricingElement with determining factor and hours
/// </summary>
private async Task<CalculatedPricingElement?> CalculatePriceForElement(
    SpecialNeedsPricingElement element,
    int disabilityCategory,
    School school,
    List<SchoolAttribute> schoolAttributes,
    SchoolStudent student)
{
    // Find pricing category for this element and disability category
    var pricingCategory = await _context.SpecialNeedsPricingCategories
        .AsNoTracking()
        .FirstOrDefaultAsync(pc =>
            pc.PricingElement == element.Id &&
            pc.Category == disabilityCategory);

    if (pricingCategory == null)
    {
        _logger.LogDebug("No pricing category found for element {ElementId}, category {Category}",
            element.Id, disabilityCategory);
        return null;
    }

    // If is_lowest_level is true, return the price directly
    if (pricingCategory.IsLowestLevel == true && pricingCategory.Price.HasValue)
    {
        _logger.LogDebug("Using lowest level price: {Price:C}", pricingCategory.Price.Value);

        var calculatedElement = new CalculatedPricingElement
        {
            PricingElementId = element.Id,
            PricingElementName = element.ElementName,
            Price = pricingCategory.Price.Value,
            DisabilityCategory = disabilityCategory,
            DeterminingFactor = null,
            Hours = null
        };

       // ✅ Special handling for "Guard" elements with offset
        if (element.Title?.Equals("Guard", StringComparison.OrdinalIgnoreCase) == true || element.Title?.Equals("Security", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Check if school has "Guard off-set" attribute set to true
            var guardOffsetAttr = schoolAttributes
                .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.Equals("Guard off-set", StringComparison.OrdinalIgnoreCase) == true);

            if (guardOffsetAttr != null && !string.IsNullOrWhiteSpace(guardOffsetAttr.Value))
            {
                // Parse boolean value (true, "1", "yes" all count as true)
                bool isGuardOffset = guardOffsetAttr.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                    guardOffsetAttr.Value == "1" ||
                                    guardOffsetAttr.Value.Equals("yes", StringComparison.OrdinalIgnoreCase);

                if (isGuardOffset)
                {
                    _logger.LogInformation("🛡️ Guard off-set is enabled. Checking council match...");
                    
                    // Check if student's sending council matches school's council
                    if (student.SendingCouncil.HasValue && 
                        school.Council.HasValue && 
                        student.SendingCouncil.Value == school.Council.Value)
                    {
                        _logger.LogInformation("✅ Guard off-set applied: Student council {StudentCouncil} matches school council {SchoolCouncil} - setting cost to 0",
                            student.SendingCouncil.Value, school.Council.Value);
                        
                        calculatedElement.Price = 0;
                        calculatedElement.DeterminingFactor = "מקוזז - תלמיד/ה מרשות בית הספר";
                        return calculatedElement;
                    }
                    else
                    {
                        _logger.LogDebug("⚠️ Guard off-set NOT applied: Student council {StudentCouncil} does not match school council {SchoolCouncil}",
                            student.SendingCouncil, school.Council);
                    }
                }
            }
        }

        // ✅ Special handling for "school help" elements
        if (element.Title?.Contains("school help", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Set determining factor to the original price
            calculatedElement.DeterminingFactor = $"{calculatedElement.Price.ToString("F2")} ש\"ח";
            // Look up "Helpers Hours" attribute
            var schoolHoursAttr = schoolAttributes
                .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.Equals("Helpers Hours", StringComparison.OrdinalIgnoreCase) == true);

            if (schoolHoursAttr != null && !string.IsNullOrWhiteSpace(schoolHoursAttr.Value))
            {
                if (decimal.TryParse(schoolHoursAttr.Value, out decimal hours))
                {
                    calculatedElement.Hours = hours;
                    calculatedElement.Price = pricingCategory.Price.Value * hours;

                    _logger.LogDebug("✅ School help calculation: {OriginalPrice} × {Hours} hours = {FinalPrice:C}",
                        calculatedElement.DeterminingFactor, hours, calculatedElement.Price);
                }
                else
                {
                    _logger.LogWarning("⚠️ Could not parse school hours value: {Value}", schoolHoursAttr.Value);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ School hours attribute not found for school help element");
            }
        }

        return calculatedElement;
    }
    // If calculation_level is 'steps', use step-based calculation
    if (element.CalculationLevel?.ToLower() == "steps")
    {
        return await CalculateStepBasedPrice(
            element,
            disabilityCategory,
            school,
            schoolAttributes,
            student); // ✅ Pass student parameter
    }

    // Fallback: if price exists and no steps, return price
    if (pricingCategory.Price.HasValue)
    {
        return new CalculatedPricingElement
        {
            PricingElementId = element.Id,
            PricingElementName = element.ElementName,
            Price = pricingCategory.Price.Value,
            DisabilityCategory = disabilityCategory,
            DeterminingFactor = null,
            Hours = null
        };
    }

    return null;
}


              /// <summary>
        /// Determine education stage based on student's class level
        /// </summary>
        /// <param name="classLevel">The class level (e.g., "א", "ז", "גן")</param>
        /// <returns>Education stage: "תיכון", "יסודי", or "גן ילדים"</returns>
        private string? GetEducationStageFromClassLevel(string? classLevel)
        {
            if (string.IsNullOrWhiteSpace(classLevel))
            {
                return null;
            }

            // Normalize the class level to pure Hebrew
            var normalizedLevel = GlobalFunctions.PureHebrewText(classLevel);

            // Check for kindergarten (גן)
            if (normalizedLevel.Contains("גן") || classLevel.Contains("גן"))
            {
                _logger.LogDebug("Class level '{Level}' identified as kindergarten", classLevel);
                return "גן ילדים";
            }

            // High school levels: ז, ח, ט, י, יא, יב
            var highSchoolLevels = new[] { "ז", "ח", "ט", "י", "יא", "יב" };
            if (highSchoolLevels.Contains(normalizedLevel))
            {
                _logger.LogDebug("Class level '{Level}' identified as high school", classLevel);
                return "תיכון";
            }

            // Elementary levels: א to ו
            var elementaryLevels = new[] { "א", "ב", "ג", "ד", "ה", "ו" };
            if (elementaryLevels.Contains(normalizedLevel))
            {
                _logger.LogDebug("Class level '{Level}' identified as elementary", classLevel);
                return "יסודי";
            }

            // If we can't determine, log warning and return null
            _logger.LogWarning("⚠️ Could not determine education stage for class level: {Level}", classLevel);
            return null;
        }

        /// <summary>
        /// Calculate price using step-based pricing rules
        /// ✅ REQUIREMENT 3: Register object_element_value in determining_factor
        /// ✅ REQUIREMENT 4: Register integer attribute value in hours field
        /// </summary>
        private async Task<CalculatedPricingElement?> CalculateStepBasedPrice(
            SpecialNeedsPricingElement element,
            int category,
            School school,
            List<SchoolAttribute> schoolAttributes,
            SchoolStudent student)  
        {
            // Get all pricing steps for this element and category
            var pricingSteps = await _context.SpecialNeedsPricingSteps
                .AsNoTracking()
                .Where(ps => ps.PricingElement == element.Id && ps.Category == category)
                .ToListAsync();

            if (pricingSteps.Count == 0)
            {
                _logger.LogDebug("No pricing steps found for element {ElementId}, category {Category}",
                    element.Id, category);
                return null;
            }

            _logger.LogDebug("Processing {Count} pricing steps for element {ElementId}",
                pricingSteps.Count, element.Id);

            // Iterate through steps and find matching price
            foreach (var step in pricingSteps)
            {
                string? valueToCheck = null;
                SchoolAttribute? matchedAttribute = null;

                // Determine which value to check based on object_element_check
                switch (step.ObjectElementCheck.ToLower())
                {
                    case "education_level":
                        if (student.ClassId.HasValue && student.ClassId.Value > 0)
                        {
                            var studentClass = await _context.SchoolClasses
                                .AsNoTracking()
                                .FirstOrDefaultAsync(sc => sc.Id == student.ClassId.Value);

                            if (studentClass != null)
                            {
                                valueToCheck = GetEducationStageFromClassLevel(studentClass.Level);
                                
                                if (valueToCheck != null)
                                {
                                    _logger.LogDebug("✅ Education level determined from class level '{ClassLevel}': {EducationStage}",
                                        studentClass.Level, valueToCheck);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ Could not determine education stage from class level: {ClassLevel}",
                                        studentClass.Level);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Class ID {ClassId} not found", student.ClassId.Value);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Student has no class assigned, cannot determine education level");
                        }
                        break;;

                    case "pool_size":
                        // Find pool_size attribute from school attributes
                        matchedAttribute = schoolAttributes
                            .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.ToLower().Contains("pool") == true);

                        if (matchedAttribute != null && !string.IsNullOrWhiteSpace(matchedAttribute.Value))
                        {
                            // ✅ Resolve text value from school_attribute_types_values table
                            valueToCheck = await ResolveAttributeTextValue(matchedAttribute.Value);

                            // ✅ Treat 'אין' as null
                            if (valueToCheck?.Trim() == "אין")
                            {
                                valueToCheck = null;
                            }
                        }
                        break;

                    default:
                        // Try to find as generic attribute
                        matchedAttribute = schoolAttributes
                            .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.Equals(step.ObjectElementCheck, StringComparison.OrdinalIgnoreCase) == true);
                        valueToCheck = matchedAttribute?.Value;

                        if (matchedAttribute == null)
                        {
                            _logger.LogWarning("Unknown object_element_check: {Check}", step.ObjectElementCheck);
                            continue;
                        }
                        break;
                }

                // Check if value matches
                if (valueToCheck != null &&
                    valueToCheck.Trim().Equals(step.ObjectElementValue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (step.Price.HasValue)
                    {
                        _logger.LogDebug("✅ Step match found! Check: {Check}, Value: {Value}, Price: {Price:C}",
                            step.ObjectElementCheck, step.ObjectElementValue, step.Price.Value);

                        return new CalculatedPricingElement
                        {
                            PricingElementId = element.Id,
                            PricingElementName = element.ElementName,
                            Price = step.Price.Value,
                            DisabilityCategory = category,
                            DeterminingFactor = step.ObjectElementValue,
                            Hours = null
                        };
                    }
                }
            }

            _logger.LogDebug("⏭️ No matching step found for element {ElementId}", element.Id);
            return null;
        }


        /// <summary>
        /// Resolve attribute text value from school_attribute_types_values table
        /// If value is an integer ID, look up the text value; otherwise return as-is
        /// </summary>
        private async Task<string?> ResolveAttributeTextValue(string? storedValue)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
            {
                return null;
            }

            // Check if the stored value is an integer ID
            if (int.TryParse(storedValue.Trim(), out int valueId))
            {
                // Look up the text value from school_attribute_types_values
                var attributeTypeValue = await _context.SchoolAttributeTypeValues
                    .AsNoTracking()
                    .FirstOrDefaultAsync(atv => atv.Id == valueId && atv.IsValid);

                if (attributeTypeValue != null)
                {
                    _logger.LogDebug("✅ Resolved attribute ID {Id} to text value: {Value}",
                        valueId, attributeTypeValue.Value);
                    return attributeTypeValue.Value;
                }
                else
                {
                    _logger.LogWarning("⚠️ Could not resolve attribute type value ID: {Id}", valueId);
                    return null;
                }
            }

            // Not an integer, return the value as-is (direct text value)
            return storedValue;
        }

        /// <summary>
        /// Calculate prices for ALL tracks associated with a class
        /// Returns a list of CalculatedPricingElement, one for each track found
        /// </summary>
        private async Task<List<CalculatedPricingElement>> CalculateAllTrackPrices(
            SpecialNeedsPricingElement element,
            int category,
            int classId)
        {
            var results = new List<CalculatedPricingElement>();

            try
            {
                _logger.LogDebug("🛤️ Starting track-based pricing for element {ElementId}, class {ClassId}, category {Category}",
                    element.Id, classId, category);

                // Step 1: Find all tracks for the student's class
                var schoolTracks = await _context.SchoolTracks
                    .AsNoTracking()
                    .Where(st => st.ClassId == classId)
                    .ToListAsync();

                if (schoolTracks.Count == 0)
                {
                    _logger.LogDebug("⚠️ No school tracks found for class {ClassId}", classId);
                    return results;
                }

                _logger.LogDebug("✅ Found {Count} school tracks for class {ClassId}",
                    schoolTracks.Count, classId);

                // Step 2: Process EACH track and collect all pricing results
                foreach (var schoolTrack in schoolTracks)
                {
                    var trackPricing = await _context.TracksPricing
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tp =>
                            tp.SchoolTrackId == schoolTrack.TrackId &&
                            tp.Category == category);

                    if (trackPricing != null && trackPricing.Price.HasValue)
                    {
                        _logger.LogDebug("✅ Found track pricing: Track ID {TrackId}, Price {Price:C}",
                            schoolTrack.Id, trackPricing.Price.Value);

                        // Step 3: Get track name
                        var track = await _context.Tracks
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Id == schoolTrack.TrackId);

                        string trackName = track?.TrackName ?? "לא ידוע";

                        // Step 4: Get level name if level_id exists
                        string levelName = "";
                        if (schoolTrack.TrackLevelId.HasValue)
                        {
                            var level = await _context.TrackLevels
                                .AsNoTracking()
                                .FirstOrDefaultAsync(tl => tl.Id == schoolTrack.TrackLevelId.Value);

                            levelName = level?.LevelName ?? "";
                        }

                        // Step 5: Build determining factor (track name + level name)
                        string determiningFactor = !string.IsNullOrWhiteSpace(levelName)
                            ? $"{trackName} - {levelName}"
                            : trackName;

                        _logger.LogDebug("✅ Track pricing calculated: {Track}, Price: {Price:C}",
                            determiningFactor, trackPricing.Price.Value);

                        // Add to results list instead of returning immediately
                        results.Add(new CalculatedPricingElement
                        {
                            PricingElementId = element.Id,
                            PricingElementName = element.ElementName,
                            Price = trackPricing.Price.Value,
                            DisabilityCategory = category,
                            DeterminingFactor = determiningFactor,
                            Hours = null
                        });
                    }
                    else
                    {
                        _logger.LogDebug("⚠️ No pricing found for track {TrackId}, category {Category}",
                            schoolTrack.TrackId, category);
                    }
                }

                if (results.Count == 0)
                {
                    _logger.LogDebug("⚠️ No matching track pricing found for class {ClassId}, category {Category}",
                        classId, category);
                }
                else
                {
                    _logger.LogInformation("✅ Calculated pricing for {Count} tracks in class {ClassId}",
                        results.Count, classId);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating track-based prices for element {ElementId}", element.Id);
                return results;
            }
        }

        /// <summary>
        /// Save calculated pricing elements to database
        /// Linked to the NEW student version
        /// ✅ UPDATED: Includes DeterminingFactor and Hours fields
        /// </summary>
        public async Task<bool> SavePricingElements(int newStudentId, List<CalculatedPricingElement> elements)
        {
            try
            {
                _logger.LogInformation("💾 Saving {Count} pricing elements for student version {StudentId}",
                    elements.Count, newStudentId);

                // Add pricing elements linked to the new student version
                foreach (var element in elements)
                {
                    var pricingElement = new SchoolStudentPricingElement
                    {
                        StudentId = newStudentId,
                        PricingElementId = element.PricingElementId,
                        Price = element.Price,
                        DeterminingFactor = element.DeterminingFactor, // ✅ NEW
                        Hours = element.Hours // ✅ NEW
                    };

                    _context.SchoolStudentPricingElements.Add(pricingElement);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Saved {Count} pricing elements for student {StudentId}",
                    elements.Count, newStudentId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving pricing elements for student {StudentId}", newStudentId);
                return false;
            }
        }
    }

    // DTO Classes
    public class PricingCalculationResult
    {
        public int SchoolStudentId { get; set; }
        public int? NewStudentId { get; set; }
        public bool Success { get; set; }
        public bool NoChangeDetected { get; set; }
        public List<CalculatedPricingElement> CalculatedElements { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class CalculatedPricingElement
    {
        public int PricingElementId { get; set; }
        public string PricingElementName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DisabilityCategory { get; set; }
        public string? DeterminingFactor { get; set; }
        public decimal? Hours { get; set; } // ✅ NEW
    }
}