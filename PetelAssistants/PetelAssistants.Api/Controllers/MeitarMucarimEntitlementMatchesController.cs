using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/meitar-mucarim-entitlement-matches")]
    public class MeitarMucarimEntitlementMatchesController : BaseController
    {
        private readonly MeitarMucarimEntitlementMatchService _matchService;

        public MeitarMucarimEntitlementMatchesController(
            MeitarMucarimEntitlementMatchService matchService,
            UserSessionService sessionService,
            ILogger<MeitarMucarimEntitlementMatchesController> logger)
            : base(sessionService, logger)
        {
            _matchService = matchService;
        }

        [HttpGet]
        public async Task<IActionResult> GetByPeriod([FromQuery] int year, [FromQuery] int month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var items = await _matchService.GetActiveByPeriodAsync(year, month);
            return Ok(new { success = true, data = items });
        }

        [HttpGet("by-entitlement/{entitlementId:int}")]
        public async Task<IActionResult> GetByEntitlement(int entitlementId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var items = await _matchService.GetActiveByEntitlementAsync(entitlementId);
            return Ok(new { success = true, data = items });
        }

        [HttpPost("recheck")]
        public async Task<IActionResult> RecheckPeriod([FromQuery] int year, [FromQuery] int month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "רשות לא תקינה" });

            int? userId = int.TryParse(session.UserId, out var uid) ? uid : null;

            try
            {
                var result = await _matchService.MatchPeriodAsync(
                    entityId, year, month, userId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MUCARIM entitlement period recheck failed for {Year}/{Month}", year, month);
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת התאמות זכאות למוכרים" });
            }
        }

        [HttpPost("recheck-entitlement/{entitlementId:int}")]
        public async Task<IActionResult> RecheckEntitlement(int entitlementId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "רשות לא תקינה" });

            int? userId = int.TryParse(session.UserId, out var uid) ? uid : null;

            try
            {
                var result = await _matchService.MatchEntitlementAsync(
                    entityId, entitlementId, userId);
                return Ok(new { success = true, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MUCARIM entitlement recheck failed for entitlement {EntitlementId}", entitlementId);
                return StatusCode(500, new { success = false, message = "שגיאה בהתאמת מוכרים לזכאות" });
            }
        }
    }
}
