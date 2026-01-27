// PetelApp.BlazorServer/DTOs/SecurityDTOs.cs
namespace PetelApp.BlazorServer.DTOs
{
    /// <summary>
    /// Request DTO for secure action verification
    /// Sent to backend /api/security/verify-action-secure endpoint
    /// </summary>
    public class SecureActionRequest
    {
        /// <summary>
        /// Action identifier (e.g., "students_addStudent" or "maindashboard" for menu)
        /// </summary>
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// Screen/page name where action is being performed
        /// </summary>
        public string? ScreenName { get; set; }

        /// <summary>
        /// Function name being executed
        /// </summary>
        public string? FunctionName { get; set; }

        /// <summary>
        /// Event type: BUTTON_CLICK, MENU_NAVIGATION, PAGE_ACCESS, etc.
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Action type: 7 = Button/Click, 8 = Page/Screen (default 7)
        /// </summary>
        public int ActionType { get; set; } = 7;

        /// <summary>
        /// Optional reference field (e.g., page URL, menu href)
        /// </summary>
        public string? Reference { get; set; }

        /// <summary>
        /// Optional parameters being passed to the action (e.g., "studentId=123")
        /// </summary>
        public string? ActionParams { get; set; }

        /// <summary>
        /// Optional description for audit trail
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Response DTO from backend security verification
    /// </summary>
    public class SecureActionResponse
    {
        /// <summary>
        /// Whether the API call succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Whether the action is allowed for this user
        /// </summary>
        public bool Allowed { get; set; }

        /// <summary>
        /// Optional message (usually present if denied)
        /// </summary>
        public string? Message { get; set; }
    }
}
