# Blazor Server Migration Instructions

## Architecture Overview

**Migration from Vanilla JavaScript to Blazor Server** for the Petel Educational Management System.

- **Original Frontend**: Vanilla HTML/CSS/JS SPA with Hebrew RTL support
- **Target**: Blazor Server .NET 9.0
- **Backend API**: ASP.NET Core 9.0 Web API (unchanged) on `http://localhost:5082`
- **Authentication**: JWT tokens stored in ProtectedSessionStorage

## Critical Configuration

### Disable Prerendering

**REQUIRED**: Blazor Server prerenders by default, which breaks JavaScript interop (like ProtectedSessionStorage).

**App.razor** - Root component configuration:
```razor
@using Microsoft.AspNetCore.Components.Web

<!DOCTYPE html>
<html lang="he" dir="rtl">
<head>
    <!-- CSS and meta tags -->
</head>
<body>
    <!-- ✅ CRITICAL: Disable prerendering explicitly -->
    <Routes @rendermode="new InteractiveServerRenderMode(prerender: false)" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

**Program.cs** - Service registration:
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies();  // For router discovery

// Map with both render modes
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies();
```

**Why This Matters**:
- Without `prerender: false`, Blazor tries to render on server first
- ProtectedSessionStorage requires JavaScript, which doesn't exist during prerender

## API Endpoint Patterns

### Correct API Routes (from Backend Controllers)

**AlertsController** (`/api/alerts`):
- `GET /api/alerts/entity/{entityId}?isEvent=false` - Get alerts for entity
- `GET /api/alerts/entity/{entityId}?isEvent=true` - Get events for entity
- `POST /api/alerts` - Create new alert/event

**SystemAttributes** (`/api/systemattributes`):
- `GET /api/systemattributes` - Get all system attributes (includes year IDs)
- System attribute names: "Previous Year", "Current Year", "Next Year"
- `ForeignId` property contains the Hebrew year ID reference

**SchoolYearsController** (`/api/schoolyears`):
- `GET /api/schoolyears/by-year-and-school?yearId={yearId}&schoolId={schoolId}` - Get school_year ID

**Key Pattern**: Events and Alerts share the same controller/endpoint, differentiated by `isEvent` query parameter.

### Loading School Years

**Frontend Pattern** - Use SystemAttributes to get year IDs:
```csharp
var attributes = await ApiService.GetAsync<List<SystemAttributeDto>>("systemattributes");

var previousYear = attributes.FirstOrDefault(a => a.Name == "Previous Year");
var currentYear = attributes.FirstOrDefault(a => a.Name == "Current Year");
var nextYear = attributes.FirstOrDefault(a => a.Name == "Next Year");

// Use ForeignId property for year ID
if (currentYear != null)
    _currentYearId = currentYear.ForeignId ?? 0;
```

### Alert/Event DTO Structure

**CRITICAL**: Match exact API response field names and types from AlertsController.

```csharp
private class AlertDto
{
    public int Id { get; set; }
    public int AlertType { get; set; }      // ✅ int FK to alert_types, not string
    public int AlertLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Status { get; set; }         // ✅ int from alert_links, not string
    public int UserId { get; set; }
    public bool IsEvent { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public long LinkId { get; set; }
}

// Helper to display alert type name
private string GetAlertTypeName(int alertTypeId)
{
    return alertTypeId switch
    {
        1 => "התראה כללית",
        2 => "התראת חשבונות",
        3 => "התראת תלמידים",
        4 => "התראת מורים",
        _ => $"סוג התראה {alertTypeId}"
    };
}
```

**Common Mistakes**:
- ❌ `AlertType` as `string` - API returns `int` (FK to alert_types table)
- ❌ `Status` as `string` - API returns `int` from `alert_links.alert_status`
- ❌ `Level` as string - API returns `alertLevel` as `int`
- ❌ Using `Title` property - API returns `alertType` field

## Page Layout Patterns

### Standard Pages with MainLayout

**REQUIRED**: All pages must explicitly specify the layout directive at the top:

```razor
@page "/pagename"
@layout MainLayout
@using PetelApp.BlazorServer.DTOs
@using PetelApp.BlazorServer.Services
```

**Why This Matters**:
- Ensures consistent side menu and navigation across all pages
- Without `@layout MainLayout`, pages will not have proper layout structure
- Must be placed at the very top of the .razor file

