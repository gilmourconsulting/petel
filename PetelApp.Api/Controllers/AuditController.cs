using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Controller for audit logging of action authorization attempts
    /// Records all user action attempts (granted/denied) for security auditing
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : BaseController
    {
        private readonly AppDbContext _context;

        public AuditController(
            AppDbContext context,
            UserSessionService sessionService,
            ILogger<AuditController> logger)
            : base(sessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Log an action authorization attempt
        /// Called from frontend action-security.js after every authorization check
        /// </summary>
         [HttpPost("log")]
        public async Task<IActionResult> LogAction([FromBody] ActionAuditLogDto logEntry)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("Audit log attempted without valid session");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var auditLog = new ActionAuditLog
                {
                    UserId = int.Parse(session.UserId),
                    ActionName = logEntry.ActionName,
                    ScreenName = logEntry.ScreenName,
                    FunctionName = logEntry.FunctionName,
                    EventType = logEntry.EventType,
                    Result = logEntry.Result,
                    ActionParams = logEntry.ActionParams,
                    Description = logEntry.Description,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress
                };

                _context.ActionAuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Audit: User {UserId} - Type {EventType} - Action {ActionName} - Result {Result} - Params {Params}",
                    auditLog.UserId,
                    auditLog.EventType,
                    auditLog.ActionName,
                    auditLog.Result,
                    auditLog.ActionParams ?? "none"
                );

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit entry");
                return Ok(new { success = false, error = "Audit log failed but request continues" });
            }
        }

        /// <summary>
        /// Get audit logs for analysis (admin only in future)
        /// Useful for identifying missing permissions or security issues
        /// </summary>
        [HttpGet("logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string? result = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.ActionAuditLogs
                    .AsNoTracking()
                    .Include(a => a.User)
                    .AsQueryable();

                // Apply filters
                if (userId.HasValue)
                    query = query.Where(a => a.UserId == userId.Value);

                if (!string.IsNullOrEmpty(result))
                    query = query.Where(a => a.Result == result);

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Get paginated results
                var logs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        id = a.Id,
                        userId = a.UserId,
                        username = a.User != null ? a.User.Username : "Unknown",
                        actionName = a.ActionName,
                        screenName = a.ScreenName,
                        functionName = a.FunctionName,
                        eventType = a.EventType,
                        result = a.Result,
                        timestamp = a.Timestamp,
                        ipAddress = a.IpAddress
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת לוגים" });
            }
        }

        /// <summary>
        /// Get denied access summary - useful for identifying missing permissions
        /// </summary>
        [HttpGet("denied-summary")]
        public async Task<IActionResult> GetDeniedAccessSummary([FromQuery] DateTime? startDate = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.ActionAuditLogs
                    .AsNoTracking()
                    .Where(a => a.Result == "DENIED");

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                var summary = await query
                    .GroupBy(a => new { a.UserId, a.ActionName })
                    .Select(g => new
                    {
                        userId = g.Key.UserId,
                        actionName = g.Key.ActionName,
                        deniedCount = g.Count(),
                        lastAttempt = g.Max(a => a.Timestamp)
                    })
                    .OrderByDescending(s => s.deniedCount)
                    .ToListAsync();

                return Ok(new { success = true, summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting denied access summary");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת סיכום" });
            }
        }
    }
}