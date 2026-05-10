---
applyTo: '**/*.razor'
---

# Blazor Server Patterns — PetelATH & PetelAssistants

All frontend UI in this solution is **pure Blazor Server** with `@rendermode InteractiveServer`. There are no HTML files, no JavaScript SPA, and no `page-lifecycle-config.js`.

## Canonical Page Template

Every authenticated page follows this structure:

```razor
@page "/pagename"
@layout MainLayout
@inherits SecurePageBase
@using PetelATH.BlazorServer.DTOs
@inject ApiService ApiService
@inject SessionStateService SessionStateService
@inject NavigationManager Navigation

<div class="page-container">
    <!-- Context buttons (between menu and main content) -->
    <div class="context-buttons-section">
        <SecureButton ActionName="pagename_actionName"
                      ScreenName="@PageName"
                      FunctionName="DoAction"
                      OnClick="DoAction"
                      CssClass="context-btn"
                      HideIfNoAccess="true">
            <img src="/images/Plus icon.png" alt="הוסף" class="action-icon-natural" />
            כותרת פעולה
        </SecureButton>
    </div>

    <div class="main-content">
        <h1>כותרת עמוד</h1>

        @if (_isLoading)
        {
            <p>טוען...</p>
        }
        else if (_items == null)
        {
            <p>שגיאה בטעינת הנתונים</p>
        }
        else
        {
            <!-- Filter bar -->
            <div class="filter-bar">
                <input type="text" @bind="_filterText" @bind:event="oninput"
                       placeholder="סנן לפי שם..." />
            </div>

            <!-- Table -->
            <div class="table-container">
                <table class="data-table">
                    <thead>
                        <tr>
                            <th>פעולות</th>
                            <th @onclick='() => SortTable("Name")' style="cursor:pointer">
                                שם @GetSortArrow("Name")
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var item in FilteredItems)
                        {
                            <tr>
                                <td>
                                    <button class="btn-icon" @onclick="() => ViewItem(item.Id)" title="צפייה">
                                        <img src="/images/view_icon.png" alt="צפייה" class="action-icon-natural" />
                                    </button>
                                </td>
                                <td>@item.Name</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

<!-- Modals -->
<MyModal @ref="_myModal" OnComplete="RefreshData" />

@code {
    // REQUIRED by SecurePageBase
    protected override string PageName => "pagename";

    // State
    private List<MyItemDto>? _items;
    private bool _isLoading = true;
    private string _filterText = "";
    private string _sortColumn = "";
    private bool _sortAscending = true;
    private MyModal? _myModal;

    // Filtered + sorted list
    private IEnumerable<MyItemDto> FilteredItems => (_items ?? [])
        .Where(i => string.IsNullOrWhiteSpace(_filterText) ||
                    i.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

    // OVERRIDE OnPageInitializedAsync — NOT OnInitializedAsync
    protected override async Task OnPageInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _items = await ApiService.GetAsync<List<MyItemDto>>("myendpoint");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshData() => await LoadData();

    private void ViewItem(int id) => Navigation.NavigateTo($"/itemdetails/{id}");

    private void ShowAddModal() => _myModal?.Show();

    // Sorting helpers
    private void SortTable(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }
        // Apply sort to _items
        _items = _sortAscending
            ? [.. (_items ?? []).OrderBy(i => i.Name)]
            : [.. (_items ?? []).OrderByDescending(i => i.Name)];
    }

    private string GetSortArrow(string column)
    {
        if (_sortColumn != column) return "";
        return _sortAscending ? " ▲" : " ▼";
    }

    private async Task DoAction()
    {
        // Called by SecureButton after permission check
        await Task.CompletedTask;
    }
}
```

## SecurePageBase

`SecurePageBase` is the abstract base class for all authenticated pages in `PetelATH.BlazorServer/Components/Pages/SecurePageBase.cs`.

