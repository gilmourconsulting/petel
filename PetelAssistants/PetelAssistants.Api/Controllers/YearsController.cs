using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YearsController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public YearsController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<YearsController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        /// <summary>
        /// Returns year context for the dashboard: previous year, current year, and
        /// the full list of active years for the "select another year" modal.
        /// </summary>
        [HttpGet("context")]
        public async Task<IActionResult> GetYearContext()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var years = await _sharedContext.HebrewYears
                    .AsNoTracking()
                    .Where(y => y.IsActive)
                    .OrderByDescending(y => y.Id)
                    .Select(y => new
                    {
                        id         = y.Id,
                        yearName   = y.YearName,
                        isCurrent  = y.IsCurrent,
                        isPrevious = y.IsPrevious
                    })
                    .ToListAsync();

                var currentYear  = years.FirstOrDefault(y => y.isCurrent);
                var previousYear = years.FirstOrDefault(y => y.isPrevious);

                return Ok(new
                {
                    currentYear,
                    previousYear,
                    allYears = years
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "hebrew_years table not found; run add-years-and-menu.sql");
                return Ok(new { currentYear = (object?)null, previousYear = (object?)null, allYears = Array.Empty<object>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading year context");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת שנים" });
            }
        }
    }
}
