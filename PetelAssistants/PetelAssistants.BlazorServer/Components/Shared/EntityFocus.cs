using Microsoft.JSInterop;

namespace PetelAssistants.BlazorServer.Components.Shared
{
    /// <summary>
    /// Year-scoped deep links that land on a specific table row.
    /// URL: /year/{yearId}/{screen}?focusId={id}
    /// </summary>
    public static class EntityFocus
    {
        public const string QueryParam = "focusId";
        public const string AllocationQueryParam = "allocationFocusId";
        public const string AssistantsScreen = "assistants";
        public const string EntitlementsScreen = "entitlements";

        public static string RowElementId(int id) => $"entity-row-{id}";

        public static string ToYearScreen(string screen, int yearId, int id) =>
            $"/year/{yearId}/{screen}?{QueryParam}={id}";

        public static string ToAssistants(int yearId, int id) =>
            ToYearScreen(AssistantsScreen, yearId, id);

        public static string ToEntitlements(int yearId, int id) =>
            ToYearScreen(EntitlementsScreen, yearId, id);

        public static string ToEntitlementsWithAllocation(int yearId, int entitlementId, int allocationId) =>
            $"/year/{yearId}/{EntitlementsScreen}?{QueryParam}={entitlementId}&{AllocationQueryParam}={allocationId}";

        public static async Task ScrollToRowAsync(IJSRuntime js, int id)
        {
            try
            {
                await js.InvokeVoidAsync("BlazorHelpers.scrollIntoView", RowElementId(id));
            }
            catch (JSDisconnectedException) { }
            catch (TaskCanceledException) { }
        }
    }
}
