using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Petel.Core.Abstractions;
using Petel.Core.Controllers;
using Petel.Core.Security;
using Petel.Core.Session;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : BaseController
    {
        private readonly IAttributeCache _attributeCache;
        private readonly SecuritySettings _securitySettings;

        public SessionController(
            UserSessionService userSessionService,
            IAttributeCache attributeCache,
            IOptions<SecuritySettings> securitySettings,
            ILogger<SessionController> logger)
            : base(userSessionService, logger)
        {
            _attributeCache = attributeCache;
            _securitySettings = securitySettings.Value;
        }

        /// <summary>Get current session identity and all stored properties.</summary>
        [HttpGet]
        public IActionResult GetSession()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { message = "Session not found" });

            return Ok(new
            {
                userId         = session.UserId,
                username       = session.Username,
                userFullName   = session.UserFullName,
                entityId       = session.EntityId,
                entityName     = session.EntityName,
                entityTypeId   = session.EntityTypeId,
                entityTypeName = session.EntityTypeName,
                sessionId      = session.SessionId,
                createdAt      = session.CreatedAt,
                lastAccessedAt = session.LastAccessedAt,
                properties     = session.GetAllProperties()
            });
        }

        /// <summary>Return session timeout configuration loaded from system attributes.</summary>
        [HttpGet("timeout-config")]
        public IActionResult GetTimeoutConfig()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int timeoutMinutes = _securitySettings.SessionTimeoutMinutes;
            var attributeValue = _attributeCache.GetAttributeValue("Security_SessionTimeoutMinutes");
            if (int.TryParse(attributeValue, out int dbMinutes) && dbMinutes > 0)
                timeoutMinutes = dbMinutes;

            return Ok(new { timeoutMinutes, warningMinutes = 2 });
        }

        /// <summary>Return all mutable properties stored in the current session.</summary>
        [HttpGet("properties")]
        public IActionResult GetAllProperties()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { message = "Session not found" });

            return Ok(session.GetAllProperties());
        }

        /// <summary>Store a mutable property in the current session.</summary>
        [HttpPost("property")]
        public IActionResult SetProperty([FromBody] SessionPropertyRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { message = "Session not found" });

            if (string.IsNullOrWhiteSpace(request.Key))
                return BadRequest(new { message = "Key is required" });

            session.SetProperty(request.Key, request.Value ?? string.Empty);
            _logger.LogDebug("Session property set: {Key}={Value}", request.Key, request.Value);

            return Ok(new { success = true, key = request.Key, value = request.Value });
        }

        /// <summary>Retrieve a specific session property by key.</summary>
        [HttpGet("property/{key}")]
        public IActionResult GetProperty(string key)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { message = "Session not found" });

            var value = session.GetProperty(key);
            if (value == null)
                return NotFound(new { message = $"Property '{key}' not found" });

            return Ok(new { key, value });
        }

        /// <summary>Remove a mutable property from the current session.</summary>
        [HttpDelete("property/{key}")]
        public IActionResult DeleteProperty(string key)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { message = "Session not found" });

            session.RemoveProperty(key);
            return Ok(new { success = true, message = $"Property '{key}' removed" });
        }

        public class SessionPropertyRequest
        {
            public string Key   { get; set; } = string.Empty;
            public string? Value { get; set; }
        }
    }
}
