using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/gregorian-years")]
    public class GregorianYearsController : BaseController
    {
        private readonly GregorianYearService _service;

        public GregorianYearsController(
            GregorianYearService service,
            UserSessionService userSessionService,
            ILogger<GregorianYearsController> logger)
            : base(userSessionService, logger)
        {
            _service = service;
        }

        [HttpGet("context")]
        public async Task<IActionResult> GetContext()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetContextAsync();
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading gregorian year context");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת שנים לועזיות" });
            }
        }

        [HttpGet("{year:int}/hub-summary")]
        public async Task<IActionResult> GetHubSummary(int year)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetHubSummaryAsync(year);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading gregorian hub summary {Year}", year);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סיכום השנה הלועזית" });
            }
        }

        [HttpGet("{year:int}/budget")]
        public async Task<IActionResult> GetBudget(int year)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetBudgetAsync(year);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading gregorian budget {Year}", year);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת תקציב השנה הלועזית" });
            }
        }

        [HttpGet("{year:int}/entitlements")]
        public async Task<IActionResult> GetEntitlements(int year)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetEntitlementsAsync(year);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading gregorian entitlements {Year}", year);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת זכאויות השנה הלועזית" });
            }
        }

        [HttpGet("{year:int}/assistants")]
        public async Task<IActionResult> GetAssistants(int year)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var data = await _service.GetAssistantsAsync(year);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading gregorian assistants {Year}", year);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סייעות השנה הלועזית" });
            }
        }
    }
}
