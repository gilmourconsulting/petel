using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Table security management following multi-tenant request flow
    /// Inherits from BaseController for tenant isolation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TableSecurityController : BaseController
    {
        private readonly UserSessionService _userSessionService;
        private readonly ILogger<TableSecurityController> _logger;

        // Fix constructor - BaseController doesn't take parameters
        public TableSecurityController(UserSessionService userSessionService, ILogger<TableSecurityController> logger)
        {
            _userSessionService = userSessionService;
            _logger = logger;
        }

        [HttpGet("canRead/{tableName}")]
        public IActionResult CanRead(string tableName)
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

                // Fix: Use session.UserId (string) instead of converting to int
                var canRead = CheckTablePermission(session.UserId, tableName, "read");
                
                return Ok(new { success = true, canRead = canRead });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking read permission for table {TableName}", tableName);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("canWrite/{tableName}")]
        public IActionResult CanWrite(string tableName)
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

                // Fix: Use session.UserId (string) instead of converting to int
                var canWrite = CheckTablePermission(session.UserId, tableName, "write");
                
                return Ok(new { success = true, canWrite = canWrite });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking write permission for table {TableName}", tableName);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("userRoles")]
        public IActionResult GetUserRoles()
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

                // Fix: Use session.Roles directly
                var roles = session.Roles;
                
                return Ok(new { success = true, roles = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user roles");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Fix: Change parameter type from int to string
        private bool CheckTablePermission(string userId, string tableName, string permission)
        {
            // Implement actual permission checking logic here
            // For now, return true for basic tables
            var allowedTables = new[] { "students", "schools", "systemattributes", "hours_budget" };
            return allowedTables.Contains(tableName.ToLower());
        }
    }
}