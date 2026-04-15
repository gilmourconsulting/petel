using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackLevelsController : BaseController
    {
        private readonly AppDbContext _context;

        public TrackLevelsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<TrackLevelsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("by-track/{trackId}")]
        public async Task<IActionResult> GetTrackLevelsByTrack(int trackId)
        {
            try
            {
                _logger.LogInformation("Loading track levels for track {TrackId}", trackId);

                var levels = await _context.TrackLevels
                    .AsNoTracking()
                    .Where(tl => tl.SchoolTrackId == trackId)
                    .Select(tl => new
                    {
                        id = tl.Id,
                        levelName = tl.LevelName,
                        level = tl.LevelName,
                        minHours = tl.MinHours,
                        maxHours = tl.MaxHours,
                        trackId = tl.SchoolTrackId
                    })
                    .OrderBy(tl => tl.levelName)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} track levels for track {TrackId}", levels.Count, trackId);

                return Ok(new
                {
                    success = true,
                    data = levels,
                    message = "Track levels retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting track levels for track {TrackId}", trackId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת רמות מגמה",
                    error = ex.Message
                });
            }
        }
    }
}