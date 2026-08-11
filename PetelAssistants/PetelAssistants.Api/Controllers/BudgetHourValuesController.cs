using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/budget-hour-values")]
    public class BudgetHourValuesController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public BudgetHourValuesController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<BudgetHourValuesController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetForYear([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var yearExists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == yearId);
            if (!yearExists)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            var row = await _sharedContext.BudgetHourValues.AsNoTracking()
                .FirstOrDefaultAsync(r => r.HebrewYearId == yearId);

            var dto = new BudgetHourValueDto
            {
                Id = row?.Id ?? 0,
                HebrewYearId = yearId,
                HourValue = row?.HourValue ?? 0
            };

            return Ok(new { success = true, data = dto });
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertBudgetHourValueRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request == null || request.HebrewYearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            if (request.HourValue < 0)
                return BadRequest(new { success = false, message = "ערך שעה חייב להיות אי-שלילי" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var yearExists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == request.HebrewYearId);
            if (!yearExists)
                return BadRequest(new { success = false, message = "שנה לא נמצאה" });

            var hourValue = Math.Round(request.HourValue, 4, MidpointRounding.AwayFromZero);
            var now = DateTime.UtcNow;

            var existing = await _sharedContext.BudgetHourValues
                .FirstOrDefaultAsync(r => r.HebrewYearId == request.HebrewYearId);

            if (existing != null)
            {
                existing.HourValue = hourValue;
                existing.UpdatedAt = now;
                existing.UpdateUser = userId;
            }
            else
            {
                existing = new BudgetHourValue
                {
                    HebrewYearId = request.HebrewYearId,
                    HourValue = hourValue,
                    UserId = userId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdateUser = userId
                };
                _sharedContext.BudgetHourValues.Add(existing);
            }

            await _sharedContext.SaveChangesAsync();

            var dto = new BudgetHourValueDto
            {
                Id = existing.Id,
                HebrewYearId = existing.HebrewYearId,
                HourValue = existing.HourValue
            };

            return Ok(new { success = true, message = "ערך השעה נשמר בהצלחה", data = dto });
        }
    }
}