### Full-Width Pages Bypassing MainLayout

For pages needing full-width layouts (like certain dashboards), use custom layout:

**Full-Width Dashboard Pattern**:
```razor
@page "/maindashboard"

<!-- Full-width layout bypassing MainLayout constraints -->
<div class="page-container" style="width: 100%; height: 100vh; overflow-y: auto; background-color: #f5f5f5;">
    <!-- Fixed collapsed side menu -->
    <div class="side-menu collapsed" style="position: fixed; left: 0; top: 60px; width: 60px; height: calc(100vh - 60px); background-color: #2c3e50; z-index: 100;">
        <!-- Placeholder for collapsed menu -->
    </div>

    <!-- Main content shifted right of menu -->
    <div class="main-container" style="margin-left: 60px; width: calc(100% - 60px); min-height: 100vh; background-color: #f5f5f5;">
        <!-- Page content here -->
    </div>
</div>
```

**Key Patterns**:
- ✅ Use inline `style` attributes for full control
- ✅ Side menu: `position: fixed; left: 0; width: 60px` for collapsed state
- ✅ Main content: `margin-left: 60px; width: calc(100% - 60px)` to fill remaining space
- ✅ Background color: `#f5f5f5` matches original HTML design
- Results in: `"JavaScript interop calls cannot be issued during static rendering"`

### API Configuration

**Environment-specific base URL** in `appsettings.json`:
```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5082/api"
  }
}
```

Production/staging environments override via environment-specific config files.

## Service Layer Patterns

### ApiService Pattern

**Centralized HTTP client** with automatic auth header injection:

```csharp
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    
    // Standard authenticated request
    public async Task<T> GetAsync<T>(string endpoint)
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.GetAsync($"{_baseUrl}/{endpoint}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
    
    // Public endpoint (login page) - no auth
    public async Task<T> GetPublicAsync<T>(string endpoint)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var response = await _httpClient.GetAsync($"{_baseUrl}/{endpoint}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
```

**Usage**:
```csharp
// Authenticated page
var schools = await ApiService.GetAsync<List<SchoolDto>>("schools");

// Login page (no auth)
var entities = await ApiService.GetPublicAsync<List<EntityDto>>("entities/login");
```

### TokenService Pattern

**Secure token storage** using ProtectedSessionStorage:

```csharp
public class TokenService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _sessionStorage.GetAsync<string>("authToken");
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;  // Handle prerender gracefully
        }
    }
    
    public async Task SetTokenAsync(string token)
    {
        await _sessionStorage.SetAsync("authToken", token);
    }
    
    public async Task ClearTokenAsync()
    {
        await _sessionStorage.DeleteAsync("authToken");
    }
}
```

**Why ProtectedSessionStorage**:
- Data encrypted on server
- Survives browser refresh
- Cleared when browser tab closes
- More secure than localStorage

### SessionStateService Pattern

**Client-side session state** for temporary page data:

```csharp
public class SessionStateService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    
    public async Task<T?> GetPropertyAsync<T>(string key)
    {
        var result = await _sessionStorage.GetAsync<T>(key);
        return result.Success ? result.Value : default;
    }
    
    public async Task SetPropertyAsync<T>(string key, T value)
    {
        await _sessionStorage.SetAsync(key, value);
    }
}
```

**Usage**:
```csharp
// Store selected school ID when navigating to school details
await SessionStateService.SetPropertyAsync("SelectedSchoolId", schoolId);

// Retrieve in target page
var schoolId = await SessionStateService.GetPropertyAsync<int>("SelectedSchoolId");
```

## Page Lifecycle Patterns

### OnAfterRenderAsync Pattern

**Use for initialization requiring JavaScript interop**:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // 1. Check authentication
        var token = await TokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Navigation.NavigateTo("/login");
            return;
        }
        
        // 2. Load session state
        var schoolId = await SessionState.GetPropertyAsync<int>("SelectedSchoolId");
        
        // 3. Load data from API
        await LoadSchoolData(schoolId);
        
        // 4. Update UI
        StateHasChanged();
        
        // 5. Set focus or run JS (with delay)
        await Task.Delay(200);
        await _inputElement.FocusAsync();
    }
}
```

**Why OnAfterRenderAsync**:
- `OnInitializedAsync` runs during prerender (no JS available)
- `OnAfterRenderAsync(firstRender: true)` runs after DOM is ready
- Safe for ProtectedSessionStorage and ElementReference

### Focus Management

**Setting focus requires delay after render**:

```csharp
@page "/mypage"

