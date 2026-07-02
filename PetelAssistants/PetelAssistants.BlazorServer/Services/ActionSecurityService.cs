using Petel.BlazorCore.Services;
using PetelAssistants.BlazorServer.Models;

namespace PetelAssistants.BlazorServer.Services
{
    /// <summary>
    /// Blazor-side action security service.
    /// Delegates all authorization decisions to the API (api/security/verify-action-secure).
    /// FAIL-SECURE: returns false on any error.
    /// </summary>
    public class ActionSecurityService
    {
        private readonly ApiService _apiService;
        private readonly AuthenticationService _authService;
        private readonly ILogger<ActionSecurityService> _logger;

        public ActionSecurityService(
            ApiService apiService,
            AuthenticationService authService,
            ILogger<ActionSecurityService> logger)
        {
            _apiService = apiService;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>Verify a button/action with full audit logging on the server side.</summary>
        public async Task<bool> VerifyActionAsync(
            string actionName,
            string screenName,
            string functionName,
            string eventType = "BUTTON_CLICK",
            string? reference = null,
            string? actionParams = null,
            string? description = null)
        {
            try
            {
                var request = new SecureActionRequest
                {
                    ActionName   = actionName,
                    ScreenName   = screenName,
                    FunctionName = functionName,
                    EventType    = eventType,
                    Reference    = reference,
                    ActionParams = actionParams,
                    Description  = description
                };

                var response = await _apiService.PostAsync<SecureActionRequest, SecureActionResponse>(
                    "security/verify-action-secure", request);

                return response?.Allowed == true;
            }
            catch (HttpStatusException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Authentication failed for action {Action} — redirecting to login", actionName);
                await _authService.LogoutAsync();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action {Action}", actionName);
                return false;
            }
        }

        public Task<bool> VerifyMenuNavigationAsync(string menuItemName, string menuReference)
            => VerifyActionAsync(
                actionName:   menuItemName,
                screenName:   "menu",
                functionName: "navigateTo",
                eventType:    "MENU_NAVIGATION",
                reference:    menuReference,
                actionParams: menuReference);

        public Task<bool> VerifyPageAccessAsync(string pageName)
            => VerifyActionAsync(
                actionName:   pageName,
                screenName:   "navigation",
                functionName: "accessPage",
                eventType:    "PAGE_ACCESS",
                reference:    pageName);

        public string GetAccessDeniedMessage(string actionName)
            => $"אין לך הרשאה לפעולה זו: {actionName}";

        public string GetGenericAccessDeniedMessage()
            => "אין לך הרשאה לפעולה זו";
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

    public class SecureActionResponse
    {
        public bool    Success  { get; set; }
        public bool    Allowed  { get; set; }
        public string? Message  { get; set; }
    }
}
