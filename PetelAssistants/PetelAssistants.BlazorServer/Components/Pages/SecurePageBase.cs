// PetelAssistants.BlazorServer/Components/Pages/SecurePageBase.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Petel.BlazorCore.Services;

namespace PetelAssistants.BlazorServer.Components.Pages
{
    /// <summary>
    /// Base class for all authenticated pages.
    /// Verifies the user has an active session before calling OnPageInitializedAsync.
    /// </summary>
    public abstract class SecurePageBase : ComponentBase
    {
        [Inject] protected NavigationManager Navigation        { get; set; } = default!;
        [Inject] protected IJSRuntime         JSRuntime        { get; set; } = default!;
        [Inject] protected SessionStateService SessionState    { get; set; } = default!;

        /// <summary>Page identifier used for logging/debugging.</summary>
        protected abstract string PageName { get; }

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

                await OnPageInitializedAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initialising page {PageName}: {ex.Message}");
                Navigation.NavigateTo("/login");
            }
        }

        /// <summary>
        /// Override this instead of OnInitializedAsync in derived pages.
        /// Called only after the session has been verified.
        /// </summary>
        protected virtual Task OnPageInitializedAsync() => Task.CompletedTask;
    }
}
