using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;
using PetelApp.Api.Session;
using PetelApp.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Controller for system attributes management following entity-based request flow
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : BaseController
    {
        private readonly SystemAttributeService _systemAttributeService;

        public SystemAttributesController(
            UserSessionService userSessionService,
            ILogger<SystemAttributesController> logger,
            SystemAttributeService systemAttributeService)
            : base(userSessionService, logger)
        {
            _systemAttributeService = systemAttributeService;
 
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemAttributes()
        {
            try
            {
                _logger.LogInformation("GetSystemAttributes endpoint called");

                var attributes = await _systemAttributeService.GetAllAttributesListAsync();

                if (attributes == null || attributes.Count == 0)
                {
                    _logger.LogError("No system attributes found in the database. The system attributes table should contain 4 records.");
                    return Ok(new List<SystemAttributeDto>()); // Return empty array
                }

                _logger.LogInformation("Returning {Count} system attributes from database", attributes.Count);

                return Ok(attributes); // <-- Return array directly
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system attributes");
                return StatusCode(500, new { success = false, message = "Internal server error", error = ex.Message });
            }
        }

   /*     [HttpPost("selectYear")]
        public async Task<IActionResult> SelectYear([FromBody] SelectYearRequest request)
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                var session = _userSessionService.GetUserSession(sessionId);
                if (session == null)
                {
                    return Unauthorized(new { message = "Invalid session" });
                }

                await _systemAttributeService.UpdateSelectedYearAsync(sessionId, request.YearId, request.YearType);

                return Ok(new { success = true, message = "Year selection updated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting year");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }*/

        [HttpGet("userSession")]
        public IActionResult GetUserSession()
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                var session = _userSessionService.GetUserSession(sessionId);
                if (session == null)
                {
                    return Unauthorized(new { message = "Invalid session" });
                }

                var sessionData = _userSessionService.GetAllSessionData(sessionId);
                
                // Return entity ID from session following entity-based request flow
                return Ok(new { 
                    success = true, 
                    systemAttributes = session.SystemAttributes,
                    systemAttributesLastLoaded = session.SystemAttributesLastLoaded,
                    selectedYear = session.SelectedYear,
                    entityId = session.EntityId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user session");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshSystemAttributes()
        {
            await _systemAttributeService.LoadAttributesAsync();
            return Ok(new { success = true });
        }
    }

    public class SelectYearRequest
    {
        public string YearId { get; set; } = string.Empty;
        public string YearType { get; set; } = string.Empty;
    }
}