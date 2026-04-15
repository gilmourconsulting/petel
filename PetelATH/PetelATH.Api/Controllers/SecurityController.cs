// PetelATH.Api/Controllers/SecurityController.cs
using Microsoft.AspNetCore.Mvc;
using PetelATH.Api.Services;
using PetelATH.Api.Session;
using PetelATH.Api.Data;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : BaseController
    {
        private readonly ActionAuthorizationService _actionAuthService;
        private readonly IServiceProvider _serviceProvider;

        public SecurityController(
            UserSessionService sessionService,
            ActionAuthorizationService actionAuthService,
            IServiceProvider serviceProvider,
            ILogger<SecurityController> logger)
            : base(sessionService, logger)
        {
            _actionAuthService = actionAuthService;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Get all actions available to the current user based on their roles
        /// Supports caching on frontend
        /// </summary>
        [HttpGet("user-actions")]
        public async Task<IActionResult> GetUserActions()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var userActions = await _actionAuthService.GetUserActionsAsync(int.Parse(session.UserId));
                return Ok(userActions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user actions");
                return StatusCode(500, new { success = false, message = "שגיאה בשליפת הרשאות" });
            }
        }

        /// <summary>
        /// Verify onclick action access by screen name and function name
        /// NEW: For frontend button/onclick interception
        /// </summary>
        [HttpPost("verify-onclick")]
        public async Task<IActionResult> VerifyOnclickAccess([FromBody] OnclickAccessRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (string.IsNullOrEmpty(request.ScreenName) || string.IsNullOrEmpty(request.FunctionName))
                {
                    return BadRequest(new { success = false, message = "שם מסך או שם פונקציה חסר" });
                }

                var hasAccess = await _actionAuthService.VerifyOnclickAccessAsync(
                    int.Parse(session.UserId),
                    request.ScreenName,
                    request.FunctionName);

                return Ok(new { success = hasAccess, allowed = hasAccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying onclick access");
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת הרשאה" });
            }
        }

        /// <summary>
        /// Verify menu item access by name
        /// </summary>
        [HttpPost("verify-menu")]
        public async Task<IActionResult> VerifyMenuItemAccess([FromBody] MenuAccessRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (string.IsNullOrEmpty(request.MenuItemName))
                {
                    return BadRequest(new { success = false, message = "שם פריט תפריט חסר" });
                }

                var hasAccess = await _actionAuthService.VerifyMenuItemAccessAsync(
                    int.Parse(session.UserId),
                    request.MenuItemName);

                return Ok(new { success = hasAccess, allowed = hasAccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying menu access");
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת הרשאה" });
            }
        }

      

        /// <summary>
        /// Verify action access by action ID
        /// </summary>
        [HttpPost("verify-action")]
        public async Task<IActionResult> VerifyActionAccess([FromBody] ActionAccessRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (request.ActionId <= 0)
                {
                    return BadRequest(new { success = false, message = "מזהה אקשן חסר" });
                }

                var hasAccess = await _actionAuthService.VerifyUserActionAccessAsync(
                    int.Parse(session.UserId),
                    request.ActionId);

                return Ok(new { success = hasAccess, allowed = hasAccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action access");
                return StatusCode(500, new { success = false, message = "שגיאה בבדיקת הרשאה" });
            }
        }
    

    
            /// <summary>
            /// SECURE: Verify action and log audit trail
            /// This is the ONLY authorization endpoint - handles verification AND logging
            /// Frontend cannot bypass audit logging since it's done server-side
            /// </summary>
            [HttpPost("verify-action-secure")]
            public async Task<IActionResult> VerifyActionSecure([FromBody] SecureActionRequest request)
            {
                var userId = 0;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                
                try
                {
                    var session = GetCurrentSession();
                    if (session == null)
                    {
                        _logger.LogWarning("Authorization attempt without valid session from IP: {IP}", ipAddress);
                        return Unauthorized(new { success = false, allowed = false, message = "נדרש אימות" });
                    }
    
                    userId = int.Parse(session.UserId);
    
                    if (string.IsNullOrEmpty(request.ActionName))
                    {
                        return BadRequest(new { success = false, allowed = false, message = "שם פעולה חסר" });
                    }
    
                    // ✅ STEP 1: Verify authorization (using existing service)
                    bool hasAccess;
                    
                    if (request.EventType == "MENU_NAVIGATION")
                    {
                        hasAccess = await _actionAuthService.VerifyMenuItemAccessAsync(userId, request.ActionName);
                    }
                    else if (request.EventType == "ONCLICK_BUTTON" || request.EventType == "BUTTON_CLICK")
                    {
                        hasAccess = await _actionAuthService.VerifyOnclickAccessAsync(
                            userId, 
                            request.ScreenName ?? "unknown", 
                            request.FunctionName ?? "unknown"
                        );
                    }
                    else
                    {
                        // Generic action verification by action name - pass actionType and reference
                        hasAccess = await _actionAuthService.VerifyActionByNameAsync(
                            userId, 
                            request.ActionName, 
                            request.ActionType, 
                            request.Reference
                        );
                    }
    
                    // ✅ STEP 2: Log to audit trail (server-side, cannot be bypassed)
                    await LogAuditTrailAsync(
                        userId,
                        request.ActionName,
                        request.ScreenName ?? "unknown",
                        request.FunctionName ?? "unknown",
                        request.EventType ?? "UNKNOWN",
                        hasAccess ? "GRANTED" : "DENIED",
                        request.ActionParams,
                        request.Description,
                        ipAddress
                    );
    
                    // ✅ STEP 3: Return result to frontend
                    return Ok(new
                    {
                        success = true,
                        allowed = hasAccess,
                        message = hasAccess ? null : "אין הרשאה לפעולה זו"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in secure action verification for user {UserId}", userId);
                    
                    // Log failed verification attempt
                    await LogAuditTrailAsync(
                        userId,
                        request.ActionName ?? "unknown",
                        request.ScreenName ?? "unknown",
                        request.FunctionName ?? "unknown",
                        request.EventType ?? "ERROR",
                        "ERROR",
                        request.ActionParams,
                        $"Exception: {ex.Message}",
                        ipAddress
                    );
    
                    return StatusCode(500, new
                    {
                        success = false,
                        allowed = false,
                        message = "שגיאה בבדיקת הרשאה"
                    });
                }
            }
    
            /// <summary>
            /// Private helper: Log audit trail entry
            /// Isolated for reusability and to ensure all logs follow same format
            /// </summary>
            private async Task LogAuditTrailAsync(
                int userId,
                string actionName,
                string screenName,
                string functionName,
                string eventType,
                string result,
                string? actionParams,
                string? description,
                string? ipAddress)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
                    var auditLog = new ActionAuditLog
                    {
                        UserId = userId,
                        ActionName = actionName,
                        ScreenName = screenName,
                        FunctionName = functionName,
                        EventType = eventType,
                        Result = result,
                        ActionParams = actionParams,
                        Description = description,
                        Timestamp = DateTime.UtcNow,
                        IpAddress = ipAddress
                    };
    
                    context.ActionAuditLogs.Add(auditLog);
                    await context.SaveChangesAsync();
    
                    _logger.LogInformation(
                        "Audit: User={UserId} Type={EventType} Action={Action} Result={Result} Params={Params}",
                        userId, eventType, actionName, result, actionParams ?? "none"
                    );
                }
                catch (Exception ex)
                {
                    // Don't fail the request if audit logging fails, but log the error
                    _logger.LogError(ex, "Failed to write audit log for user {UserId}, action {Action}", userId, actionName);
                }
            }
    }
        // Add DTOs at end of file (before closing namespace):
        
        public class SecureActionRequest
        {
            public string ActionName { get; set; } = string.Empty;
            public string? ScreenName { get; set; }
            public string? FunctionName { get; set; }
            public string? EventType { get; set; }
            public int ActionType { get; set; } = 7; // 7 = Button/Click, 8 = Page/Screen
            public string? Reference { get; set; } // Optional reference field (page URL, menu href, etc.)
            public string? ActionParams { get; set; }
            public string? Description { get; set; }
        }

    // ============================================================================
    // DTOs for Security Requests
    // ============================================================================

    /// <summary>
    /// Request to verify onclick action access
    /// NEW: For frontend button/onclick interception
    /// </summary>
    public class OnclickAccessRequest
    {
        public string ScreenName { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to verify menu item access
    /// </summary>
    public class MenuAccessRequest
    {
        public string MenuItemName { get; set; } = string.Empty;
    }



    /// <summary>
    /// Request to verify action by ID
    /// </summary>
    public class ActionAccessRequest
    {
        public int ActionId { get; set; }
    }
}