# Blazor Action Security Design

**Project**: Petel Educational Management System - Blazor Server  
**Date**: January 18, 2026  
**Status**: Design Proposal

---

## Overview

This document proposes a comprehensive action-based security system for the Blazor Server application that mirrors the **fail-secure design** of the original vanilla JavaScript implementation. Every user action (button clicks, navigation, data operations) becomes a security event that is:

1. **Verified server-side** with backend authorization check
2. **Logged to audit trail** with full context (user, action, result, timestamp, IP)
3. **Fail-secure** - any error = deny access

---

## Original Vanilla JS Architecture (Reference)

### Frontend: `action-security.js`

**Key Features:**
```javascript
window.ActionSecurity = {
    // Global click interceptor - runs in CAPTURE phase (before onclick)
    setupClickInterceptor() {
        document.addEventListener('click', async (event) => {
            // 1. Find element with onclick attribute
            // 2. PREVENT default execution
            // 3. Call backend: /api/security/verify-action-secure
            // 4. Backend verifies + logs audit trail
            // 5. Execute onclick ONLY if allowed
        }, true); // Capture phase
    }
};
```

**Security Properties:**
- ✅ **Fail-secure**: System blocks all actions if security initialization fails
- ✅ **Server-side audit**: Frontend cannot bypass audit logging
- ✅ **No client-side permissions cache**: Every action verified with backend
- ✅ **Event type detection**: Distinguishes menu navigation vs button clicks
- ✅ **Action parameter extraction**: Captures onclick function arguments

### Backend: `SecurityController.cs`

**Key Endpoint:**
```csharp
[HttpPost("verify-action-secure")]
public async Task<IActionResult> VerifyActionSecure([FromBody] SecureActionRequest request)
{
    // STEP 1: Verify authorization
    bool hasAccess = await _actionAuthService.VerifyOnclickAccessAsync(...);
    
    // STEP 2: Log to audit trail (ALWAYS)
    await LogAuditTrailAsync(userId, actionName, result, ...);
    
    // STEP 3: Return result
    return Ok(new { allowed = hasAccess });
}
```

**Database Tables:**
- `system_actions` - All registered actions in the system
- `roles_actions` - Role-to-action permissions mapping
- `action_audit_logs` - Audit trail of all action attempts
- `user_roles` - User-to-role assignments

---

## Blazor Implementation Strategy

### Phase 1: Core Security Infrastructure

#### 1.1 Security Service (Server-Side)

**`Services/ActionSecurityService.cs`** - Blazor equivalent of `action-security.js`

```csharp
/// <summary>
/// Blazor Server action security service
/// Wraps all authorization checks and ensures audit logging
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
    public async Task<bool> VerifyActionAsync(
        string actionName,
        string screenName,
        string functionName,
        string eventType = "BUTTON_CLICK",
        string? actionParams = null,
        string? description = null)
    {
        try
        {
            var request = new SecureActionRequest
            {
                ActionName = actionName,
                ScreenName = screenName,
                FunctionName = functionName,
                EventType = eventType,
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

            _logger.LogWarning("🚫 Action denied: {Action}", actionName);
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
    /// </summary>
    public async Task<bool> VerifyMenuNavigationAsync(string menuItemName, string menuReference)
    {
        return await VerifyActionAsync(
            actionName: menuItemName,
            screenName: "menu",
            functionName: "navigateTo",
            eventType: "MENU_NAVIGATION",
            actionParams: menuReference
        );
    }

    /// <summary>
    /// Verify page access (for direct URL navigation)
    /// </summary>
    public async Task<bool> VerifyPageAccessAsync(string pageName)
    {
        return await VerifyActionAsync(
            actionName: pageName,
            screenName: "navigation",
            functionName: "accessPage",
            eventType: "PAGE_ACCESS"
        );
    }

    /// <summary>
    /// Show access denied message
    /// </summary>
    public string GetAccessDeniedMessage(string actionName)
    {
        return $"אין לך הרשאה לפעולה זו: {actionName}";
    }
}
```

#### 1.2 DTOs for Security

**`DTOs/SecurityDTOs.cs`**

