// PetelApp.Api/Services/StudentPricingService.cs
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Services
{
    public class StudentPricingService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StudentPricingService> _logger;

        public StudentPricingService(AppDbContext context, ILogger<StudentPricingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Calculate pricing elements for a student
        /// </summary>
        /// <param name="schoolStudentId">ID of the school_students record</param>
        /// <returns>List of calculated pricing elements with prices</returns>
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
                // Step 1: Get student record
                _logger.LogInformation("📊 Starting pricing calculation for student ID: {StudentId}", schoolStudentId);

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

                // Step 2: Get school year to find year_id
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

                // Step 3: Get school details for attribute lookups
                var school = await _context.Schools
                    .AsNoTracking()
                    .Where(s => s.SchoolYearId == student.SchoolYearId && s.IsLastVersion)
                    .FirstOrDefaultAsync();

                if (school == null)
                {
                    result.Errors.Add($"School not found for school year {student.SchoolYearId}");
                    return result;
                }

                // Get school attributes for step-based pricing
                var schoolAttributes = await _context.SchoolAttributes
                    .AsNoTracking()
                    .Where(sa => sa.SchoolYearId == student.SchoolYearId && sa.IsLastVersion)
                    .Include(sa => sa.SchoolAttributeType)
                    .ToListAsync();

                _logger.LogInformation("✅ School found with {AttributeCount} attributes", schoolAttributes.Count);

                // Step 4: Get all pricing elements for this year (sorted by sort_order)
                var pricingElements = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(pe => pe.YearId == yearId)
                    .OrderBy(pe => pe.SortOrder)
                    .ThenBy(pe => pe.ElementName)
                    .ToListAsync();

                _logger.LogInformation("📋 Found {Count} pricing elements for year {YearId}", 
                    pricingElements.Count, yearId);

                // Step 5: Process each pricing element
                foreach (var element in pricingElements)
                {
                    try
                    {
                        var calculatedPrice = await CalculatePriceForElement(
                            element, 
                            disabilityCategory, 
                            school,
                            schoolAttributes);

                        if (calculatedPrice.HasValue)
                        {
                            result.CalculatedElements.Add(new CalculatedPricingElement
                            {
                                PricingElementId = element.Id,
                                PricingElementName = element.ElementName,
                                Price = calculatedPrice.Value,
                                DisabilityCategory = disabilityCategory
                            });

                            _logger.LogInformation("✅ Element '{Name}' calculated: {Price:C}", 
                                element.ElementName, calculatedPrice.Value);
                        }
                        else
                        {
                            result.Errors.Add($"No price found for element '{element.ElementName}' (ID: {element.Id})");
                            _logger.LogWarning("⚠️ No price found for element '{Name}' (ID: {Id})", 
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
        /// Calculate price for a single pricing element
        /// </summary>
        private async Task<decimal?> CalculatePriceForElement(
            SpecialNeedsPricingElement element,
            int disabilityCategory,
            School school,
            List<SchoolAttribute> schoolAttributes)
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
                return pricingCategory.Price.Value;
            }

            // If calculation_level is 'steps', use step-based calculation
            if (element.CalculationLevel?.ToLower() == "steps")
            {
                return await CalculateStepBasedPrice(
                    element.Id, 
                    disabilityCategory, 
                    school, 
                    schoolAttributes);
            }

            // Fallback: if price exists and no steps, return price
            if (pricingCategory.Price.HasValue)
            {
                return pricingCategory.Price.Value;
            }

            return null;
        }

        /// <summary>
        /// Calculate price using step-based pricing rules
        /// </summary>
        private async Task<decimal?> CalculateStepBasedPrice(
            int pricingElementId,
            int category,
            School school,
            List<SchoolAttribute> schoolAttributes)
        {
            // Get all pricing steps for this element and category
            var pricingSteps = await _context.SpecialNeedsPricingSteps
                .AsNoTracking()
                .Where(ps => ps.PricingElement == pricingElementId && ps.Category == category)
                .ToListAsync();

            if (pricingSteps.Count == 0)
            {
                _logger.LogDebug("No pricing steps found for element {ElementId}, category {Category}", 
                    pricingElementId, category);
                return null;
            }

            _logger.LogDebug("Processing {Count} pricing steps for element {ElementId}", 
                pricingSteps.Count, pricingElementId);

            // Iterate through steps and find matching price
            foreach (var step in pricingSteps)
            {
                string? valueToCheck = null;

                // Determine which value to check based on object_element_check
                switch (step.ObjectElementCheck.ToLower())
                {
                    case "education_level":
                        valueToCheck = school.EducationStage;
                        break;

                    case "pool_size":
                        // Find pool_size attribute from school attributes
                        var poolAttr = schoolAttributes
                            .FirstOrDefault(sa => sa.SchoolAttributeType?.Name?.ToLower() == "pool_size" 
                                               || sa.SchoolAttributeType?.Name?.ToLower() == "pool size");
                        valueToCheck = poolAttr?.Value;
                        break;

                    default:
                        _logger.LogWarning("Unknown object_element_check: {Check}", step.ObjectElementCheck);
                        continue;
                }

                // Check if value matches
                if (valueToCheck != null && 
                    valueToCheck.Trim().Equals(step.ObjectElementValue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (step.Price.HasValue)
                    {
                        _logger.LogDebug("✅ Step match found! Check: {Check}, Value: {Value}, Price: {Price:C}",
                            step.ObjectElementCheck, step.ObjectElementValue, step.Price.Value);
                        return step.Price.Value;
                    }
                }
            }

            _logger.LogDebug("⚠️ No matching step found for element {ElementId}", pricingElementId);
            return null;
        }

        /// <summary>
        /// Save calculated pricing elements to database
        /// </summary>
        public async Task<bool> SavePricingElements(int schoolStudentId, List<CalculatedPricingElement> elements)
        {
            try
            {
                // Remove existing pricing elements for this student
                var existingElements = await _context.SchoolStudentPricingElements
                    .Where(pe => pe.StudentId == schoolStudentId)
                    .ToListAsync();

                if (existingElements.Any())
                {
                    _context.SchoolStudentPricingElements.RemoveRange(existingElements);
                    _logger.LogInformation("🗑️ Removed {Count} existing pricing elements", existingElements.Count);
                }

                // Add new pricing elements
                foreach (var element in elements)
                {
                    var pricingElement = new SchoolStudentPricingElement
                    {
                        StudentId = schoolStudentId,
                        PricingElementId = element.PricingElementId,
                        Price = element.Price
                    };

                    _context.SchoolStudentPricingElements.Add(pricingElement);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Saved {Count} pricing elements for student {StudentId}", 
                    elements.Count, schoolStudentId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving pricing elements for student {StudentId}", schoolStudentId);
                return false;
            }
        }
    }

    // DTO Classes for pricing calculation results
    public class PricingCalculationResult
    {
        public int SchoolStudentId { get; set; }
        public bool Success { get; set; }
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
    }
}