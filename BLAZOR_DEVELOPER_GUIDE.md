# Blazor Server Developer Guide

**Project**: Petel Educational Management System  
**Version**: 2.0  
**Last Updated**: January 27, 2026

---

## Table of Contents

1. [Development Setup](#development-setup)
2. [Architecture Overview](#architecture-overview)
3. [Core Services](#core-services)
4. [Security Implementation](#security-implementation)
5. [Component Patterns](#component-patterns)
6. [Common Scenarios](#common-scenarios)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

---

## Development Setup

### Prerequisites

- Visual Studio 2022 (v17.8+) or VS Code with C# extension
- .NET 8.0 SDK
- PostgreSQL connection to backend database
- Git

### Local Development

1. **Start Backend API**:
   ```bash
   cd c:\dev\PetelFullApp
   # Double-click: Start Local Api.cmd
   # OR: cd PetelApp.Api && dotnet run
   ```
   - API runs on: `http://localhost:5082`

2. **Start Blazor Server**:
   ```bash
   cd c:\dev\PetelFullApp
   # Double-click: Start Blazor Server.cmd
   # OR: cd PetelApp.BlazorServer && dotnet run
   ```
   - Blazor runs on: `http://localhost:5169` (or `https://localhost:7169`)

3. **Access Application**:
   - Open browser: `https://localhost:7169`
   - Login with test credentials
   - Select entity and authenticate

### Project Structure

```
PetelApp.BlazorServer/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor         # Main app layout
│   │   ├── NavMenu.razor            # Database-driven menu
│   │   ├── EmptyLayout.razor        # Layout for login
│   │   └── AuthenticationGuard.razor # Route protection
│   ├── Pages/
│   │   ├── Login.razor              # Authentication
│   │   ├── MainDashboard.razor      # Main dashboard
│   │   ├── Students.razor           # Student list
│   │   ├── Student.razor            # Student details
│   │   ├── SchoolDetails.razor      # School details
│   │   ├── SecurePageBase.cs        # Base for secure pages
│   │   └── ... (25 pages total)
│   └── Shared/
│       ├── SecureButton.razor       # Secured action button
│       ├── DocumentsTable.razor     # Document management
│       ├── SessionTimeoutWarning.razor
│       └── ... (8 components total)
├── Services/
│   ├── ApiService.cs               # HTTP client wrapper
│   ├── TokenService.cs             # JWT token storage
│   ├── SessionStateService.cs      # Session caching
│   ├── AuthenticationService.cs    # Auth state management
│   ├── SessionTimeoutService.cs    # Idle timeout tracking
│   └── ActionSecurityService.cs    # Action security
├── DTOs/
│   ├── SessionData.cs              # Session data structure
│   ├── SecurityDTOs.cs             # Security requests/responses
│   └── ... (20+ DTOs)
├── Models/
│   └── ... (domain models)
├── wwwroot/
│   ├── css/                        # All CSS files
│   ├── images/                     # All images/icons
│   └── js/                         # JavaScript interop
└── Program.cs                      # App configuration
```

---

## Architecture Overview

### Authentication Flow

```
User Request
    ↓
AuthenticationGuard (wraps MainLayout)
    ↓
Check JWT token in ProtectedSessionStorage
    ↓
If valid → Proceed to page
If invalid → Redirect to /login
    ↓
Login → POST /api/auth/login
    ↓
Receive JWT token → Store in ProtectedSessionStorage
    ↓
Redirect to dashboard
```

### Page Lifecycle

```
User navigates to /students
    ↓
SecurePageBase.OnInitializedAsync()
    ↓
Verify page access (POST /api/security/verify-action-secure)
    ↓
If allowed:
  - Call OnPageInitializedAsync() (your code)
  - Render page
If denied:
  - Show alert "אין לך הרשאה לגשת לעמוד זה"
  - Navigate back (history.back)
```

### Action Execution Flow

```
User clicks SecureButton
    ↓
SecureButton.HandleClickAsync()
    ↓
Verify action (POST /api/security/verify-action-secure)
    ↓
If allowed:
  - Execute OnClick callback
  - Log to audit trail
If denied:
  - Show alert "אין לך הרשאה לבצע פעולה זו"
  - Stay on page
```

---

## Core Services

### ApiService

**Purpose**: Centralized HTTP client with automatic authentication headers.

**Usage**:
```csharp
@inject ApiService ApiService

// GET request with authentication
var data = await ApiService.GetAsync<StudentDto[]>("students?schoolId=123");

// POST request
var response = await ApiService.PostAsync<CreateRequest, CreateResponse>(
    "students",
    new CreateRequest { Name = "John" }
);

// PUT request
await ApiService.PutAsync<UpdateRequest, bool>("students/123", request);

// DELETE request
await ApiService.DeleteAsync("students/123");

// Public GET (no auth required)
var entities = await ApiService.GetPublicAsync<EntityDto[]>("entities/login");
```

**Key Features**:
- ✅ Automatic JWT header injection
- ✅ JSON serialization/deserialization
- ✅ Error handling and logging
- ✅ Base URL from configuration

### SessionStateService

**Purpose**: Cache user session data to reduce API calls.

**Usage**:
```csharp
@inject SessionStateService SessionState

// Get session (cached for 1 minute)
var session = await SessionState.GetSessionAsync();
Console.WriteLine($"User: {session.Username}");
Console.WriteLine($"Entity: {session.EntityName}");
Console.WriteLine($"School Year: {session.SchoolYearId}");

// Force refresh cache
SessionState.InvalidateCache();
var freshSession = await SessionState.GetSessionAsync();

// Subscribe to cache invalidation events
SessionState.OnCacheInvalidated += async () =>
{
    await LoadData(); // Reload page data
};
```

**Session Properties**:
```csharp
public class SessionData
{
    public string UserId { get; set; }
    public string Username { get; set; }
    public string UserFullName { get; set; }
    public string EntityId { get; set; }
    public string EntityName { get; set; }
    public int EntityTypeId { get; set; }
    public string EntityTypeName { get; set; }
    public int? SchoolYearId { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public List<int> RoleIds { get; set; }
    // ... additional properties
}
```

### ActionSecurityService

**Purpose**: Verify user permissions for actions and pages.

**Usage**:
```csharp
@inject ActionSecurityService SecurityService

// Verify action permission
bool allowed = await SecurityService.VerifyActionAsync(
    actionName: "students_deleteStudent",
    screenName: "students",
    functionName: "DeleteStudent",
    eventType: "BUTTON_CLICK",
    actionParams: $"studentId={studentId}",
    description: "Delete student from system"
);

if (!allowed)
{
    await JSRuntime.InvokeVoidAsync("alert", "אין לך הרשאה לפעולה זו");
    return;
}

// Execute action...
await ApiService.DeleteAsync($"students/{studentId}");

// Verify page access
bool canAccess = await SecurityService.VerifyPageAccessAsync("students");

// Get access denied message
string message = SecurityService.GetAccessDeniedMessage("students_delete");
```

### TokenService

**Purpose**: Securely store JWT authentication tokens.

**Usage**:
```csharp
@inject TokenService TokenService

// Store token (after login)
await TokenService.SetTokenAsync(jwtToken);

// Get token (for API calls)
var token = await TokenService.GetTokenAsync();

// Check if token exists
bool hasToken = await TokenService.HasTokenAsync();

// Clear token (logout)
await TokenService.ClearTokenAsync();
```

**Key Features**:
- ✅ Uses `ProtectedSessionStorage` (encrypted)
- ✅ Survives page refreshes
- ✅ Cleared on logout
- ✅ Automatic expiration handling

### SessionTimeoutService

**Purpose**: Track user idle time and auto-logout.

**Usage**:
```csharp
@inject SessionTimeoutService TimeoutService

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // Start tracking (if not already started)
        await TimeoutService.StartAsync();
        
        // Subscribe to warning event (2 min before timeout)
        TimeoutService.OnWarning += ShowTimeoutWarning;
        
        // Subscribe to timeout event (logout)
        TimeoutService.OnTimeout += async () =>
        {
            await HandleLogout();
        };
    }
}

// Reset timeout on user activity (automatic via JS interop)
// TimeoutService.ResetAsync() called by activity-tracker.js

// Get remaining time
var remaining = await TimeoutService.GetRemainingTimeAsync();
Console.WriteLine($"Timeout in: {remaining.TotalMinutes:F0} minutes");
```

**Configuration** (from backend):
```json
{
  "SessionTimeoutMinutes": 10,
  "SessionWarningMinutes": 2
}
```

---

## Security Implementation

### Creating a Secure Page

**Step 1**: Inherit from `SecurePageBase`:

```csharp
@page "/mypage"
@layout MainLayout
@inherits SecurePageBase
@inject ApiService ApiService
@inject SessionStateService SessionState

<div class="page-container">
    <h1>My Secure Page</h1>
    <!-- Page content -->
</div>

@code {
    // REQUIRED: Page identifier for security
    protected override string PageName => "mypage";
    
    // Called AFTER page access verified
    protected override async Task OnPageInitializedAsync()
    {
        await LoadData();
    }
    
    private async Task LoadData()
    {
        var session = await SessionState.GetSessionAsync();
        // Load data for entity
        var data = await ApiService.GetAsync<DataDto[]>($"data?entityId={session.EntityId}");
    }
}
```

**What You Get**:
- ✅ Automatic page access verification
- ✅ Redirect to login if not authenticated
- ✅ Navigation back if access denied
- ✅ Hebrew error messages
- ✅ Audit trail logging

### Creating a Secure Button

**Pattern 1**: Using `SecureButton` Component (Recommended):

```razor
<SecureButton 
    ActionName="mypage_saveData"
    ScreenName="@PageName"
    FunctionName="SaveData"
    OnClick="SaveData"
    CssClass="btn-primary"
    Disabled="@_isSaving"
    HideIfNoAccess="false">
    שמור נתונים
</SecureButton>

@code {
    private bool _isSaving = false;
    
    private async Task SaveData()
    {
        _isSaving = true;
        try
        {
            await ApiService.PostAsync("data/save", _data);
            await JSRuntime.InvokeVoidAsync("alert", "הנתונים נשמרו בהצלחה");
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }
}
```

**SecureButton Parameters**:
- `ActionName` (required): Action identifier (e.g., "students_delete")
- `ScreenName` (required): Page name (use `@PageName`)
- `FunctionName` (required): Method name being called
- `OnClick` (required): Callback method to execute
- `CssClass` (optional): CSS classes to apply
- `Disabled` (optional): Disable button
- `HideIfNoAccess` (optional): Hide button if no permission (default: false)
- `ActionParams` (optional): Parameters for audit log
- `Description` (optional): Human-readable description

**Pattern 2**: Manual Security Check:

```csharp
private async Task DeleteStudent(int studentId)
{
    // Manual security check using helper method
    var executed = await ExecuteSecureActionAsync(
        actionName: "students_deleteStudent",
        functionName: "DeleteStudent",
        action: async () =>
        {
            // This code ONLY runs if permission granted
            await ApiService.DeleteAsync($"students/{studentId}");
            await LoadStudents(); // Refresh list
        },
        actionParams: $"studentId={studentId}",
        description: "Delete student from system"
    );
    
    if (executed)
    {
        await JSRuntime.InvokeVoidAsync("alert", "התלמיד נמחק בהצלחה");
    }
    // If not executed, alert already shown by helper
}
```

### Action Naming Conventions

**Format**: `{page}_{action}` or descriptive name

**Examples**:
```csharp
// Page access (Type 8)
"students"
"schooldetails"
"roledetails"

// Button actions (Type 7)
"students_addStudent"
"students_deleteStudent"
"students_uploadFile"
"students_calculatePricing"
"students_generateDocuments"

// Context buttons
"students_backToSchool"
"students_refreshData"

// Table row actions
"students_viewStudent"
"students_editStudent"
```

### Auto-Create Actions

**How It Works**:
1. User performs action (page access or button click)
2. Backend checks if action exists in `actions` table
3. If NOT found → Creates new action automatically
4. New action is `is_active=true` but NOT assigned to any role
5. User gets "access denied" (fail-secure)
6. Admin must assign action to role for user to proceed

**Database Result**:
```sql
INSERT INTO petel_schema.actions (name, action_type_id, reference, description)
VALUES (
    'students_deleteStudent',  -- Action name
    7,                         -- Type: Button
    NULL,                      -- No reference needed
    'Auto-created by security system'
);
```

**Admin Workflow**:
1. User reports "access denied" for action
2. Admin queries database: `SELECT * FROM actions WHERE name = 'students_deleteStudent'`
3. Admin assigns to role: `INSERT INTO roles_actions (role_id, action_id) VALUES (1, 123)`
4. Admin refreshes security cache: Click "רענן מטמון אבטחה" in Roles page
5. User can now perform action

---

## Component Patterns

### Collapsible Cards

**Pattern**: Expand/collapse sections in detail pages.

```razor
<div class="detail-card @(_isExpanded ? "expanded" : "collapsed")">
    <div class="detail-card-header" @onclick="ToggleExpansion">
        <h2 class="detail-card-title">כותרת הכרטיס</h2>
        <div class="card-header-actions">
            <button class="btn-icon" @onclick:stopPropagation="true" @onclick="EditCard">
                <img src="images/edit_icon.png" alt="עריכה" class="action-icon-natural">
            </button>
            <button class="collapse-toggle">@(_isExpanded ? "×" : "+")</button>
        </div>
    </div>
    <div class="detail-card-content">
        <!-- Card content here -->
    </div>
</div>

@code {
    private bool _isExpanded = false;
    
    private void ToggleExpansion()
    {
        _isExpanded = !_isExpanded;
    }
    
    private void EditCard()
    {
        // Edit logic (doesn't trigger collapse)
    }
}
```

**CSS**:
```css
.detail-card {
    transition: all 0.3s ease;
}

.detail-card.collapsed .detail-card-content {
    max-height: 0;
    overflow: hidden;
}

.detail-card.expanded .detail-card-content {
    max-height: 1000px;
    overflow: visible;
}
```

### Modal Dialogs

**Pattern**: Add/edit forms in modal overlays.

```razor
@if (_showAddModal)
{
    <div class="modal-backdrop" @onclick="CloseAddModal">
        <div class="modal-dialog" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h3>הוסף פריט חדש</h3>
                <button class="modal-close" @onclick="CloseAddModal">×</button>
            </div>
            <div class="modal-body">
                <EditForm Model="@_newItem" OnValidSubmit="SaveNewItem">
                    <DataAnnotationsValidator />
                    
                    <div class="form-group">
                        <label>שם: <span style="color: red;">*</span></label>
                        <InputText @bind-Value="_newItem.Name" class="form-control" />
                        <ValidationMessage For="@(() => _newItem.Name)" />
                    </div>
                    
                    <div class="modal-actions">
                        <button type="submit" class="btn-primary" disabled="@_isSaving">
                            @if (_isSaving)
                            {
                                <span class="spinner-border spinner-border-sm"></span>
                                <text>שומר...</text>
                            }
                            else
                            {
                                <text>שמור</text>
                            }
                        </button>
                        <button type="button" class="btn-secondary" @onclick="CloseAddModal">
                            ביטול
                        </button>
                    </div>
                </EditForm>
            </div>
        </div>
    </div>
}

<SecureButton ActionName="mypage_addItem" OnClick="ShowAddModal">
    הוסף פריט
</SecureButton>

@code {
    private bool _showAddModal = false;
    private bool _isSaving = false;
    private ItemDto _newItem = new();
    
    private void ShowAddModal()
    {
        _newItem = new ItemDto(); // Reset form
        _showAddModal = true;
    }
    
    private void CloseAddModal()
    {
        _showAddModal = false;
        _newItem = new();
    }
    
    private async Task SaveNewItem()
    {
        _isSaving = true;
        try
        {
            await ApiService.PostAsync("items", _newItem);
            await JSRuntime.InvokeVoidAsync("alert", "הפריט נוסף בהצלחה");
            CloseAddModal();
            await LoadItems(); // Refresh list
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }
}
```

### Edit/Save/Cancel Pattern

**Pattern**: Toggle edit mode with data preservation.

```razor
<div class="detail-card">
    <div class="detail-card-header">
        <h2>פרטי הישות</h2>
        <div class="card-header-actions">
            @if (!_isEditMode)
            {
                <SecureButton ActionName="mypage_edit" OnClick="ToggleEditMode" CssClass="btn-icon">
                    <img src="images/edit_icon.png" alt="עריכה" />
                </SecureButton>
            }
            else
            {
                <button class="btn-primary" @onclick="SaveChanges" disabled="@_isSaving">
                    שמור
                </button>
                <button class="btn-secondary" @onclick="CancelEdit">
                    ביטול
                </button>
            }
        </div>
    </div>
    <div class="detail-card-content">
        <div class="form-group">
            <label>שם:</label>
            @if (_isEditMode)
            {
                <input type="text" @bind="_currentData.Name" class="form-control" />
            }
            else
            {
                <span>@_currentData.Name</span>
            }
        </div>
    </div>
</div>

@code {
    private bool _isEditMode = false;
    private bool _isSaving = false;
    private EntityDto _currentData = new();
    private EntityDto _originalData = new();
    
    protected override async Task OnPageInitializedAsync()
    {
        await LoadData();
    }
    
    private async Task LoadData()
    {
        var session = await SessionState.GetSessionAsync();
        _currentData = await ApiService.GetAsync<EntityDto>($"entities/{session.EntityId}");
        _originalData = _currentData.Clone(); // Deep copy
    }
    
    private void ToggleEditMode()
    {
        _isEditMode = !_isEditMode;
        if (_isEditMode)
        {
            _originalData = _currentData.Clone(); // Save for cancel
        }
    }
    
    private async Task SaveChanges()
    {
        _isSaving = true;
        try
        {
            await ApiService.PutAsync<EntityDto, bool>($"entities/{_currentData.Id}", _currentData);
            await JSRuntime.InvokeVoidAsync("alert", "השינויים נשמרו בהצלחה");
            _isEditMode = false;
            _originalData = _currentData.Clone(); // Update saved state
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }
    
    private void CancelEdit()
    {
        _currentData = _originalData.Clone(); // Restore original
        _isEditMode = false;
    }
}

// Extension method for cloning
public static class DtoExtensions
{
    public static T Clone<T>(this T source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
```

### Context Buttons Layout

**Pattern**: Action buttons positioned on right side of page.

```razor
<div class="page-container">
    <!-- Context buttons on RIGHT -->
    <div class="context-buttons-section">
        <SecureButton ActionName="mypage_refresh" OnClick="RefreshData" CssClass="context-btn">
            <img src="images/view_icon.png" alt="רענן" class="action-icon-natural">
            רענן נתונים
        </SecureButton>
        
        <SecureButton ActionName="mypage_export" OnClick="ExportData" CssClass="context-btn">
            <img src="images/download_icon.png" alt="ייצא" class="action-icon-natural">
            ייצא לאקסל
        </SecureButton>
        
        <SecureButton ActionName="mypage_back" OnClick="NavigateBack" CssClass="context-btn">
            חזרה
        </SecureButton>
    </div>
    
    <!-- Main content on LEFT -->
    <div class="main-content">
        <h1>כותרת העמוד</h1>
        <!-- Page content here -->
    </div>
</div>

@code {
    private void NavigateBack()
    {
        Navigation.NavigateTo("/previous-page");
    }
}
```

---

## Common Scenarios

### Loading Data from API

**Pattern**: Load data in `OnPageInitializedAsync` with error handling.

```csharp
private List<StudentDto> _students = new();
private bool _isLoading = true;
private string? _errorMessage = null;

protected override async Task OnPageInitializedAsync()
{
    await LoadStudents();
}

private async Task LoadStudents()
{
    _isLoading = true;
    _errorMessage = null;
    
    try
    {
        var session = await SessionState.GetSessionAsync();
        
        _students = await ApiService.GetAsync<List<StudentDto>>(
            $"students?schoolId={session.SchoolId}&yearId={session.SchoolYearId}"
        );
        
        Console.WriteLine($"✅ Loaded {_students.Count} students");
    }
    catch (Exception ex)
    {
        _errorMessage = $"שגיאה בטעינת נתונים: {ex.Message}";
        Console.WriteLine($"❌ Error loading students: {ex}");
    }
    finally
    {
        _isLoading = false;
        StateHasChanged();
    }
}

// In template
@if (_isLoading)
{
    <div class="loading-spinner">טוען נתונים...</div>
}
else if (_errorMessage != null)
{
    <div class="alert alert-danger">@_errorMessage</div>
}
else if (_students.Count == 0)
{
    <div class="no-data">אין נתונים להצגה</div>
}
else
{
    <table class="data-table">
        @foreach (var student in _students)
        {
            <tr>
                <td>@student.Name</td>
                <td>@student.ClassName</td>
            </tr>
        }
    </table>
}
```

### Creating/Updating Data

**Pattern**: POST/PUT with validation and feedback.

```csharp
private async Task CreateStudent()
{
    // Validate
    if (string.IsNullOrWhiteSpace(_newStudent.FirstName))
    {
        await JSRuntime.InvokeVoidAsync("alert", "שם פרטי חובה");
        return;
    }
    
    if (string.IsNullOrWhiteSpace(_newStudent.LastName))
    {
        await JSRuntime.InvokeVoidAsync("alert", "שם משפחה חובה");
        return;
    }
    
    _isSaving = true;
    
    try
    {
        var session = await SessionState.GetSessionAsync();
        _newStudent.SchoolId = session.SchoolId;
        _newStudent.SchoolYearId = session.SchoolYearId;
        
        var response = await ApiService.PostAsync<StudentDto, CreateStudentResponse>(
            "students",
            _newStudent
        );
        
        if (response.Success)
        {
            await JSRuntime.InvokeVoidAsync("alert", "התלמיד נוסף בהצלחה");
            _showAddModal = false;
            await LoadStudents(); // Refresh list
        }
        else
        {
            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {response.Message}");
        }
    }
    catch (Exception ex)
    {
        await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {ex.Message}");
        Console.WriteLine($"❌ Error creating student: {ex}");
    }
    finally
    {
        _isSaving = false;
    }
}
```

### Deleting Data

**Pattern**: Confirmation before delete.

```csharp
private async Task DeleteStudent(int studentId, string studentName)
{
    // Confirm
    bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", 
        $"האם למחוק את התלמיד {studentName}?");
    
    if (!confirmed)
        return;
    
    try
    {
        await ApiService.DeleteAsync($"students/{studentId}");
        await JSRuntime.InvokeVoidAsync("alert", "התלמיד נמחק בהצלחה");
        await LoadStudents(); // Refresh list
    }
    catch (Exception ex)
    {
        await JSRuntime.InvokeVoidAsync("alert", $"שגיאה במחיקה: {ex.Message}");
    }
}
```

### File Upload

**Pattern**: Upload files to document API.

```razor
<InputFile OnChange="HandleFileSelected" accept=".xlsx,.xls,.pdf,.docx" />

@code {
    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        
        if (file == null)
            return;
        
        // Validate file size (max 10MB)
        if (file.Size > 10 * 1024 * 1024)
        {
            await JSRuntime.InvokeVoidAsync("alert", "הקובץ גדול מדי (מקסימום 10MB)");
            return;
        }
        
        _isUploading = true;
        
        try
        {
            // Read file content
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            
            // Create multipart form data
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);
            
            // Add metadata
            var session = await SessionState.GetSessionAsync();
            content.Add(new StringContent(session.EntityId), "entityId");
            content.Add(new StringContent("1"), "documentTypeId"); // Student document
            
            // Upload
            var response = await HttpClient.PostAsync($"{ApiBaseUrl}/documents/upload", content);
            
            if (response.IsSuccessStatusCode)
            {
                await JSRuntime.InvokeVoidAsync("alert", "הקובץ הועלה בהצלחה");
                await LoadDocuments(); // Refresh list
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בהעלאת קובץ: {error}");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {ex.Message}");
        }
        finally
        {
            _isUploading = false;
        }
    }
}
```

### File Download

**Pattern**: Download files from document API.

```csharp
private async Task DownloadDocument(int documentId, string fileName)
{
    try
    {
        // Get file bytes from API
        var fileBytes = await ApiService.GetAsync<byte[]>($"documents/{documentId}/download");
        
        // Trigger browser download
        await JSRuntime.InvokeVoidAsync("downloadFile", fileName, Convert.ToBase64String(fileBytes));
    }
    catch (Exception ex)
    {
        await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בהורדת קובץ: {ex.Message}");
    }
}

// JavaScript helper in wwwroot/js/file-helpers.js
window.downloadFile = function (fileName, base64Data) {
    const link = document.createElement('a');
    link.href = 'data:application/octet-stream;base64,' + base64Data;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
```

---

## Best Practices

### Code Organization

✅ **DO**:
- Inherit from `SecurePageBase` for all authenticated pages
- Use `SecureButton` for all actions
- Inject services via `@inject`
- Put business logic in services, not in pages
- Use DTOs for API communication
- Use `async`/`await` consistently

❌ **DON'T**:
- Hardcode API URLs (use configuration)
- Hardcode schema names (use configuration)
- Call API directly from JSRuntime
- Store sensitive data in browser storage
- Skip error handling
- Forget to dispose of subscriptions

### Performance

✅ **DO**:
- Cache session data (SessionStateService)
- Use projection queries (Select specific fields)
- Debounce search inputs
- Use `StateHasChanged()` sparingly
- Dispose of event subscriptions in `Dispose()`

❌ **DON'T**:
- Call API on every render
- Load entire entities when you only need IDs
- Create new HttpClient instances
- Subscribe to events without cleanup
- Use `InvokeAsync(StateHasChanged)` excessively

### Security

✅ **DO**:
- Verify page access in `SecurePageBase`
- Verify action access in `SecureButton`
- Log all security events
- Use JWT tokens for authentication
- Clear tokens on logout
- Validate all inputs

❌ **DON'T**:
- Trust client-side validation alone
- Store passwords in plain text
- Skip audit logging
- Use HTTP (always HTTPS in production)
- Expose sensitive data in logs
- Hardcode API keys

### User Experience

✅ **DO**:
- Show loading spinners during async operations
- Display error messages in Hebrew
- Confirm destructive actions
- Provide feedback after operations
- Use consistent button styling
- Support keyboard navigation

❌ **DON'T**:
- Leave users waiting without feedback
- Show technical error messages
- Allow destructive actions without confirmation
- Disable buttons without explanation
- Break browser back button
- Forget mobile users

---

## Troubleshooting

### Common Issues

#### Issue: "Access Denied" for new action

**Symptom**: User gets "אין לך הרשאה לפעולה זו" for a new button.

**Solution**:
1. Check if action was auto-created:
   ```sql
   SELECT * FROM petel_schema.actions WHERE name = 'students_newAction';
   ```
2. Assign action to user's role:
   ```sql
   INSERT INTO petel_schema.roles_actions (role_id, action_id)
   VALUES (1, (SELECT id FROM petel_schema.actions WHERE name = 'students_newAction'));
   ```
3. Refresh security cache: Go to Roles page, click "רענן מטמון אבטחה"

#### Issue: Session data is stale

**Symptom**: Page shows old data after updates.

**Solution**:
```csharp
// Force refresh session cache
SessionState.InvalidateCache();
var freshSession = await SessionState.GetSessionAsync();
```

#### Issue: "JavaScript interop calls cannot be issued during static rendering"

**Symptom**: Error when calling JSRuntime in OnInitializedAsync.

**Solution**: Move JSRuntime calls to OnAfterRenderAsync:
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // JSRuntime calls here
        await JSRuntime.InvokeVoidAsync("initComponent");
    }
}
```

#### Issue: Modal doesn't close on backdrop click

**Symptom**: Clicking outside modal doesn't close it.

**Solution**: Add `@onclick:stopPropagation` to modal content:
```razor
<div class="modal-backdrop" @onclick="CloseModal">
    <div class="modal-dialog" @onclick:stopPropagation="true">
        <!-- Modal content -->
    </div>
</div>
```

#### Issue: Button stays disabled after click

**Symptom**: Button disabled during async operation and never re-enabled.

**Solution**: Use try-finally to reset state:
```csharp
private bool _isSaving = false;

private async Task SaveData()
{
    _isSaving = true;
    try
    {
        await ApiService.PostAsync("data", _data);
    }
    finally
    {
        _isSaving = false;
        StateHasChanged(); // Force re-render
    }
}
```

#### Issue: Navigation doesn't update URL

**Symptom**: Page changes but URL stays the same.

**Solution**: Use NavigationManager:
```csharp
@inject NavigationManager Navigation

Navigation.NavigateTo("/target-page");
```

#### Issue: Session timeout not working

**Symptom**: User never gets logged out despite inactivity.

**Solution**: Check if SessionTimeoutService is started:
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await TimeoutService.StartAsync();
    }
}
```

### Debugging Tips

1. **Check Browser Console**: Look for JavaScript errors and network requests
2. **Check Server Logs**: Review API logs for errors
3. **Use Breakpoints**: Debug C# code in Visual Studio
4. **Check Network Tab**: Verify API requests and responses
5. **Check Database**: Query actions and roles tables directly
6. **Use Swagger**: Test API endpoints independently

---

## Additional Resources

- **Blazor Documentation**: https://docs.microsoft.com/en-us/aspnet/core/blazor/
- **Security Guide**: See `BLAZOR_SECURITY_USAGE_GUIDE.md`
- **Deployment Guide**: See `BLAZOR_DEPLOYMENT_GUIDE.md`
- **Architecture Guide**: See `.github/copilot-instructions.md`
- **Migration Summary**: See `BLAZOR_MIGRATION_COMPLETE.md`

---

**Document Version**: 1.0  
**Last Updated**: January 27, 2026  
**For Questions**: Contact development team