```csharp
public class SecureActionRequest
{
    public string ActionName { get; set; } = string.Empty;
    public string? ScreenName { get; set; }
    public string? FunctionName { get; set; }
    public string? EventType { get; set; }
    public string? ActionParams { get; set; }
    public string? Description { get; set; }
}

public class SecureActionResponse
{
    public bool Success { get; set; }
    public bool Allowed { get; set; }
    public string? Message { get; set; }
}
```

#### 1.3 Register Service

**`Program.cs`**

```csharp
// Register security service as scoped (per-user)
builder.Services.AddScoped<ActionSecurityService>();
```

---

### Phase 2: Component-Level Security Attributes

#### 2.1 Security-Aware Button Component

**`Components/Shared/SecureButton.razor`**

```razor
@inject ActionSecurityService SecurityService
@inject IJSRuntime JSRuntime

@* Renders a button that verifies permissions before executing action *@

@if (_isVisible)
{
    <button 
        class="@CssClass" 
        disabled="@(_isProcessing || Disabled)"
        @onclick="HandleClickAsync"
        title="@Title">
        @if (_isProcessing)
        {
            <span class="spinner-border spinner-border-sm me-2"></span>
        }
        @ChildContent
    </button>
}

@code {
    [Parameter] public string ActionName { get; set; } = string.Empty;
    [Parameter] public string ScreenName { get; set; } = string.Empty;
    [Parameter] public string FunctionName { get; set; } = string.Empty;
    [Parameter] public string? ActionParams { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string CssClass { get; set; } = "btn btn-primary";
    [Parameter] public string? Title { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool HideIfNoAccess { get; set; } = false; // Hide vs disable

    private bool _isProcessing = false;
    private bool _isVisible = true;

    protected override async Task OnInitializedAsync()
    {
        // Check if user has access to this button
        if (HideIfNoAccess && !string.IsNullOrEmpty(ActionName))
        {
            _isVisible = await SecurityService.VerifyActionAsync(
                ActionName, ScreenName, FunctionName, "BUTTON_VISIBLE_CHECK", ActionParams
            );
        }
    }

    private async Task HandleClickAsync()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        StateHasChanged();

        try
        {
            // Verify permission before executing action
            var allowed = await SecurityService.VerifyActionAsync(
                ActionName,
                ScreenName,
                FunctionName,
                "BUTTON_CLICK",
                ActionParams
            );

            if (!allowed)
            {
                await JSRuntime.InvokeVoidAsync("alert", 
                    SecurityService.GetAccessDeniedMessage(ActionName));
                return;
            }

            // Execute the action
            await OnClick.InvokeAsync();
        }
        finally
        {
            _isProcessing = false;
            StateHasChanged();
        }
    }
}
```

**Usage Example:**

```razor
<SecureButton 
    ActionName="students_addStudent"
    ScreenName="students"
    FunctionName="addStudent"
    OnClick="HandleAddStudentAsync"
    HideIfNoAccess="true"
    CssClass="btn btn-primary">
    <img src="Plus icon.png" alt="הוסף" class="action-icon-natural">
    הוסף תלמיד
</SecureButton>
```

#### 2.2 Menu Filtering (Already Implemented)

**✅ IMPORTANT: Menu filtering is ALREADY WORKING in the current system!**

The `MenuController.GetMenuItems()` endpoint already:
- Loads all active menu items from database
- Filters them using `ActionAuthorizationService.VerifyMenuItemAccessAsync()`
- Returns only menu items the user has permission to see

**Current Implementation in `NavMenu.razor`:**

```razor
@code {
    protected override async Task OnInitializedAsync()
    {
        // Backend already filters menu items by user permissions
        _menuItems = await ApiService.GetAsync<List<MenuItemDto>>("menu");
    }
}
```

**No additional security check needed!** The menu items returned from the API are already filtered. Simply render them:

```razor
@foreach (var item in _menuItems ?? new())
{
    <a href="@item.Reference" 
       class="menu-item @(IsActive(item.Reference) ? "active" : "")"
       @onclick="() => Navigation.NavigateTo(item.Reference)"
       @onclick:preventDefault="true">
        <span class="menu-item-text">@item.Text</span>
    </a>
}
```

