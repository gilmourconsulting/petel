using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitlementsController : BaseController
    {
        private readonly EntitlementService _entitlementService;

        public EntitlementsController(
            EntitlementService entitlementService,
            UserSessionService userSessionService,
            ILogger<EntitlementsController> logger)
            : base(userSessionService, logger)
        {
            _entitlementService = entitlementService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int yearId, [FromQuery] string? kind = null)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            try
            {
                var items = await _entitlementService.ListEntitlementsAsync(entityId, yearId, kind);
                return Ok(new { success = true, data = items });
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

            var item = await _entitlementService.GetEntitlementAsync(id);
            if (item == null)
                return NotFound(new { success = false, message = "זכאות לא נמצאה" });

            return Ok(new { success = true, data = item });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEntitlementRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var id = await _entitlementService.CreateEntitlementAsync(entityId, userId, request);
                return Ok(new { success = true, message = "זכאות נוצרה בהצלחה", data = new { id } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEntitlementRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                await _entitlementService.UpdateEntitlementAsync(entityId, userId, id, request);
                return Ok(new { success = true, message = "זכאות עודכנה בהצלחה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                await _entitlementService.DeactivateEntitlementAsync(userId, id);
                return Ok(new { success = true, message = "זכאות הושבתה בהצלחה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
