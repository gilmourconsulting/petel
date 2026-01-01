using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;
using System.Text.Json;
using PetelApp.Api.Models;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolYearExportController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolYearExportController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolYearExportController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] int yearId)
        {
            try
            {
                _logger.LogInformation("Export request for year ID: {YearId}", yearId);

                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("Unauthorized export attempt");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Validate year exists
                var year = await _context.HebrewYears
                    .AsNoTracking()
                    .FirstOrDefaultAsync(y => y.Id == yearId);

                if (year == null)
                {
                    _logger.LogWarning("Year not found: {YearId}", yearId);
                    return NotFound(new { success = false, message = "שנה לא נמצאה" });
                }

                _logger.LogInformation("Exporting configuration for year: {YearName}", year.HebrewYearText);

                // Load all pricing elements with their categories and steps
                var elementsData = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(e => e.YearId == yearId)
                    .ToListAsync();

                var pricingElements = new List<PricingElementExport>();

                foreach (var element in elementsData)
                {
                    var categories = await _context.SpecialNeedsPricingCategories
                        .AsNoTracking()
                        .Where(c => c.PricingElement == element.Id)
                        .ToListAsync();

                    var categoryExports = new List<PricingCategoryExport>();

                    foreach (var category in categories)
                    {
                        var steps = await _context.SpecialNeedsPricingSteps
                            .AsNoTracking()
                            .Where(s => s.PricingElement == element.Id && s.Category == category.Category)
                            .Select(s => new PricingStepExport
                            {
                                ObjectCheck = s.ObjectCheck,
                                ObjectElementCheck = s.ObjectElementCheck,
                                ObjectElementValue = s.ObjectElementValue,
                                Price = s.Price
                            })
                            .ToListAsync();

                        categoryExports.Add(new PricingCategoryExport
                        {
                            Category = category.Category,
                            IsLowestLevel = category.IsLowestLevel ?? false,
                            Price = category.Price,
                            Steps = steps
                        });
                    }

                    pricingElements.Add(new PricingElementExport
                    {
                        Name = element.ElementName,
                        Title = element.Title,
                        Description = element.Description,
                        CalculationLevel = element.CalculationLevel,
                        AttributeToCheck = element.AttributeToCheck,
                        Categories = categoryExports
                    });
                }

                var documentTypes = await _context.DocumentTypes
                    .AsNoTracking()
                    .Where(d => d.YearId == yearId)
                    .Select(d => new DocumentTypeExport
                    {
                        TypeName = d.Name
                    })
                    .ToListAsync();

                var studyPrograms = await _context.AdditionalStudyProgramsPricing
                    .AsNoTracking()
                    .Where(p => p.YearId == yearId)
                    .Select(p => new StudyProgramExport
                    {
                        Students = p.Students,
                        Price = p.Price
                    })
                    .ToListAsync();

                var tracksData = await _context.Tracks
                    .AsNoTracking()
                    .Where(t => t.YearId == yearId)
                    .ToListAsync();

                var tracks = new List<TrackExport>();

                foreach (var track in tracksData)
                {
                    var levels = await _context.TrackLevels
                        .AsNoTracking()
                        .Where(l => l.SchoolTrackId == track.Id)
                        .ToListAsync();

                    var levelExports = new List<TrackLevelExport>();

                    foreach (var level in levels)
                    {
                        var pricing = await _context.TracksPricing
                            .AsNoTracking()
                            .Where(p => p.LevelId == level.Id)
                            .Select(p => new TrackPricingExport
                            {
                                Category = p.Category ?? 0,
                                Price = p.Price
                            })
                            .ToListAsync();

                        levelExports.Add(new TrackLevelExport
                        {
                            LevelName = level.LevelName ?? "",
                            Description = null,
                            MinHours = level.MinHours,
                            MaxHours = level.MaxHours,
                            AvailableForClasses = level.AvailableForClasses,
                            Pricing = pricing
                        });
                    }

                    tracks.Add(new TrackExport
                    {
                        TrackName = track.TrackName,
                        Description = null,
                        ExternalCode = track.ExternalCode,
                        AvailableForClasses = track.AvailableForClasses,
                        Levels = levelExports
                    });
                }

                // Create export object
                var exportData = new SchoolYearConfigExport
                {
                    YearId = yearId,
                    YearName = year.HebrewYearText,
                    ExportDate = DateTime.UtcNow,
                    PricingElements = pricingElements,
                    DocumentTypes = documentTypes,
                    StudyPrograms = studyPrograms,
                    Tracks = tracks
                };

                _logger.LogInformation("Export complete: {ElementCount} elements, {TrackCount} tracks, {DocCount} docs",
                    pricingElements.Count, tracks.Count, documentTypes.Count);

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(exportData, options);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"SchoolYearConfig_{year.HebrewYearText}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting year configuration");
                return StatusCode(500, new { success = false, message = "שגיאה בייצוא הגדרות", error = ex.Message });
            }
        }
    }
}