**Why no SecureMenuItem component needed:**
- Server-side filtering is more secure (can't be bypassed)
- Eliminates N+1 permission checks (one API call instead of one per menu item)
- Menu items not visible = user never sees them in HTML source
- Reduces frontend complexity

---

---

## Auto-Create Missing Actions Feature

**✅ CRITICAL FEATURE: Already implemented in `ActionAuthorizationService`**

When a user attempts an action that doesn't exist in the `system_actions` table, the system automatically:

1. **Creates the action** in the database (as ACTIVE initially per code)
2. **Logs a warning** for admin review
3. **Denies access** (fail-secure - action created but not assigned to any role)
4. **Admin can then assign** the action to appropriate roles

**How it works:**

```csharp
public async Task<bool> VerifyOnclickAccessAsync(int userId, string screenName, string functionName)
{
    var actionId = $"{screenName}_{functionName}".ToLower();
    
    // Check if action exists in cache
    if (!_actionsCache.TryGetValue(actionId, out var action))
    {
        _logger.LogWarning("🚫 Action NOT REGISTERED: {ActionId}", actionId);
        
        // Auto-create as INACTIVE
        action = await AutoCreateMissingActionAsync(actionId, screenName, functionName);
        
        // DENY access (fail-secure)
        return false;
    }
    
    // Check user permissions...
}
```

**Benefits:**
- ✅ **Zero maintenance overhead** - New buttons/actions auto-register
- ✅ **Fail-secure** - Auto-created actions have no permissions until admin assigns them
- ✅ **Audit trail** - Logs show which actions users are attempting
- ✅ **Discovery tool** - Admins see what actions are actually being used
- ✅ **Development friendly** - Devs don't need to manually register every action

**Admin Workflow:**

1. Developer adds new button: `<SecureButton ActionName="students_printReport" .../>`
2. User clicks button → Action auto-created → Access denied
3. Admin checks logs: "Action NOT REGISTERED: students_printreport"
4. Admin goes to Action Management page
5. Finds auto-created action: `students_printreport` (Status: ACTIVE, No roles assigned)
6. Admin assigns action to "Teacher" and "Administrator" roles
7. User clicks button again → Access granted

**Blazor Integration - No Changes Needed:**

The `ActionSecurityService.VerifyActionAsync()` method will automatically trigger the auto-create behavior through the existing backend endpoint. The Blazor frontend just needs to call the API - the auto-create magic happens server-side.

---

### Phase 3: Page-Level Security Guards

#### 3.1 Secure Page Base Class

**`Components/Pages/SecurePageBase.cs`**

```csharp
using Microsoft.AspNetCore.Components;

public abstract class SecurePageBase : ComponentBase
{
    [Inject] protected ActionSecurityService SecurityService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

    protected abstract string PageName { get; }

    protected override async Task OnInitializedAsync()
    {
        // Verify user has access to this page
        var allowed = await SecurityService.VerifyPageAccessAsync(PageName);

        if (!allowed)
        {
            await JSRuntime.InvokeVoidAsync("alert", "אין לך הרשאה לגשת לעמוד זה");
            Navigation.NavigateTo("/");
            return;
        }

        await OnPageInitializedAsync();
    }

    /// <summary>
    /// Override this instead of OnInitializedAsync
    /// </summary>
    protected virtual Task OnPageInitializedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper: Execute action with automatic security check
    /// </summary>
    protected async Task<bool> ExecuteSecureActionAsync(
        string actionName,
        string functionName,
        Func<Task> action,
        string? actionParams = null)
    {
        var allowed = await SecurityService.VerifyActionAsync(
            actionName,
            PageName,
            functionName,
            "PAGE_ACTION",
            actionParams
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
}
```

**Usage Example:**

```razor
@page "/students"
@inherits SecurePageBase
@layout MainLayout

<h1>תלמידים</h1>

<SecureButton 
    ActionName="students_addStudent"
    ScreenName="@PageName"
    FunctionName="addStudent"
    OnClick="HandleAddStudentAsync">
    הוסף תלמיד
</SecureButton>

@code {
    protected override string PageName => "students";

    private async Task HandleAddStudentAsync()
    {
        // Action already verified by SecureButton
        // Implement add student logic
        Console.WriteLine("Adding student...");
    }

    private async Task HandleDeleteStudentAsync(int studentId)
    {
        // Manual security check for dynamic actions
        await ExecuteSecureActionAsync(
            actionName: "students_deleteStudent",
            functionName: "deleteStudent",
            action: async () =>
            {
                // Delete student logic
                Console.WriteLine($"Deleting student {studentId}");
            },
            actionParams: $"studentId={studentId}"
        );
    }
}
```

---

### Phase 4: Advanced Patterns

#### 4.1 Table Row Actions with Security

**Pattern for tables with per-row actions:**

```razor
<table class="data-table">
    <thead>
        <tr>
            <th>שם</th>
            <th>פעולות</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var student in _students)
        {
            <tr>
                <td>@student.Name</td>
                <td>
                    <SecureButton 
                        ActionName="students_viewStudent"
                        ScreenName="students"
                        FunctionName="viewStudent"
                        ActionParams="@($"studentId={student.Id}")"
                        OnClick="@(() => ViewStudentAsync(student.Id))"
                        CssClass="btn-icon"
                        HideIfNoAccess="true">
                        <img src="view_icon.png" alt="צפה" class="action-icon-natural">
                    </SecureButton>

                    <SecureButton 
                        ActionName="students_editStudent"
                        ScreenName="students"
                        FunctionName="editStudent"
                        ActionParams="@($"studentId={student.Id}")"
                        OnClick="@(() => EditStudentAsync(student.Id))"
                        CssClass="btn-icon"
                        HideIfNoAccess="true">
                        <img src="edit_icon.png" alt="ערוך" class="action-icon-natural">
                    </SecureButton>

                    <SecureButton 
                        ActionName="students_deleteStudent"
                        ScreenName="students"
                        FunctionName="deleteStudent"
                        ActionParams="@($"studentId={student.Id}")"
                        OnClick="@(() => DeleteStudentAsync(student.Id))"
                        CssClass="btn-icon"
                        HideIfNoAccess="true">
                        <img src="delete_icon.png" alt="מחק" class="action-icon-natural">
                    </SecureButton>
                </td>
            </tr>
        }
    </tbody>
</table>
```

#### 4.2 Modal Actions with Security

**Pattern for modal dialogs:**

```razor
@if (_showAddStudentModal)
{
    <div class="modal-backdrop"></div>
    <div class="modal">
        <h2>הוסף תלמיד חדש</h2>
        
        <EditForm Model="_newStudent" OnValidSubmit="SaveStudentAsync">
            <!-- Form fields -->
            
            <div class="modal-actions">
                <SecureButton 
                    ActionName="students_saveStudent"
                    ScreenName="students"
                    FunctionName="saveStudent"
                    OnClick="SaveStudentAsync"
                    CssClass="btn btn-primary">
                    שמור
                </SecureButton>
                
                <button type="button" class="btn btn-secondary" @onclick="CloseModal">
                    ביטול
                </button>
            </div>
        </EditForm>
    </div>
}
```

#### 4.3 API Operations with Security

**Pattern for direct API calls:**

```csharp
private async Task UploadDocumentAsync()
{
    // Option 1: Use SecureButton (recommended)
    // Button already handles security check

    // Option 2: Manual security check (for programmatic calls)
    var allowed = await SecurityService.VerifyActionAsync(
        actionName: "documents_upload",
        screenName: "student",
        functionName: "uploadDocument",
        eventType: "FILE_UPLOAD",
        actionParams: $"studentId={_studentId},type=document"
    );

    if (!allowed)
    {
        await JSRuntime.InvokeVoidAsync("alert", "אין לך הרשאה להעלות מסמכים");
        return;
    }

    // Proceed with upload
    await _apiService.PostAsync(...);
}
```

---

## Migration Checklist

### High Priority (Week 1-2)

1. ✅ **Implement `ActionSecurityService`** - Core security service
2. ✅ **Create `SecureButton` component** - Security-aware button
3. ✅ **Implement `SecurePageBase`** - Base class for secure pages
4. ✅ **Verify menu filtering works** - Current NavMenu already uses filtered API (no changes needed)
5. ✅ **Test auto-create feature** - Verify missing actions are auto-created and logged

### Medium Priority (Week 3-4)

6. ⏳ **Migrate all buttons to `SecureButton`** - Replace standard buttons across all pages
7. ⏳ **Add page-level security guards** - Inherit from `SecurePageBase`
8. ⏳ **Implement table row action security** - Per-row action buttons
9. ⏳ **Add modal action security** - Secure save/submit buttons in modals

### Low Priority (Week 5+)

10. ⏳ **Bulk security checks** - Check multiple actions at once for performance
11. ⏳ **Security cache** - Cache permission checks for 5 minutes (with invalidation)
12. ⏳ **Security dashboard** - View audit logs in UI
13. ⏳ **Real-time permission updates** - SignalR notifications when permissions change

---

## Action Naming Conventions

**Format:** `{screenName}_{functionName}`

**Examples:**
- `students_addStudent`
- `students_editStudent`
- `students_deleteStudent`
- `students_viewStudent`
- `students_uploadExcel`
- `students_calculatePricing`
- `student_uploadDocument`
- `student_generateDocuments`
- `schooldetails_editSchool`
- `schooldetails_addClass`
- `schooldetails_deleteClass`

**Menu Items:** Use page name as action name
- `maindashboard`
- `students`
- `schoollist`
- `schooldetails`

---

## Benefits Over Original Implementation

✅ **Type Safety** - Compile-time checking of action names and parameters  
✅ **Component Reusability** - `SecureButton` eliminates repetitive security code  
✅ **Automatic UI Hiding** - `HideIfNoAccess` hides buttons user can't use  
✅ **Server-Side Rendering** - No client-side permission cache to worry about  
✅ **Strongly Typed DTOs** - Prevents parameter mismatch errors  
✅ **Dependency Injection** - Easy to mock for unit testing  
✅ **IntelliSense Support** - Autocomplete for action names and parameters  
✅ **Menu Filtering Already Working** - Backend filtering is more secure than client-side checks  
✅ **Auto-Create Feature Preserved** - Automatic action registration continues to work  

---

## Security Guarantees (Same as Original)

✅ **Fail-Secure Design** - Any error = deny access  
✅ **Server-Side Authorization** - All decisions made by backend  
✅ **Immutable Audit Trail** - Every action logged to database  
✅ **No Client-Side Bypass** - Frontend cannot skip permission checks  
✅ **IP Address Tracking** - Audit logs include source IP  
✅ **Timestamp Precision** - UTC timestamps for all actions  
✅ **User Context** - Every action linked to authenticated user  

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task SecureButton_WhenDenied_ShouldNotExecuteAction()
{
    // Arrange
    var mockSecurity = new Mock<ActionSecurityService>();
    mockSecurity.Setup(s => s.VerifyActionAsync(...)).ReturnsAsync(false);
    
    var button = new SecureButton { SecurityService = mockSecurity_Object };
    var actionExecuted = false;
    button.OnClick = EventCallback.Factory.Create(this, () => actionExecuted = true);
    
    // Act
    await button.HandleClickAsync();
    
    // Assert
    Assert.False(actionExecuted);
}
```

### Integration Tests

```csharp
[Fact]
public async Task Students_AddButton_ShouldLogAuditTrail()
{
    // Arrange
    var client = _factory.CreateClient();
    await AuthenticateAsTestUser(client);
    
    // Act
    await client.PostAsync("/api/security/verify-action-secure", ...);
    
    // Assert
    var auditLog = await GetLatestAuditLog();
    Assert.Equal("students_addStudent", auditLog.ActionName);
    Assert.Equal("GRANTED", auditLog.Result);
}
```

---

## Next Steps

1. **Review and approve this design document**
2. **Implement Phase 1** - Core security infrastructure
3. **Implement Phase 2** - Security-aware components
4. **Pilot on Students page** - Test all patterns
5. **Roll out to all pages** - Systematic migration
6. **Performance testing** - Ensure security doesn't impact UX
7. **Security audit** - Verify fail-secure properties

---

## IP Address Tracking on Login

### Current State

**❌ IP Address NOT currently captured on login**

The `AuthController.Login()` method does not currently extract or store the user's IP address. The `User` model also lacks an IP address field for login tracking.

### Proposed Database Changes

#### 1. Add Login History Table

**Purpose**: Track all login attempts (successful and failed) with full audit trail

```sql
-- petel_schema.login_history table
CREATE TABLE petel_schema.login_history (
    id BIGSERIAL PRIMARY KEY,
    user_id INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    username VARCHAR(100) NOT NULL,
    entity_id INTEGER NULL REFERENCES petel_schema.entities(id) ON DELETE SET NULL,
    ip_address VARCHAR(45) NOT NULL,
    user_agent VARCHAR(500) NULL,
    login_result VARCHAR(20) NOT NULL,  -- 'SUCCESS', 'FAILED_PASSWORD', 'FAILED_OTP', 'LOCKED', 'INACTIVE'
    failure_reason VARCHAR(500) NULL,
    session_token VARCHAR(500) NULL,
    login_timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    logout_timestamp TIMESTAMP NULL,
    
    -- Indexes for fast queries
    INDEX idx_login_history_user_id (user_id),
    INDEX idx_login_history_ip (ip_address),
    INDEX idx_login_history_timestamp (login_timestamp),
    INDEX idx_login_history_result (login_result)
);
```

**Entity Model:**

```csharp
// Data/LoginHistory.cs
[Table("login_history")]
public class LoginHistory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Required]
    [Column("username")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Column("entity_id")]
    public int? EntityId { get; set; }

    [Required]
    [Column("ip_address")]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Required]
    [Column("login_result")]
    [MaxLength(20)]
    public string LoginResult { get; set; } = string.Empty;

    [Column("failure_reason")]
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    [Column("session_token")]
    [MaxLength(500)]
    public string? SessionToken { get; set; }

    [Column("login_timestamp")]
    public DateTime LoginTimestamp { get; set; } = DateTime.UtcNow;

    [Column("logout_timestamp")]
    public DateTime? LogoutTimestamp { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Entity? Entity { get; set; }
}
```

#### 2. Update User Model (Optional - Last Login IP)

**Add field to track most recent login IP:**

```csharp
// In User.cs - add field
[Column("last_login_ip")]
[MaxLength(45)]
public string? LastLoginIp { get; set; }
```

**Migration SQL:**

```sql
ALTER TABLE petel_schema.users 
ADD COLUMN last_login_ip VARCHAR(45) NULL;

