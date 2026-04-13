// PetelATH.BlazorServer/Components/Pages/SecurePageBase.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PetelATH.BlazorServer.Services;

namespace PetelATH.BlazorServer.Components.Pages
{
    /// <summary>
    /// Base class for all authenticated pages with automatic page-level security
    /// Verifies page access on load and provides helper methods for secure actions
    /// </summary>
    public abstract class SecurePageBase : ComponentBase
    {
        [Inject] protected ActionSecurityService SecurityService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        /// <summary>
        /// Page identifier for security checks (e.g., "students", "schooldetails")
        /// MUST be implemented by derived classes
        /// </summary>
        protected abstract string PageName { get; }

        /// <summary>
        /// Override OnInitializedAsync to add page-level security
        /// Verifies user has access to this page, redirects if denied
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Verify user has access to this page
                var allowed = await SecurityService.VerifyPageAccessAsync(PageName);

                if (!allowed)
                {
                    Console.WriteLine($"🚫 Page access denied: {PageName}");
                    
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("alert", "אין לך הרשאה לגשת לעמוד זה");
                        await JSRuntime.InvokeVoidAsync("history.back");
                    }
                    catch (JSDisconnectedException)
                    {
                        // Circuit disconnected - user likely navigated away
                        Console.WriteLine("Circuit disconnected during access denial");
                    }
                    catch (TaskCanceledException)
                    {
                        // Operation cancelled
                        Console.WriteLine("JS interop cancelled during access denial");
                    }
                    return;
                }

                Console.WriteLine($"✅ Page access granted: {PageName}");

                // Call derived class initialization
                await OnPageInitializedAsync();
            }
            catch (JSDisconnectedException ex)
            {
                // Circuit disconnected - this is normal during disposal
                Console.WriteLine($"Circuit disconnected in page initialization: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                // Operation cancelled - this is normal
                Console.WriteLine($"Page initialization cancelled: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in page initialization: {ex.Message}");
                
                try
                {
                    await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת העמוד");
                    await JSRuntime.InvokeVoidAsync("history.back");
                }
                catch (JSDisconnectedException)
                {
                    // Ignore - circuit already disconnected
                }
                catch (TaskCanceledException)
                {
                    // Ignore - operation cancelled
                }
            }
        }

        /// <summary>
        /// Override this instead of OnInitializedAsync in derived classes
        /// Called after page access has been verified
        /// </summary>
        protected virtual Task OnPageInitializedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper: Execute action with automatic security check
        /// Returns true if action was executed, false if denied
        /// </summary>
        /// <param name="actionName">Action identifier (e.g., "students_deleteStudent")</param>
        /// <param name="functionName">Function name being executed</param>
        /// <param name="action">Action to execute if allowed</param>
        /// <param name="actionParams">Optional parameters (e.g., "studentId=123")</param>
        /// <returns>True if executed, false if denied</returns>
        protected async Task<bool> ExecuteSecureActionAsync(
            string actionName,
            string functionName,
            Func<Task> action,
            string? actionParams = null)
        {
            try
            {
                var allowed = await SecurityService.VerifyActionAsync(
                    actionName: actionName,
                    screenName: PageName,
                    functionName: functionName,
                    eventType: "PAGE_ACTION",
                    actionType: 7, // Type 7 = Button/Click action from page
                    reference: PageName, // ✅ Add reference parameter for auto-creation
                    actionParams: actionParams
                );

                if (!allowed)
                {
                    await JSRuntime.InvokeVoidAsync("alert", 
                        SecurityService.GetAccessDeniedMessage(actionName));
                    return false;
                }

                await action();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing secure action: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בביצוע הפעולה");
                return false;
            }
        }

        /// <summary>
        /// Helper: Execute action with automatic security check and return result
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="actionName">Action identifier</param>
        /// <param name="functionName">Function name being executed</param>
        /// <param name="action">Action to execute if allowed</param>
        /// <param name="actionParams">Optional parameters</param>
        /// <returns>Tuple: (success, result)</returns>
        protected async Task<(bool success, T? result)> ExecuteSecureActionAsync<T>(
            string actionName,
            string functionName,
            Func<Task<T>> action,
            string? actionParams = null)
        {
            try
            {
                var allowed = await SecurityService.VerifyActionAsync(
                    actionName: actionName,
                    screenName: PageName,
                    functionName: functionName,
                    eventType: "PAGE_ACTION",
                    actionType: 7, // Type 7 = Button/Click action from page
                    reference: PageName, // ✅ Add reference parameter for auto-creation
                    actionParams: actionParams
                );

                if (!allowed)
                {
                    await JSRuntime.InvokeVoidAsync("alert", 
                        SecurityService.GetAccessDeniedMessage(actionName));
                    return (false, default(T));
                }

                var result = await action();
                return (true, result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing secure action: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בביצוע הפעולה");
                return (false, default(T));
            }
        }
    }
}
