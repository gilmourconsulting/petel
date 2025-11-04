using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TracksController : BaseController
    {
        private readonly AppDbContext _context;

        public TracksController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<TracksController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("by-year/{yearId}")]
        public async Task<IActionResult> GetTracksByYear(int yearId)
        {
            try
            {
                _logger.LogInformation("Loading tracks for year {YearId}", yearId);

                // ✅ Get data from database WITHOUT calling GlobalFunctions
                var tracksFromDb = await _context.Tracks
                    .AsNoTracking()
                    .Where(t => t.YearId == yearId && t.Id >= 100000)
                    .OrderBy(t => t.TrackName) // ✅ Order by database field
                    .ToListAsync(); // ✅ Execute query FIRST

                // ✅ THEN apply RTL text processing in memory
                var tracks = tracksFromDb
                    .Select(t => new
                    {
                        id = t.Id,
                        name = GlobalFunctions.ToRtlText(t.TrackName), // ✅ Now in memory
                        yearId = t.YearId,
                        externalCode = t.ExternalCode,
                        availableForClasses = t.AvailableForClasses
                    })
                    .ToList();
                _logger.LogInformation("Found {Count} tracks for year {YearId}", tracks.Count, yearId);

                return Ok(new
                {
                    success = true,
                    data = tracks,
                    message = "Tracks retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracks for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מגמות",
                    error = ex.Message
                });
            }
        }

                /// <summary>
        /// Get tracks filtered by year and class level
        /// Filters tracks where the class level appears in available_for_classes array
        /// </summary>
        [HttpGet("by-year/{yearId}/class-level/{classLevel}")]
        public async Task<IActionResult> GetTracksByYearAndClassLevel(int yearId, string classLevel)
        {
            try
            {
                _logger.LogInformation("Loading tracks for year {YearId} and class level {ClassLevel}", 
                    yearId, classLevel);

                // ✅ Get data from database WITHOUT calling GlobalFunctions
                var tracksFromDb = await _context.Tracks
                    .AsNoTracking()
                    .Where(t => t.YearId == yearId && t.Id >= 100000)
                    .OrderBy(t => t.TrackName) // ✅ Order by database field
                    .ToListAsync(); // ✅ Execute query FIRST
                    
                // ✅ THEN filter and apply RTL text processing in memory
                var tracks = tracksFromDb
                    .Where(t => t.AvailableForClasses == null || 
                               t.AvailableForClasses.Length == 0 ||
                               t.AvailableForClasses.Contains(classLevel))
                    .Select(t => new
                    {
                        id = t.Id,
                        name = GlobalFunctions.ToRtlText(t.TrackName), // ✅ Now in memory
                        yearId = t.YearId,
                        externalCode = t.ExternalCode,
                        availableForClasses = t.AvailableForClasses
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} tracks for year {YearId} and class level {ClassLevel}", 
                    tracks.Count, yearId, classLevel);

                return Ok(new
                {
                    success = true,
                    data = tracks,
                    message = "Tracks retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracks for year {YearId} and class level {ClassLevel}", 
                    yearId, classLevel);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מגמות",
                    error = ex.Message
                });
            }
        }
    }
}