CREATE INDEX idx_users_last_login_ip ON petel_schema.users(last_login_ip);
```

### Implementation Changes

#### AuthController.cs Updates

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
{
    // ✅ Capture IP and User-Agent
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    var userAgent = Request.Headers["User-Agent"].ToString();
    
    _logger.LogInformation("Login attempt from IP: {IP}, User-Agent: {UserAgent}", 
        ipAddress, userAgent);

    try
    {
        // Pass IP and User-Agent to auth service
        var result = await _authService.LoginAsync(request, ipAddress, userAgent);

        if (!result.Success)
        {
            _logger.LogWarning("Login failed for user: {Username} from IP: {IP} - Reason: {Message}", 
                request.Username, ipAddress, result.Message);
            return Ok(result);
        }

        _logger.LogInformation("Login successful: {Username} from IP: {IP}, Token: {Token}", 
            request.Username, ipAddress, result.Token);

        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Login error: {Username} from IP: {IP}", 
            request.Username, ipAddress);
        return StatusCode(500, new LoginResponseDto
        { 
            Success = false, 
            Message = "אירעה שגיאה בעת ההתחברות" 
        });
    }
}
```

#### AuthService.cs Updates

```csharp
public async Task<LoginResponseDto> LoginAsync(
    LoginRequestDto loginRequest, 
    string ipAddress, 
    string? userAgent)
{
    // ... existing validation logic ...

    try
    {
        // ... existing user lookup and validation ...

        // ✅ Log login attempt
        await LogLoginAttemptAsync(
            userId: user?.Id,
            username: loginRequest.Username,
            entityId: loginRequest.EntityId,
            ipAddress: ipAddress,
            userAgent: userAgent,
            result: "SUCCESS",
            sessionToken: token
        );

        // ✅ Update user's last login IP
        if (user != null)
        {
            user.LastLogin = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            await _context.SaveChangesAsync();
        }

        return new LoginResponseDto
        {
            Success = true,
            Token = token,
            // ... rest of response
        };
    }
    catch (Exception ex)
    {
        // ✅ Log failed attempt
        await LogLoginAttemptAsync(
            userId: null,
            username: loginRequest.Username,
            entityId: loginRequest.EntityId,
            ipAddress: ipAddress,
            userAgent: userAgent,
            result: "ERROR",
            failureReason: ex.Message
        );
        throw;
    }
}

private async Task LogLoginAttemptAsync(
    int? userId,
    string username,
    int entityId,
    string ipAddress,
    string? userAgent,
    string result,
    string? sessionToken = null,
    string? failureReason = null)
{
    try
    {
        var loginHistory = new LoginHistory
        {
            UserId = userId,
            Username = username,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            LoginResult = result,
            SessionToken = sessionToken,
            FailureReason = failureReason,
            LoginTimestamp = DateTime.UtcNow
        };

        _context.LoginHistory.Add(loginHistory);
        await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to log login attempt for user: {Username}", username);
        // Don't throw - login should succeed even if logging fails
    }
}
```

