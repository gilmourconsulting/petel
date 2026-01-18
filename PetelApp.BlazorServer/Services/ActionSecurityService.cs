// PetelApp.BlazorServer/Services/ActionSecurityService.cs
using PetelApp.BlazorServer.DTOs;

namespace PetelApp.BlazorServer.Services
{
    /// <summary>
    /// Blazor Server action security service
    /// Wraps all authorization checks and ensures audit logging
    /// FAIL-SECURE: Returns false on any error
    /// </summary>
    public class ActionSecurityService
    {
        private readonly ApiService _apiService;
        private readonly SessionStateService _sessionState;
        private readonly ILogger<ActionSecurityService> _logger;

        public ActionSecurityService(
            ApiService apiService,
            SessionStateService sessionState,
            ILogger<ActionSecurityService> logger)
        {
            _apiService = apiService;
            _sessionState = sessionState;
            _logger = logger;
        }

        /// <summary>
        /// Verify button/action access with audit logging
        /// Returns true if allowed, false if denied
        /// FAIL-SECURE: Returns false on any error
        /// </summary>
        /// <param name="actionName">Action identifier (e.g., "students_addStudent")</param>
        /// <param name="screenName">Screen/page name where action is performed</param>
        /// <param name="functionName">Function name being executed</param>
        /// <param name="eventType">Event type: BUTTON_CLICK, MENU_NAVIGATION, PAGE_ACCESS, etc.</param>
        /// <param name="actionType">Action type: 7 = Button/Click, 8 = Page/Screen (default 7)</param>
        /// <param name="reference">Optional reference field (e.g., page URL, menu href)</param>
        /// <param name="actionParams">Optional parameters (e.g., "studentId=123")</param>
        /// <param name="description">Optional description for audit trail</param>
        /// <returns>True if allowed, false if denied or error</returns>
        public async Task<bool> VerifyActionAsync(
            string actionName,
            string screenName,
            string functionName,
            string eventType = "BUTTON_CLICK",
            int actionType = 7,
            string? reference = null,
            string? actionParams = null,
            string? description = null)
        {
            try
            {
                _logger.LogDebug("🔐 Verifying action: {Action} ({EventType})", 
                    actionName, eventType);

                var request = new SecureActionRequest
                {
                    ActionName = actionName,
                    ScreenName = screenName,
                    FunctionName = functionName,
                    EventType = eventType,
                    ActionType = actionType,
                    Reference = reference,
                    ActionParams = actionParams,
                    Description = description
                };

                var response = await _apiService.PostAsync<SecureActionRequest, SecureActionResponse>(
                    "security/verify-action-secure",
                    request
                );

                if (response?.Allowed == true)
                {
                    _logger.LogDebug("✅ Action allowed: {Action}", actionName);
                    return true;
                }

                _logger.LogWarning("🚫 Action denied: {Action} - {Message}", 
                    actionName, response?.Message ?? "No permission");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error verifying action: {Action}", actionName);
                // FAIL-SECURE: Deny on error
                return false;
            }
        }

        /// <summary>
        /// Verify menu navigation access
        /// Backend already filters menu items, but this provides additional security layer
        /// </summary>
        /// <param name="menuItemName">Menu item name (e.g., "students", "maindashboard")</param>
        /// <param name="menuReference">Menu reference/href (e.g., "/students")</param>
        /// <returns>True if allowed, false if denied</returns>
        public async Task<bool> VerifyMenuNavigationAsync(string menuItemName, string menuReference)
        {
            return await VerifyActionAsync(
                actionName: menuItemName,
                screenName: "menu",
                functionName: "navigateTo",
                eventType: "MENU_NAVIGATION",
                actionType: 8,
                reference: menuReference,
                actionParams: menuReference
            );
        }

        /// <summary>
        /// Verify page access (for direct URL navigation)
        /// Called by SecurePageBase on page load
        /// </summary>
        /// <param name="pageName">Page name (e.g., "students", "schooldetails")</param>
        /// <returns>True if allowed, false if denied</returns>
        public async Task<bool> VerifyPageAccessAsync(string pageName)
        {
            return await VerifyActionAsync(
                actionName: pageName,
                screenName: "navigation",
                functionName: "accessPage",
                eventType: "PAGE_ACCESS",
                actionType: 8,
                reference: pageName // ✅ Remove "/" prefix to fix database reference field
            );
        }

        /// <summary>
        /// Get localized access denied message
        /// </summary>
        /// <param name="actionName">Action that was denied</param>
        /// <returns>Hebrew access denied message</returns>
        public string GetAccessDeniedMessage(string actionName)
        {
            return $"אין לך הרשאה לפעולה זו: {actionName}";
        }

        /// <summary>
        /// Get generic access denied message (no action name)
        /// </summary>
        /// <returns>Hebrew access denied message</returns>
        public string GetGenericAccessDeniedMessage()
        {
            return "אין לך הרשאה לפעולה זו";
        }
    }
}