<input @ref="_inputElement" />

@code {
    private ElementReference _inputElement;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load data first
            await LoadData();
            StateHasChanged();
            
            // Then focus with delay
            await Task.Delay(200);
            await _inputElement.FocusAsync();
        }
    }
}
```

**Why Delay Required**:
- Blazor re-renders after StateHasChanged()
- ElementReference may not be ready immediately
- 200ms ensures DOM is fully updated

## Component Patterns

### Data Table with Actions Column

**Actions column MUST always be the first column with no header text**:

```razor
<table class="data-table">
    <thead>
        <tr>
            <!-- ✅ Actions column first with empty header -->
            <th></th>
            <th @onclick="() => SortTable('Name')" style="cursor: pointer;">
                שם @GetSortArrow('Name')
            </th>
            <th @onclick="() => SortTable('Status')" style="cursor: pointer;">
                סטטוס @GetSortArrow('Status')
            </th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in _items)
        {
            <tr>
                <!-- ✅ Action buttons first -->
                <td>
                    <button class="btn-icon" @onclick="() => ViewItem(item.Id)" title="צפה">
                        <img src="/images/view_icon.png" alt="צפייה" class="action-icon-natural">
                    </button>
                    <button class="btn-icon" @onclick="() => EditItem(item.Id)" title="ערוך">
                        <img src="/images/edit_icon.png" alt="עריכה" class="action-icon-natural">
                    </button>
                </td>
                <td>@item.Name</td>
                <td>@item.Status</td>
            </tr>
        }
    </tbody>
</table>
```

**Key Patterns**:
- ✅ Actions column is ALWAYS the first column
- ✅ Header cell is empty (`<th></th>`) - no text label
- ✅ Action buttons use `btn-icon` class for consistent styling
- ✅ Icons are 15px PNG files with `action-icon-natural` class
- ✅ Each button has a descriptive `title` attribute for tooltips
- ❌ Do NOT place actions column at the end
- ❌ Do NOT add a header label like "פעולות" or "Actions"

### Summary Cards Pattern

**Horizontal summary cards for metrics/statistics** (e.g., Students page):

```razor
<!-- Summary Cards -->
<div class="dashboard-cards-wrapper" style="margin-bottom: 20px;">
    <div style="display: flex; gap: 20px; flex-wrap: wrap;">
        <!-- Total Count Card -->
        <div class="summary-card" style="min-width: 200px; background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); border-radius: 12px; padding: 20px; display: flex; align-items: center; gap: 15px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);">
            <img src="/images/view_icon.png" alt="" style="width: 40px; height: 40px; object-fit: contain; opacity: 0.8;">
            <div style="flex: 1; text-align: right;">
                <div style="font-size: 2em; font-weight: 700; color: #2c3e50; line-height: 1; margin-bottom: 5px;">@_summary.TotalCount</div>
                <div style="font-size: 0.9em; color: #666; font-weight: 500;">סה"כ פריטים</div>
            </div>
        </div>
        
        <!-- Additional cards... -->
    </div>
</div>
```

**CRITICAL**: 
- ❌ **DO NOT** use `internal-cards-container` for horizontal summary cards - it uses `flex-direction: column` (vertical)
- ✅ Use inline `style="display: flex; gap: 20px; flex-wrap: wrap;"` for horizontal layout
- ✅ Each card uses inline styles for self-contained, portable styling
- ✅ Icons should be 40x40px with `opacity: 0.8`
- ✅ Summary values use `font-size: 2em; font-weight: 700`

**When to Use**:
- Top-of-page metrics summary (total students, active users, etc.)
- Statistics overview (4-6 horizontal cards)
- Pages with tabular data below the summary

### Autocomplete Dropdown Pattern

**Standard implementation for entity/school selection**:

```razor
<div class="autocomplete-container">
    <input type="text" 
           @ref="_searchInput"
           value="@_searchText"
           @oninput="OnSearchTextChanged"
           @onkeydown="HandleKeyDown"
           @onfocus="OnFocus"
           placeholder="הזן לפחות 3 תווים..."
           autocomplete="off" />
    
    <div class="autocomplete-arrow"></div>
    
    <div class="autocomplete-dropdown @(_showDropdown ? "show" : "")">
        @if (_filteredItems.Count == 0 && _searchText.Length >= 3)
        {
            <div class="autocomplete-no-results">לא נמצאו תוצאות</div>
        }
        else
        {
            @foreach (var item in _filteredItems)
            {
                <div class="autocomplete-option" @onclick="() => SelectItem(item)">
                    @item.Name
                </div>
            }
        }
    </div>