### Benefits

✅ **Complete audit trail** - Every login attempt logged  
✅ **Security monitoring** - Detect brute force attacks from same IP  
✅ **Geographic tracking** - Analyze login patterns by IP location  
✅ **Session correlation** - Link session tokens to login events  
✅ **Compliance** - Meet regulatory requirements for access logs  
✅ **Forensics** - Investigate security incidents with full history  
✅ **User analytics** - Track when/where users access the system  

---

## User Management Blazor Pages

### Current Vanilla JS Implementation

The system has three comprehensive user/role management pages:

#### 1. Users Management (`users.html`)

**Features:**
- ✅ **Users table** with ReusableTable component
- ✅ **Summary cards**: Total users, active users, inactive users
- ✅ **User CRUD**: Add, edit user details
- ✅ **Password management**: Change password, force password change
- ✅ **Lock/unlock users**: Manual user locking
- ✅ **Role assignment**: Manage user-to-role mappings
- ✅ **Entity assignment**: Assign users to entities
- ✅ **Status tracking**: Last login, password age, lock status
- ✅ **Context buttons**: Refresh, add user, navigate to roles

**Key UI Elements:**
```html
<!-- Summary Section -->
<div class="users-summary">
    <div class="summary-card">👥 Total Users</div>
    <div class="summary-card">✅ Active Users</div>
    <div class="summary-card">🚫 Inactive Users</div>
</div>

<!-- Users Table Columns -->
- ID, Username, Full Name, Email, Phone
- Entity Name, Active Status, Lock Status
- Last Login, Password Changed Date, Password Age
- Password Change Required
- Actions: Edit, Manage Roles, Change Password, Force Password Change, Lock/Unlock
```

