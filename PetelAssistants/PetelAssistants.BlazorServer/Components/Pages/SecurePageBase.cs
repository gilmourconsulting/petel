// PetelAssistants.BlazorServer/Components/Pages/SecurePageBase.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Petel.BlazorCore.Services;
using PetelAssistants.BlazorServer.Services;

namespace PetelAssistants.BlazorServer.Components.Pages
{
    /// <summary>
    /// Base class for all authenticated pages.
    /// Verifies session then enforces page-level action access before calling OnPageInitializedAsync.
    /// </summary>
    public abstract class SecurePageBase : ComponentBase
    {
        [Inject] protected NavigationManager    Navigation    { get; set; } = default!;
        [Inject] protected IJSRuntime           JSRuntime     { get; set; } = default!;
        [Inject] protected SessionStateService  SessionState  { get; set; } = default!;
        [Inject] protected ActionSecurityService ActionSecurity { get; set; } = default!;

        /// <summary>Page identifier used for logging/debugging and action lookup.</summary>
        protected abstract string PageName { get; }

        /// <summary>
        /// Set to false in pages that do not have a corresponding entry in the actions table
        /// and should bypass page-level access enforcement.
        /// </summary>
        protected virtual bool EnforcePageAccess => true;

        protected sealed override async Task OnInitializedAsync()
        {
            try
            {
                var session = await SessionState.GetSessionAsync();
                if (session == null)
                {
                    Navigation.NavigateTo("/login");
                    return;
                }

                if (EnforcePageAccess)
                {
                    var allowed = await ActionSecurity.VerifyPageAccessAsync(PageName);
                    if (!allowed)
                    {
                        Console.WriteLine($"Page access denied: {PageName}");
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("alert", "אין לך הרשאה לגשת לעמוד זה");
                            await JSRuntime.InvokeVoidAsync("history.back");
                        }
                        catch (JSDisconnectedException) { }
                        catch (TaskCanceledException) { }
                        return;
                    }
                }

                await OnPageInitializedAsync();
            }
            catch (JSDisconnectedException ex)
            {
                Console.WriteLine($"Circuit disconnected in page initialisation: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Page initialisation cancelled: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initialising page {PageName}: {ex.Message}");
                try
                {
                    await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת העמוד");
                    await JSRuntime.InvokeVoidAsync("history.back");
                }
                catch (JSDisconnectedException) { }
                catch (TaskCanceledException) { }
            }
        }

        /// <summary>
        /// Override this instead of OnInitializedAsync in derived pages.
        /// Called only after session and page access have been verified.
        /// </summary>
        protected virtual Task OnPageInitializedAsync() => Task.CompletedTask;
    }
}