</div>

@code {
    private string _searchText = "";
    private bool _showDropdown = false;
    private List<ItemDto> _items = new();
    private List<ItemDto> _filteredItems = new();
    
    private void OnSearchTextChanged(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? "";
        FilterItems();
    }
    
    private void FilterItems()
    {
        if (_searchText.Length < 3)
        {
            _filteredItems = new();
            _showDropdown = false;
            return;
        }
        
        _filteredItems = _items
            .Where(i => i.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
        
        _showDropdown = true;
    }
    
    private void SelectItem(ItemDto item)
    {
        _searchText = item.Name;
        _showDropdown = false;
        // Navigate or store selection
    }
}
```

**CSS for dropdown arrow**:
```css
.autocomplete-arrow {
    position: absolute;
    left: 12px;
    top: 50%;
    transform: translateY(-50%);
    width: 0;
    height: 0;
    border-left: 5px solid transparent;
    border-right: 5px solid transparent;
    border-top: 6px solid #666;
    pointer-events: none;
    opacity: 0.5;
}
```

### Student Detail Page with Tabs and Collapsible Cards

**Individual entity detail page pattern** (Student, School, Program, etc.):

```razor
@page "/student"
@layout MainLayout

<div class="main-container">
    <!-- Context buttons for actions -->
    <div class="context-buttons-section">
        <button class="context-btn" @onclick="CalculateAction">
            חשב רכיבי תמחור
        </button>
        <div class="context-spacer"></div>
        <button class="context-navigation-btn" @onclick="NavigateBack">
            חזרה לרשימה
        </button>
    </div>

    <div class="students-content">
        <!-- Detail Form (NOT table) -->
        <div class="content-card">
            <!-- Header with entity name -->
            <div class="school-header">
                <h2>@_entity.Name</h2>
                <span class="school-code">קוד: @_entity.Code</span>
            </div>

            <!-- Detail Sections (using form-group pattern) -->
            <div class="detail-card">
                <div class="detail-card-header">
                    <h3>פרטים אישיים</h3>
                </div>
                <div class="detail-card-content">
                    <div class="form-row">
                        <div class="form-group">
                            <label>שדה 1</label>
                            <input type="text" class="form-input" value="@_entity.Field1" disabled />
                        </div>
                        <div class="form-group">
                            <label>שדה 2</label>
                            <input type="text" class="form-input" value="@_entity.Field2" disabled />
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Tabs Container -->
        <div class="content-card">
            <!-- Tab Headers -->
            <div class="tabs-header">
                <button class="tab-button @(_activeTab == "tab1" ? "active" : "")" 
                        @onclick='() => SwitchTab("tab1")'>
                    טאב 1
                </button>
                <button class="tab-button @(_activeTab == "tab2" ? "active" : "")" 
                        @onclick='() => SwitchTab("tab2")'>
                    טאב 2
                </button>
            </div>

            <!-- Tab Content -->
            <div class="tab-content" style="display: @(_activeTab == "tab1" ? "block" : "none");">
                <!-- Collapsible Card -->
                <div class="detail-card @(_cardExpanded ? "expanded" : "collapsed")">
                    <div class="detail-card-header" @onclick="() => _cardExpanded = !_cardExpanded">
                        <h2 class="detail-card-title">כותרת כרטיס</h2>
                        <div class="card-header-actions">
                            <button class="collapse-toggle" aria-label="הרחב/כווץ">
                                @(_cardExpanded ? "×" : "+")
                            </button>
                        </div>
                    </div>
                    <div class="detail-card-content">
                        @if (_cardExpanded)
                        {
                            <!-- Card content here -->
                        }
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    private EntityDto? _entity;
    private string _activeTab = "tab1";
    private bool _cardExpanded = true;

    private void SwitchTab(string tabName)
    {
        _activeTab = tabName;
    }
}
```

**Key Patterns**:
- ✅ Load entity data from session property (e.g., SelectedStudentId)
- ✅ **Use form-group pattern, NOT tables** for entity details
- ✅ Header uses `school-header` class with name and code/ID
- ✅ Details organized in `detail-card` sections (Personal, Address, Additional)
- ✅ Each section uses `form-row` with two `form-group` elements
- ✅ All inputs use `disabled` attribute (non-editable display)
- ✅ Labels are simple text, values in disabled input fields
- ✅ Tabs use conditional CSS class: `@(_activeTab == "tab1" ? "active" : "")`
- ✅ Tab content uses inline style: `style="display: @(_activeTab == "tab1" ? "block" : "none");"`
- ✅ Collapsible cards toggle via boolean state: `@(_cardExpanded ? "expanded" : "collapsed")`
- ✅ Card header click toggles expansion: `@onclick="() => _cardExpanded = !_cardExpanded"`
- ✅ Toggle button shows × when expanded, + when collapsed
- ✅ Card content conditionally rendered: `@if (_cardExpanded) { ... }`

**Form Layout Structure**:
```razor
<div class="detail-card">
    <div class="detail-card-header">
        <h3>Section Title</h3>
    </div>
    <div class="detail-card-content">
        <div class="form-row">
            <div class="form-group">
                <label>Field Label</label>
                <input type="text" class="form-input" value="@_value" disabled />
            </div>
            <div class="form-group">
                <label>Field Label 2</label>
                <input type="text" class="form-input" value="@_value2" disabled />
            </div>
        </div>
    </div>