**Dialogs:**
1. **Create/Edit User Dialog** - User details form
2. **Manage User Roles Dialog** - Drag-and-drop role assignment
3. **Change Password Dialog** - Admin password reset

#### 2. Roles Management (`roles.html`)

**Features:**
- ✅ **Roles table** with ReusableTable component
- ✅ **Role CRUD**: Add, edit, delete roles
- ✅ **User count per role**: Shows number of assigned users
- ✅ **Action count per role**: Shows number of permissions
- ✅ **Import/Export**: Actions, role-actions mappings, complete packages
- ✅ **Context buttons**: Refresh, add role, import/export, navigate to users

**Key UI Elements:**
```html
<!-- Roles Table Columns -->
- ID, Role Name
- User Count (assigned users)
- Action Count (permissions)
- Actions: Manage Users, Edit, Delete

<!-- Import/Export Options -->
- Export Actions (JSON)
- Import Actions (JSON)
- Export Role-Actions Mappings (JSON)
- Import Role-Actions Mappings (JSON)
- Export Complete Package (all data)
- Import Complete Package (all data)
```

#### 3. Role Details (`roledetails.html`)

**Features:**
- ✅ **Role information card** - Name, description, edit mode
- ✅ **Permissions grid** - All system actions with checkboxes
- ✅ **Users list** - All users assigned to this role
- ✅ **Group by action type** - Organize permissions by category
- ✅ **Bulk selection** - Select all/none per category
- ✅ **Search/filter** - Find specific permissions
- ✅ **Save changes** - Batch update role permissions

