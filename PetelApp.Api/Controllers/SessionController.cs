using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : BaseController
    {
        //private readonly UserSessionService _userSessionService;
        private readonly ILogger<SessionController> _logger;

        public SessionController(
            UserSessionService userSessionService,
            ILogger<BaseController> baseLogger,
            ILogger<SessionController> logger)
            : base(userSessionService, baseLogger)
        {
            _logger = logger;
        }

        [HttpGet("data")]
        public IActionResult GetSessionData([FromQuery] string? key = null)
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                if (!_userSessionService.IsSessionValid(sessionId))
                {
                    return Unauthorized(new { message = "Session expired" });
                }

                if (string.IsNullOrEmpty(key))
                {
                    // Return all session data
                    var allData = _userSessionService.GetAllSessionData(sessionId);
                    return Ok(new { success = true, data = allData });
                }
                else
                {
                    // Return specific key
                    var value = _userSessionService.GetSessionData(sessionId, key);
                    return Ok(new { success = true, key = key, value = value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session data");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("data")]
        public IActionResult UpdateSessionData([FromBody] UpdateSessionDataRequest request)
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { message = "No valid session found" });
                }

                if (!_userSessionService.IsSessionValid(sessionId))
                {
                    return Unauthorized(new { message = "Session expired" });
                }

                var success = _userSessionService.UpdateSessionData(sessionId, request.Key, request.Value);
                if (success)
                {
                    return Ok(new { success = true, message = "Session data updated" });
                }
                else
                {
                    return BadRequest(new { message = "Failed to update session data" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session data");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("invalidate")]
        public IActionResult InvalidateSession()
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new { message = "No session to invalidate" });
                }

                _userSessionService.InvalidateSession(sessionId);
                return Ok(new { success = true, message = "Session invalidated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating session");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("validate")]
        public IActionResult ValidateSession()
        {
            try
            {
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { valid = false, message = "No session token provided" });
                }

                var isValid = _userSessionService.IsSessionValid(sessionId);
                if (isValid)
                {
                    var session = _userSessionService.GetUserSession(sessionId);
                    return Ok(new { 
                        valid = true, 
                        userId = session?.UserId,
                        userFullName = session?.UserFullName,
                        lastAccessed = session?.LastAccessedAt
                    });
                }
                else
                {
                    return Unauthorized(new { valid = false, message = "Session expired or invalid" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class UpdateSessionDataRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}