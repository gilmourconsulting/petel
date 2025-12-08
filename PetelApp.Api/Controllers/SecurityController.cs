// PetelApp.Api/Controllers/SecurityController.cs
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : BaseController
    {
        private readonly ActionAuthorizationService _actionAuthService;

        public SecurityController(
            UserSessionService sessionService,
            ActionAuthorizationService actionAuthService,
            ILogger<SecurityController> logger)
            : base(sessionService, logger)
        {
            _actionAuthService = actionAuthService;
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