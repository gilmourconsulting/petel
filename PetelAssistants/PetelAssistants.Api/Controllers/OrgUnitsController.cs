using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/org-units")]
    public class OrgUnitsController : BaseController
    {
        private readonly OrgUnitService _orgUnitService;

        public OrgUnitsController(
            OrgUnitService orgUnitService,
            UserSessionService userSessionService,
            ILogger<OrgUnitsController> logger)
            : base(userSessionService, logger)
        {
            _orgUnitService = orgUnitService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? type)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            try
            {
                var units = await _orgUnitService.ListOrgUnitsAsync(entityId, type);
                return Ok(new { success = true, data = units });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrgUnitRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            try
            {
                var id = await _orgUnitService.CreateOrgUnitAsync(entityId, request);
                return Ok(new { success = true, message = "מוסד נוצר בהצלחה", data = new { id } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateOrgUnitRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            try
            {
                await _orgUnitService.UpdateOrgUnitAsync(entityId, id, request);
                return Ok(new { success = true, message = "מוסד עודכן בהצלחה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            return await SetActive(id, true);
        }

        [HttpPut("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            return await SetActive(id, false);
        }

        private async Task<IActionResult> SetActive(int id, bool isActive)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            try
            {
                await _orgUnitService.SetOrgUnitActiveAsync(entityId, id, isActive);
                return Ok(new { success = true, message = isActive ? "מוסד הופעל בהצלחה" : "מוסד הושבת בהצלחה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
