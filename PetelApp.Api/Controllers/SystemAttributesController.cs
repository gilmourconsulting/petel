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

                // Get all system attributes with null check
                var attributes = await _systemAttributeService.GetAllAttributesListAsync();
                
                // Log warning if no attributes found
                if (attributes == null || attributes.Count == 0)
                {
                    _logger.LogError("No system attributes found in the database. The system attributes table should contain 4 records.");
                    return Ok(new { success = true, data = new Dictionary<string, SystemAttributeDto>() });
                }
                
                _logger.LogInformation("Returning {Count} system attributes from database", attributes.Count);
                
                // Check if version attribute exists
                var versionAttr = attributes.FirstOrDefault(a => a.Description == "version");
                if (versionAttr != null)
                {
                    _logger.LogInformation("Version attribute: {Version}", versionAttr.Value);
                }
                else
                {
                    _logger.LogWarning("No version attribute found in database");
                }
                
                // Return in expected format with success flag and data property
                return Ok(new { success = true, data = attributes });
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
    }

    public class SelectYearRequest
    {
        public string YearId { get; set; } = string.Empty;
        public string YearType { get; set; } = string.Empty;
    }
}