</div>
```

**Student Page Specifics**:
- Documents tab: Three collapsible sections (Student, School, Entity documents)
- Pricing tab: Single collapsible card with summary and table
- Pricing summary: Three metrics (Elements Total, Student Cost, Enrollment Months)
- Context actions: Calculate pricing, Generate documents, Navigation

**CSS Classes Used**:
- `school-header` - Entity name and code display
- `detail-card` - Section container
- `detail-card-header` - Section header
- `detail-card-content` - Section content area
- `form-row` - Horizontal container for form groups (2 per row)
- `form-group` - Individual field container (label + input)
- `form-input` - Input field styling

### Step Indicator Pattern

**Multi-step forms with progress visualization**:

```razor
<div class="step-indicator">
    <div class="step @(_currentStep >= 1 ? "active" : "") @(_currentStep > 1 ? "completed" : "")">
        <span class="step-number">1</span>
        <span>בחר ארגון</span>
    </div>
    <div class="step-divider"></div>
    <div class="step @(_currentStep >= 2 ? "active" : "") @(_currentStep > 2 ? "completed" : "")">
        <span class="step-number">2</span>
        <span>הזן פרטים</span>
    </div>
    <div class="step-divider"></div>
    <div class="step @(_currentStep >= 3 ? "active" : "")">
        <span class="step-number">3</span>
        <span>התחבר</span>
    </div>
</div>

@code {
    private int _currentStep = 1;
    
    private void AdvanceStep()
    {
        _currentStep++;
    }
}
```

## System Attributes Pattern

**Loading global configuration from backend cache**:

```csharp
// System attributes are loaded into memory on API startup
// Query via SystemAttributesController (AllowAnonymous)

