using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;

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

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetYear(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var year = await _sharedContext.HebrewYears
                    .AsNoTracking()
                    .Where(y => y.Id == id)
                    .Select(y => new YearDetailDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        StartDate = y.StartDate,
                        EndDate = y.EndDate,
                        IsCurrent = y.IsCurrent,
                        IsPrevious = y.IsPrevious,
                        IsActive = y.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (year == null)
                    return NotFound(new { success = false, message = "שנה לא נמצאה" });

                return Ok(new
                {
                    id = year.Id,
                    yearName = year.YearName,
                    startDate = year.StartDate,
                    endDate = year.EndDate,
                    isCurrent = year.IsCurrent,
                    isPrevious = year.IsPrevious,
                    isActive = year.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading year {Id}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת השנה" });
            }
        }

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
                    .Select(y => new YearDetailDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        StartDate = y.StartDate,
                        EndDate = y.EndDate,
                        IsCurrent = y.IsCurrent,
                        IsPrevious = y.IsPrevious,
                        IsActive = y.IsActive
                    })
                    .ToListAsync();

                var currentYear = years.FirstOrDefault(y => y.IsCurrent) ?? years.FirstOrDefault();
                var previousYear = years.FirstOrDefault(y => y.IsPrevious)
                    ?? years.Where(y => currentYear == null || y.Id != currentYear.Id).Skip(1).FirstOrDefault();

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

        [HttpGet("admin")]
        public async Task<IActionResult> GetAllYearsAdmin()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var years = await _sharedContext.HebrewYears
                .AsNoTracking()
                .OrderByDescending(y => y.Id)
                .Select(y => new YearDetailDto
                {
                    Id = y.Id,
                    YearName = y.YearName,
                    StartDate = y.StartDate,
                    EndDate = y.EndDate,
                    IsCurrent = y.IsCurrent,
                    IsPrevious = y.IsPrevious,
                    IsActive = y.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = years });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateYear(int id, [FromBody] UpdateHebrewYearRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var year = await _sharedContext.HebrewYears.FirstOrDefaultAsync(y => y.Id == id);
            if (year == null)
                return NotFound(new { success = false, message = "שנה לא נמצאה" });

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
                return BadRequest(new { success = false, message = "תאריך סיום חייב להיות אחרי תאריך התחלה" });

            if (request.IsCurrent)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.Id != id && y.IsCurrent).ToListAsync();
                foreach (var other in others)
                    other.IsCurrent = false;
            }

            if (request.IsPrevious)
            {
                var others = await _sharedContext.HebrewYears.Where(y => y.Id != id && y.IsPrevious).ToListAsync();
                foreach (var other in others)
                    other.IsPrevious = false;
            }

            year.StartDate = request.StartDate;
            year.EndDate = request.EndDate;
            year.IsCurrent = request.IsCurrent;
            year.IsPrevious = request.IsPrevious;
            year.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "שנת לימודים עודכנה בהצלחה" });
        }
    }
}