**Critical rules**:
- `protected abstract string PageName { get; }` — **must** be implemented in every page
- Override `OnPageInitializedAsync()` for page init logic — **never** `OnInitializedAsync()` (it's sealed in the base)
- The base verifies page access via `ActionSecurityService.VerifyPageAccessAsync(PageName)` before calling `OnPageInitializedAsync()`
- For secure actions within a page, call `ExecuteSecureActionAsync(actionName, functionName, action)`

```csharp
// Secure action helper (alternative to SecureButton for code-triggered actions)
protected async Task<bool> ExecuteSecureActionAsync(
    string actionName,
    string functionName,
    Func<Task> action,
    string? actionParams = null)
```

## ApiService Call Patterns

`ApiService` is in `shared/Petel.BlazorCore/Services/ApiService.cs` and is registered as `Scoped`.

```csharp
// Authenticated GET
var items = await ApiService.GetAsync<List<ItemDto>>("endpoint");

// Public GET (no auth token required)
var policy = await ApiService.GetPublicAsync<PasswordPolicyDto>("auth/password-policy");

// POST with typed response
var result = await ApiService.PostAsync<CreateRequest, CreateResponse>("endpoint", new CreateRequest { ... });

// PUT
await ApiService.PutAsync<UpdateRequest, object>("endpoint/id", request);

// DELETE
await ApiService.DeleteAsync("endpoint/id");

// DELETE with typed response
var result = await ApiService.DeleteAsync<DeleteResponse>("endpoint/id");

// File download — returns HttpResponseMessage
var response = await ApiService.GetFileAsync("documents/42/download");

// File upload (multipart)
using var content = new MultipartFormDataContent();
content.Add(new StreamContent(fileStream), "file", fileName);
var result = await ApiService.PostMultipartAsync<ImportResult>("items/import", content);
```

**Error handling**: `ApiService` throws `HttpStatusException` on non-2xx responses. Wrap calls in try/catch; check `ex.StatusCode` for specific handling.

## SessionStateService

```csharp
@inject SessionStateService SessionStateService

// Get full session data (cached 1 min)
var session = await SessionStateService.GetSessionAsync();

// Quick accessors (use cached data — call GetSessionAsync first)
var entityId = SessionStateService.GetEntityId();
var userId   = SessionStateService.GetUserId();
var username = SessionStateService.GetUsername();
var name     = SessionStateService.GetEntityName();

// Force refresh
var session = await SessionStateService.GetSessionAsync(forceRefresh: true);

// Clear cache (called on logout)
SessionStateService.ClearSession();
```

## Navigation

```razor
@inject NavigationManager Navigation

@* Programmatic navigation *@
Navigation.NavigateTo("/students");
Navigation.NavigateTo($"/studentdetails/{studentId}");
Navigation.NavigateTo("/login", forceLoad: true);  // full reload

@* Declarative link — use NavLink for active-class support *@
<NavLink href="/students" Match="NavLinkMatch.All">תלמידים</NavLink>
```

Do **not** use JavaScript `window.location` or `history.pushState` for navigation.

## SecureButton Component

`SecureButton` verifies the user has permission before executing a callback.

```razor
<SecureButton ActionName="students_addStudent"
              ScreenName="@PageName"
              FunctionName="ShowAddModal"
              OnClick="ShowAddModal"
              CssClass="context-btn"
              Title="הוסף תלמיד"
              HideIfNoAccess="true">
    <img src="/images/Plus icon.png" alt="הוסף" class="action-icon-natural" />
    הוסף תלמיד
</SecureButton>
```

**Parameters**:
| Parameter | Required | Description |
|---|---|---|
| `ActionName` | ✅ | `"{screenName}_{functionName}"` |
| `ScreenName` | ✅ | Use `@PageName` from SecurePageBase |
| `FunctionName` | ✅ | Name of the method being invoked |
| `OnClick` | ✅ | `EventCallback` — method to execute if allowed |
| `CssClass` | | CSS class (default: `btn btn-primary`) |
| `HideIfNoAccess` | | `true` = hide button; `false` = show disabled |
| `Disabled` | | Disable regardless of security |
| `Title` | | Tooltip |
| `ChildContent` | | Button content (icons, text) |

## Modal Component Pattern

### Consuming page:
```razor
<!-- Declare the modal component -->
<MyModal @ref="_myModal" OnComplete="RefreshData" />

@code {
    private MyModal? _myModal;

    private void ShowAddModal() => _myModal?.Show();
    private void ShowEditModal(int id) => _myModal?.Show(id);

    private async Task RefreshData() => await LoadData();
}
```

### Modal component (`Components/Modals/MyModal.razor`):
```razor
@inject ApiService ApiService
@inject IJSRuntime JSRuntime

@if (_isVisible)
{
    <div class="modal-overlay" style="display: flex;">
        <div class="modal-content">
            <div class="modal-header">
                <div>@_title</div>
                <button class="modal-close" @onclick="CloseModal">&times;</button>
            </div>
            <div class="modal-body">
                <!-- Form fields -->
                <div class="form-group">
                    <label>שם: <span style="color: red;">*</span></label>
                    <input type="text" @bind="_name" class="form-control" />
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn-primary" @onclick="SaveAsync">שמור</button>
                <button class="btn-secondary" @onclick="CloseModal">ביטול</button>
            </div>
            @if (!string.IsNullOrEmpty(_errorMessage))
            {
                <div class="alert alert-danger">@_errorMessage</div>
            }
        </div>
    </div>
}

@code {
    [Parameter] public EventCallback OnComplete { get; set; }

    private bool _isVisible = false;
    private string _title = "הוסף פריט";
    private string _name = "";
    private string _errorMessage = "";
    private int? _editId;

    // Called from parent page
    public void Show(int? id = null)
    {
        _editId = id;
        _title = id.HasValue ? "ערוך פריט" : "הוסף פריט";
        _name = "";
        _errorMessage = "";
        _isVisible = true;
        StateHasChanged();
    }

    private void CloseModal()
    {
        _isVisible = false;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            _errorMessage = "יש למלא את השם";
            return;
        }

        try
        {
            if (_editId.HasValue)
                await ApiService.PutAsync<object, object>("items/" + _editId, new { name = _name });
            else
                await ApiService.PostAsync<object, object>("items", new { name = _name });

            CloseModal();
            await OnComplete.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"שגיאה בשמירה: {ex.Message}";
        }
    }
}
```

## Filter Bar Pattern

Use `@bind:event="oninput"` for live (keystroke) filtering. Do NOT use a button submit:

```razor
<!-- Search/filter bar -->
<div class="filter-bar" style="margin-bottom: 12px;">
    <input type="text"
           @bind="_filterText"
           @bind:event="oninput"
           placeholder="חיפוש לפי שם..."
           style="padding: 6px 10px; border: 1px solid #dee2e6; border-radius: 4px; width: 250px;" />
</div>

@code {
    private string _filterText = "";

    private IEnumerable<MyDto> FilteredItems => (_items ?? [])
        .Where(i => string.IsNullOrWhiteSpace(_filterText) ||
                    i.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    i.Id.ToString().Contains(_filterText));
}
```

## Table Pattern Options

### Option 1: Inline table (most common)

Simple `<table>` in markup with `@foreach`. Use for standard list pages:

```razor
<div class="table-container">
    <table class="data-table">
        <thead>
            <tr>
                <th>פעולות</th>  @* Actions column ALWAYS first *@
                <th @onclick='() => SortTable("Name")' style="cursor:pointer">
                    שם @GetSortArrow("Name")
                </th>
                <th @onclick='() => SortTable("Id")' style="cursor:pointer">
                    מספר @GetSortArrow("Id")
                </th>
            </tr>
        </thead>
        <tbody>
            @foreach (var item in FilteredItems)
            {
                <tr>
                    <td>
                        <button class="btn-icon" @onclick="() => ViewItem(item.Id)">
                            <img src="/images/view_icon.png" alt="צפייה" class="action-icon-natural" />
                        </button>
                        <button class="btn-icon" @onclick="() => EditItem(item.Id)">
                            <img src="/images/edit_icon.png" alt="עריכה" class="action-icon-natural" />
                        </button>
                    </td>
                    <td>@item.Name</td>
                    <td>@item.Id</td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

### Option 2: SortableTableBase<T> (for reusable components)

Inherit from `SortableTableBase<T>` in `Shared/SortableTableBase.cs` when the same table is shared across multiple pages:

```csharp
// In the component's @code
// (The base already provides: SortTable(column), GetSortArrow(column), SortedItems)
public class MyTableComponent : SortableTableBase<MyItemDto>
{
    [Parameter] public List<MyItemDto> Items { get; set; } = [];
}
```

## DTOs

- Blazor-side DTOs live in `PetelATH.BlazorServer/DTOs/`
- Mirror the API response shape; keep them thin (no logic)
- Naming: `{Domain}Dto.cs` containing one or more DTO classes

```csharp
// DTOs/StudentDtos.cs
namespace PetelATH.BlazorServer.DTOs
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public string? SchoolYearName { get; set; }
    }

    public class CreateStudentRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
    }
}
```

## Icon Usage in Razor

All icons are PNGs in `wwwroot/images/`. Reference with absolute path:

```razor
<img src="/images/view_icon.png"     alt="צפייה"   class="action-icon-natural" />
<img src="/images/edit_icon.png"     alt="עריכה"   class="action-icon-natural" />
<img src="/images/delete_icon.png"   alt="מחיקה"   class="action-icon-natural" />
<img src="/images/download_icon.png" alt="הורד"    class="action-icon-natural" />
<img src="/images/upload_icon.png"   alt="העלה"    class="action-icon-natural" />
<img src="/images/stats_icon.png"    alt="סטטיסטיקה" class="action-icon-natural" />
<img src="/images/Plus icon.png"     alt="הוסף"    class="action-icon-natural" />
```

Do **not** use emoji (🔍, ✏️, 🗑️) as icon replacements.

## Authentication Guard

`AuthenticationGuard.razor` in `Components/Security/` wraps `MainLayout`'s body. It calls `AuthService.IsAuthenticatedAsync()` on init and redirects to `/login` if the token is invalid. You do **not** need to add authentication checks to individual pages — `SecurePageBase` does page-level permission checks.

## Anti-Patterns to Avoid

```razor
@* ❌ WRONG — JavaScript SPA patterns don't exist here *@
@inject IJSRuntime JS
await JS.InvokeVoidAsync("window.navigateTo", "students");  // NO SPA nav

@* ❌ WRONG — sessionStorage for state *@
await JS.InvokeVoidAsync("sessionStorage.setItem", "key", value);  // NO

@* ❌ WRONG — Override OnInitializedAsync in a SecurePageBase page *@
protected override async Task OnInitializedAsync() { ... }  // Use OnPageInitializedAsync instead

@* ❌ WRONG — Hardcoded API URLs *@
var r = await _http.GetAsync("http://localhost:5082/api/students");  // Use ApiService

@* ✅ CORRECT *@
protected override string PageName => "students";
protected override async Task OnPageInitializedAsync()
{
    _items = await ApiService.GetAsync<List<StudentDto>>("students");
}
```

```razor
@* ❌ WRONG — Emoji icons *@
<button>🗑️ מחק</button>

@* ✅ CORRECT — PNG icons *@
<button class="btn-icon" @onclick="DeleteItem">
    <img src="/images/delete_icon.png" alt="מחיקה" class="action-icon-natural" />
</button>
```

```razor
@* ❌ WRONG — Navigation via JS *@
await JS.InvokeVoidAsync("window.location.href", "/students");

@* ✅ CORRECT — NavigationManager *@
Navigation.NavigateTo("/students");
```
