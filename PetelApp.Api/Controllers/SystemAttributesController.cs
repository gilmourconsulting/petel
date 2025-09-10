using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Controller for system attributes management following entity-based request flow
    /// Inherits from BaseController for session access methods
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : BaseController
    {
        private readonly SystemAttributeService _systemAttributeService;
        private readonly UserSessionService _userSessionService;
        private readonly ILogger<SystemAttributesController> _logger;

        public SystemAttributesController(
            SystemAttributeService systemAttributeService, 
            UserSessionService userSessionService,
            ILogger<SystemAttributesController> logger)
        {
            _systemAttributeService = systemAttributeService;
            _userSessionService = userSessionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemAttributes()
        {
            try
            {
                _logger.LogInformation("GetSystemAttributes endpoint called");

                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    // Allow access to system attributes without session for initial load
                    var attributes = _systemAttributeService.GetSystemAttributes();
                    return Ok(new { success = true, data = attributes });
                }

                var systemAttributes = await _systemAttributeService.GetSystemAttributesForSessionAsync(sessionId);
                return Ok(new { success = true, data = systemAttributes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system attributes");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("selectYear")]
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
        }

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
                
                // Return entity ID from session following the entity-based request flow
                return Ok(new { 
                    success = true, 
                    systemAttributes = session.SystemAttributes,
                    systemAttributesLastLoaded = session.SystemAttributesLastLoaded,
                    selectedYear = session.SelectedYear,
                    entityId = session.EntityId // Return EntityId instead of TenantId
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