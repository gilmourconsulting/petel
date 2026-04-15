
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PetelATH.Api.Configuration;
using PetelATH.Api.Session;
using PetelATH.Api.Services;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : BaseController
    {
        private readonly UserSessionService _sessionService;
        private readonly SecuritySettings _securitySettings;

        private readonly SystemAttributeCache? _systemAttributeCache;

        public SessionController(
            UserSessionService sessionService,
            IOptions<SecuritySettings> securitySettings,
            SystemAttributeCache? systemAttributeCache,
            ILogger<SessionController> logger)
                : base(sessionService, logger)
        {
            _sessionService = sessionService;
            _securitySettings = securitySettings.Value;
            _systemAttributeCache = systemAttributeCache;
        }

        /// <summary>
        /// Get session timeout configuration for current user
        /// Returns timeout settings from system attributes (database) with fallback to config
        /// </summary>
        [HttpGet("timeout-config")]
        public IActionResult GetSessionTimeoutConfig()
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            // ✅ Read from SystemAttributeCache instead of static config
            int timeoutMinutes = GetSessionTimeoutMinutes();

            return Ok(new
            {
                timeoutMinutes = timeoutMinutes,
                warningMinutes = 2 // Show warning 2 minutes before timeout
            });
        }

        /// <summary>
        /// Get SessionTimeoutMinutes from system attributes cache, fallback to config
        /// </summary>
        private int GetSessionTimeoutMinutes()
        {
            try
            {
                var attribute = _systemAttributeCache.GetAttributeByName("Security_SessionTimeoutMinutes");
                if (attribute != null && int.TryParse(attribute.Value, out int minutes))
                {
                    _logger.LogDebug("Using SessionTimeoutMinutes from system attributes: {Minutes}", minutes);
                    return minutes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read SessionTimeoutMinutes from cache, using default");
            }
            
            // Fallback to configuration file
            _logger.LogDebug("Using SessionTimeoutMinutes from config file: {Minutes}", _securitySettings.SessionTimeoutMinutes);
            return _securitySettings.SessionTimeoutMinutes;
        }

        /// <summary>
        /// Get current user session info (identity + properties)
        /// </summary>
        [HttpGet]
        public IActionResult GetSession()
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { message = "Session not found" });
            }

            return Ok(new
            {
                // Identity data
                userId = session.UserId,
                username = session.Username,
                userFullName = session.UserFullName,
                entityId = session.EntityId,
                entityName = session.EntityName,
                entityTypeId = session.EntityTypeId,
                entityTypeName = session.EntityTypeName,
                
                // Session metadata
                sessionId = session.SessionId,
                createdAt = session.CreatedAt,
                lastAccessedAt = session.LastAccessedAt,
                
                // All session properties
                properties = session.GetAllProperties()
            });
        }

        /// <summary>
        /// Set a session property (generic storage for mutable session data)
        /// </summary>
        [HttpPost("property")]
        public IActionResult SetSessionProperty([FromBody] SessionPropertyRequest request)
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { message = "Session not found" });
            }

            session.SetProperty(request.Key, request.Value);

            _logger.LogDebug("Session property set: {Key}={Value} for session {SessionId}", 
                request.Key, request.Value, session.SessionId);

            return Ok(new { success = true, key = request.Key, value = request.Value });
        }

        /// <summary>
        /// Get a specific session property
        /// </summary>
        [HttpGet("property/{key}")]
        public IActionResult GetSessionProperty(string key)
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { message = "Session not found" });
            }

            var value = session.GetProperty(key);

            if (value == null)
            {
                return NotFound(new { message = $"Property '{key}' not found" });
            }

            return Ok(new { key = key, value = value });
        }

        /// <summary>
        /// Get all session properties
        /// </summary>
        [HttpGet("properties")]
        public IActionResult GetAllSessionProperties()
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { message = "Session not found" });
            }

            return Ok(session.GetAllProperties());
        }

        /// <summary>
        /// Delete a session property
        /// </summary>
        [HttpDelete("property/{key}")]
        public IActionResult DeleteSessionProperty(string key)
        {
            var session = GetCurrentSession();
            
            if (session == null)
            {
                return Unauthorized(new { message = "Session not found" });
            }

            session.RemoveProperty(key);

            return Ok(new { success = true, message = $"Property '{key}' removed" });
        }
    }

    public class SessionPropertyRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}