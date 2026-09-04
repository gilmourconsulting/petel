using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/yearly-budgets")]
    public class YearlyBudgetsController : BaseController
    {
        private readonly YearlyBudgetService _service;

        public YearlyBudgetsController(
            YearlyBudgetService service,
            UserSessionService userSessionService,
            ILogger<YearlyBudgetsController> logger)
            : base(userSessionService, logger)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetForYear([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            try
            {
                var data = await _service.GetForYearAsync(yearId);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetByIdAsync(id);
                if (data == null)
                    return NotFound(new { success = false, message = "תקציב לא נמצא" });

                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Save(int id, [FromBody] UpdateYearlyBudgetRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.SaveAsync(entityId, userId, id, request);
                return Ok(new { success = true, message = "התקציב נשמר בהצלחה", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{id:int}/calculate")]
        public async Task<IActionResult> Calculate(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.CalculateAsync(entityId, userId, id);
                return Ok(new { success = true, message = "החישוב הושלם", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{id:int}/recalculate-summaries")]
        public async Task<IActionResult> RecalculateSummaries(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.RecalculateSummariesAsync(userId, id);
                return Ok(new { success = true, message = "סיכומי השכר והמיתר חושבו מחדש", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.LockAsync(userId, id);
                return Ok(new { success = true, message = "התקציב ננעל", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("new-version")]
        public async Task<IActionResult> NewVersionForYear([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.CreateNewVersionForYearAsync(entityId, userId, yearId);
                return Ok(new { success = true, message = "גרסה חדשה נוצרה", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{id:int}/new-version")]
        public async Task<IActionResult> NewVersion(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.CreateNewVersionAsync(entityId, userId, id);
                return Ok(new { success = true, message = "גרסה חדשה נוצרה", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var data = await _service.DeleteAsync(userId, id);
                return Ok(new { success = true, message = "הגרסה נמחקה", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