**Key UI Elements:**
```html
<!-- Role Info Card -->
- Role Name (editable)
- Description (editable)
- Edit/Save/Cancel buttons

<!-- Permissions Grid -->
- Grouped by Action Type: Menu, Button, API, Report, etc.
- Checkbox per action: Action Name, Display Name
- Select All/None per group
- Search bar to filter actions

<!-- Users List Card -->
- All users with this role
- Remove user from role button
```

### Proposed Blazor Migration

#### Page Structure

1. **`Pages/Security/Users.razor`** - User management (replaces users.html)
2. **`Pages/Security/Roles.razor`** - Role management (replaces roles.html)
3. **`Pages/Security/RoleDetails.razor`** - Role permissions (replaces roledetails.html)
4. **`Pages/Security/LoginHistory.razor`** - NEW: Login audit trail
5. **`Pages/Security/ActionAuditLog.razor`** - NEW: Action security logs

#### Component Breakdown

**Shared Components:**
- `Components/Security/UserFormDialog.razor` - Add/edit user modal
- `Components/Security/RoleFormDialog.razor` - Add/edit role modal
- `Components/Security/ManageUserRolesDialog.razor` - Drag-and-drop role assignment
- `Components/Security/ManageRoleUsersDialog.razor` - User list for role
- `Components/Security/ChangePasswordDialog.razor` - Admin password reset
- `Components/Security/PermissionsGrid.razor` - Grouped permissions checkboxes

