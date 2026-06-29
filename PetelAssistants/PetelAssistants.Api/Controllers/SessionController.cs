using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : BaseController
    {
        public SessionController(
            UserSessionService userSessionService,
            ILogger<SessionController> logger)
            : base(userSessionService, logger)
        {
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
                userId        = session.UserId,
                username      = session.Username,
                userFullName  = session.UserFullName,
                entityId      = session.EntityId,
                entityName    = session.EntityName,
                entityTypeId  = session.EntityTypeId,
                entityTypeName = session.EntityTypeName,
                sessionId     = session.SessionId,
                properties    = session.GetAllProperties()
            });
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

        public class SessionPropertyRequest
        {
            public string Key   { get; set; } = string.Empty;
            public string? Value { get; set; }
        }
    }
}
