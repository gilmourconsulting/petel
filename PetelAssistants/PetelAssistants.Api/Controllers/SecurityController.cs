using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : BaseController
    {
        private readonly ActionAuthorizationService _authService;
        private readonly AssistDbContext _context;

        public SecurityController(
            ActionAuthorizationService authService,
            AssistDbContext context,
            UserSessionService userSessionService,
            ILogger<SecurityController> logger)
            : base(userSessionService, logger)
        {
            _authService = authService;
            _context = context;
        }

        /// <summary>Verify action and log to audit trail.</summary>
        [HttpPost("verify-action-secure")]
        public async Task<IActionResult> VerifyActionSecure([FromBody] SecureActionRequest request)
        {
            var userId = 0;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            _logger.LogInformation("VerifyActionSecure called: Action={ActionName} EventType={EventType} Screen={Screen}",
                request.ActionName, request.EventType, request.ScreenName);

            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("VerifyActionSecure: session is null — returning 401 for action '{ActionName}'", request.ActionName);
                    return Unauthorized(new { success = false, allowed = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.UserId, out userId))
                {
                    _logger.LogWarning("VerifyActionSecure: cannot parse UserId '{UserId}' — returning 400", session.UserId);
                    return BadRequest(new { success = false, allowed = false, message = "מזהה משתמש לא תקין" });
                }

                if (!int.TryParse(session.EntityId, out int entityId) || entityId == 0)
                {
                    _logger.LogWarning("VerifyActionSecure: cannot parse EntityId '{EntityId}' — returning 400", session.EntityId);
                    return BadRequest(new { success = false, allowed = false, message = "מזהה רשות לא תקין" });
                }

                if (string.IsNullOrEmpty(request.ActionName))
                {
                    _logger.LogWarning("VerifyActionSecure: ActionName is empty — returning 400");
                    return BadRequest(new { success = false, allowed = false, message = "שם פעולה חסר" });
                }

                var hasAccess = await _authService.VerifyActionByNameAsync(
                    userId,
                    entityId,
                    request.ActionName,
                    request.EventType ?? "BUTTON_CLICK",
                    request.ScreenName ?? "",
                    request.Reference);

                await LogAuditAsync(session, userId, request, hasAccess ? "GRANTED" : "DENIED", ipAddress);

                return Ok(new
                {
                    success = true,
                    allowed = hasAccess,
                    message = hasAccess ? null : "אין הרשאה לפעולה זו"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action for user {UserId}", userId);
                await LogAuditAsync(null, userId, request, "ERROR", ipAddress);
                return StatusCode(500, new { success = false, allowed = false, message = "שגיאה בבדיקת הרשאה" });
            }
        }

        private async Task LogAuditAsync(
            UserSession? session,
            int userId,
            SecureActionRequest request,
            string result,
            string? ipAddress)
        {
            try
            {
                if (!int.TryParse(session?.EntityId, out int entityId) || entityId == 0)
                    return;

                var log = new ActionAuditLog
                {
                    EntityId      = entityId,
                    UserId        = userId > 0 ? userId : null,
                    ActionName    = request.ActionName ?? "unknown",
                    ScreenName    = request.ScreenName,
                    FunctionName  = request.FunctionName,
                    EventType     = request.EventType ?? "UNKNOWN",
                    Result        = result,
                    ActionParams  = request.ActionParams,
                    Description   = request.Description,
                    IpAddress     = ipAddress,
                    Timestamp     = DateTime.UtcNow
                };

                _context.ActionAuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log for user {UserId}", userId);
            }
        }
    }

    public class SecureActionRequest
    {
        public string  ActionName   { get; set; } = string.Empty;
        public string? ScreenName   { get; set; }
        public string? FunctionName { get; set; }
        public string? EventType    { get; set; }
        public string? Reference    { get; set; }
        public string? ActionParams { get; set; }
        public string? Description  { get; set; }
    }
}