**Service Layer:**
- `Services/UserManagementService.cs` - User CRUD operations
- `Services/RoleManagementService.cs` - Role CRUD operations
- `Services/SecurityAuditService.cs` - Audit log queries

#### Implementation Priority

**Phase 1 - Core Pages (Week 1-2):**
1. ✅ Implement `Users.razor` with all features from users.html
2. ✅ Implement `Roles.razor` with all features from roles.html
3. ✅ Implement `RoleDetails.razor` with permissions grid

**Phase 2 - Audit Logs (Week 3):**
4. ✅ Implement `LoginHistory.razor` - Display login_history table
5. ✅ Implement `ActionAuditLog.razor` - Display action_audit_logs table
6. ✅ Add filters: Date range, user, IP address, result

**Phase 3 - Import/Export (Week 4):**
7. ✅ Implement Excel/JSON export for all security data
8. ✅ Implement import dialogs with validation
9. ✅ Add bulk operations (bulk user creation, bulk role assignment)

#### API Endpoints Needed

Most endpoints already exist, need to add:

```csharp
// GET /api/security/login-history?userId=&fromDate=&toDate=&result=
[HttpGet("login-history")]
public async Task<IActionResult> GetLoginHistory(
    int? userId, DateTime? fromDate, DateTime? toDate, string? result);

// GET /api/security/action-audit?userId=&actionName=&fromDate=&toDate=&result=
[HttpGet("action-audit")]
public async Task<IActionResult> GetActionAuditLogs(
    int? userId, string? actionName, DateTime? fromDate, DateTime? toDate, string? result);

// GET /api/security/suspicious-activity
[HttpGet("suspicious-activity")]
public async Task<IActionResult> GetSuspiciousActivity();
```

---

**Document Status**: ✅ Ready for Review  
**Estimated Implementation Time**: 4 weeks (security) + 4 weeks (user management)  
**Risk Level**: Low (mirrors proven vanilla JS pattern)