private async Task LoadSystemVersion()
{
    try
    {
        // Get all attributes (from cache)
        var allAttributes = await ApiService.GetPublicAsync<List<SystemAttributeDto>>("systemattributes");
        
        // System Version is attribute ID 1 with description "System Version"
        var systemVersionAttr = allAttributes?.FirstOrDefault(a => 
            a.Id == 1 || a.Description == "System Version");
        
        if (systemVersionAttr != null && !string.IsNullOrEmpty(systemVersionAttr.Value))
        {
            _systemVersion = $"מערכת ניהול - גרסה {systemVersionAttr.Value}";
        }
        else
        {
            _systemVersion = "מערכת ניהול";
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load system version");
        _systemVersion = "מערכת ניהול";
    }
}
```

**System Attribute Structure**:
```csharp
public class SystemAttributeDto
{
    public int Id { get; set; }
    public string Name { get; set; }           // Internal identifier
    public string Value { get; set; }          // Actual value
    public string ValueType { get; set; }      // Data type
    public string? Description { get; set; }   // Display name (Hebrew)
}
```

**Known System Attributes**:
- ID 1: System Version (description: "System Version") - displayed on login page
- Other attributes loaded via SystemAttributeCache on backend startup

## CSS and RTL Support

### Hebrew RTL Patterns

**HTML structure**:
```html
<html lang="he" dir="rtl">
```

**CSS patterns**:
```css
.rtl-content {
    direction: rtl;
    text-align: right;
}

/* Use logical properties instead of left/right */
.container {
    padding-inline-start: 20px;  /* Instead of padding-left */
    padding-inline-end: 20px;    /* Instead of padding-right */
}
```

### Reusing Original CSS

**Copy CSS files to `wwwroot/css/`**:
- `theme.css` - Global variables and theme
- `styles.css` - Main application styles
- `login.css` - Login page specific
- `ui-components.css` - Reusable components

**Reference in App.razor**:
```razor
<head>
    <link rel="stylesheet" href="/css/theme.css" />
    <link rel="stylesheet" href="/css/styles.css" />
    <link rel="stylesheet" href="/css/login.css" />
</head>
```

## Testing and Debugging

### Running Both Servers

**Terminal 1 - Backend API**:
```bash
cd PetelApp.Api
dotnet run
# Runs on http://localhost:5082
```

**Terminal 2 - Blazor Server**:
```bash
cd PetelApp.BlazorServer
dotnet run
# Runs on http://localhost:5293 (dynamic port)
```

### Hard Refresh Requirements

**After code changes, always hard refresh browser**:
- `Ctrl+F5` (Windows/Linux)
- `Cmd+Shift+R` (Mac)
- Or: DevTools → Right-click refresh → "Empty Cache and Hard Reload"

**Why**: Blazor caches JavaScript bundles aggressively

### Common Issues

**Issue**: "JavaScript interop calls cannot be issued during static rendering"
**Solution**: Add `prerender: false` to InteractiveServerRenderMode in App.razor

**Issue**: Focus not working on input
**Solution**: Add 200ms delay before calling FocusAsync()

**Issue**: ProtectedSessionStorage returns null
**Solution**: Wrap in try-catch for prerender compatibility

**Issue**: API returns 401 Unauthorized
**Solution**: Check token in browser storage, verify TokenService is injecting header

## Migration Checklist

For each page migration:

1. ✅ Create `.razor` file in `Components/Pages/`
2. ✅ Add `@page` directive with route
3. ✅ Use `@layout EmptyLayout` if no standard layout needed
4. ✅ Inject required services (`@inject ApiService`, `@inject TokenService`)
5. ✅ Copy HTML markup from original `.html` file
6. ✅ Convert event handlers:
   - `onclick="function()"` → `@onclick="Function"`
   - `oninput="function()"` → `@oninput="OnInput"`
7. ✅ Convert bindings:
   - `value="${variable}"` → `value="@_variable"`
   - `id="element"` → `@ref="_element"`
8. ✅ Move JavaScript functions to `@code` block
9. ✅ Convert DOM manipulation to Blazor state updates
10. ✅ Use `OnAfterRenderAsync` for initialization
11. ✅ Test with hard browser refresh
12. ✅ Verify authentication flow works

## Phase 1 Completion Status

**Migrated Pages** (12 pages):
- ✅ Login (with OTP support)
- ✅ MainDashboard
- ✅ SchoolDashboard  
- ✅ SchoolList
- ✅ SchoolDetails
- ✅ Students (list page)
- ✅ Student (individual detail page)
- ✅ Analytics
- ✅ SystemAttributes
- ✅ About
- ✅ Swagger (external link)

**Login Page Notes**:
- 3-step indicator: Select Entity → Enter Credentials → Login
- Autocomplete dropdown with 3-character minimum
- Keyboard navigation (Arrow Up/Down, Enter, Escape)
- Auto-focus on entity input field
- System version loaded from system_attributes (ID 1)
- OTP verification flow supported

## Next Steps (Phase 2)

Remaining pages to migrate:
- SchoolYearDetails
- ClassDetails
- Documents management
- Reports
- User management
- Additional forms and detail pages

Focus areas:
- Complex forms with validation
- File upload/download
- Modal dialogs
- Data grids with sorting/filtering
- Real-time updates via SignalR
