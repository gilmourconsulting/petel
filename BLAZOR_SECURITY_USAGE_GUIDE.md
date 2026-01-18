# Blazor Action Security - Usage Guide

**Project**: Petel Educational Management System - Blazor Server  
**Date**: January 18, 2026  
**Purpose**: Developer guide for implementing secure pages and actions

---

## Table of Contents

1. [Security Action Types](#security-action-types)
2. [Quick Start](#quick-start)
3. [Secure Pages](#secure-pages)
4. [Secure Buttons](#secure-buttons)
5. [Secure Actions](#secure-actions)
6. [Testing Auto-Create](#testing-auto-create)
7. [Common Patterns](#common-patterns)
8. [Troubleshooting](#troubleshooting)

---

## Security Action Types

**The system implements three levels of security**:

### 1. Screen/Page Security 🔒

**What it protects**: Access to entire pages/screens

**How it works**:
- Verified when user navigates to a page
- Checked in `SecurePageBase.OnInitializedAsync()`
- Uses `SecurityService.VerifyPageAccessAsync(pageName)`

**On Authorization Failure**:
- ✅ Shows alert: "אין לך הרשאה לגשת לעמוד זה"
- ✅ Navigates back to previous page (using `history.back()`)
- ✅ Page content never loads

**Example**: User tries to access `/students` without permission

### 2. Action/Button Security 🔘

**What it protects**: Individual actions and button clicks

**How it works**:
- Verified when user clicks a button
- Checked in `SecureButton.HandleClickAsync()`
- Uses `SecurityService.VerifyActionAsync(actionName, ...)`

**On Authorization Failure**:
- ✅ Shows alert: "אין לך הרשאה לבצע פעולה זו"
- ✅ **Stays on current page** (no navigation)
- ✅ Action/function does not execute

**Example**: User clicks "Delete Student" button without permission

### 3. Menu Security 📋

**What it protects**: Visibility of menu items

**How it works**:
- Filtered server-side by `MenuController.GetMenuItems()`
- Uses `ActionAuthorizationService` to check user roles
- Menu items without permission are not returned to client

**On Authorization Failure**:
- ✅ Menu item **not rendered** in UI
- ✅ User never sees unauthorized menu items
- ✅ Server-side filtering (secure, cannot be bypassed)

**Example**: "Security Management" menu item only visible to administrators

### Authorization Failure Patterns

| Security Type | Failure Behavior | User Impact |
|---------------|-----------------|-------------|
| **Page Access** | Navigate back to previous page | User returns to where they came from |
| **Button Click** | Stay on current page, show alert | User can continue working on page |
| **Menu Item** | Not rendered in menu | User never sees the option |

**Key Principle**: **Double Protection**
- Menu items filtered → User doesn't see unauthorized options
- Page access checked → Even if user types URL directly, access denied
- Button actions checked → Even if button visible, action won't execute

---

## Quick Start

### Prerequisites

All Phase 1 infrastructure is already implemented:
- ✅ `ActionSecurityService` registered in DI container
- ✅ `SecureButton` component available
- ✅ `SecurePageBase` base class available
- ✅ Backend security API endpoints active

### 3-Step Implementation

**Step 1**: Inherit from `SecurePageBase`
```csharp
@page "/mypage"
@inherits SecurePageBase

@code {
    protected override string PageName => "mypage";
}
```

**Step 2**: Replace buttons with `SecureButton`
```razor
<SecureButton 
    ActionName="mypage_saveData"
    ScreenName="@PageName"
    FunctionName="SaveData"
    OnClick="SaveData">
    שמור נתונים
</SecureButton>
```

**Step 3**: Test!
- Access page → Automatic page access check
- Click button → Automatic action security check
- Missing actions → Auto-created in database

---

## Secure Pages

### Pattern 1: Basic Secure Page

**Minimal implementation** with automatic page access verification:

```csharp
@page "/students"
@inherits SecurePageBase
@inject ApiService ApiService

<div class="main-container">
    <h1>רשימת תלמידים</h1>
    <!-- Page content here -->
</div>

@code {
    // REQUIRED: Page identifier for security
    protected override string PageName => "students";

    // Optional: Override for page initialization
    protected override async Task OnPageInitializedAsync()
    {
        // Called AFTER page access has been verified
        await LoadStudents();
    }

    private async Task LoadStudents()
    {
        // Your data loading logic
    }
}
```

**What happens automatically:**
1. ✅ User navigates to `/students`
2. ✅ `SecurePageBase.OnInitializedAsync()` runs
3. ✅ Calls `SecurityService.VerifyPageAccessAsync("students")`
4. ✅ If allowed → Calls `OnPageInitializedAsync()` → Loads data
5. ✅ If denied → Shows alert "אין לך הרשאה לגשת לעמוד זה" → Navigates back to previous page

### Pattern 2: Page with Secure Actions

**Using `ExecuteSecureActionAsync()` helper**:

```csharp
@page "/students"
@inherits SecurePageBase
@inject ApiService ApiService

<div class="main-container">
    <button class="btn-primary" @onclick="DeleteSelectedStudents">
        מחק תלמידים נבחרים
    </button>
</div>

@code {
    protected override string PageName => "students";

    private async Task DeleteSelectedStudents()
    {
        // ExecuteSecureActionAsync verifies permission BEFORE executing
        var executed = await ExecuteSecureActionAsync(
            actionName: "students_deleteMultiple",
            functionName: "DeleteSelectedStudents",
            action: async () =>
            {
                // This code ONLY runs if permission is granted
                await ApiService.PostAsync("students/delete-batch", _selectedIds);
                await LoadStudents();
            },
            actionParams: $"count={_selectedIds.Count}"
        );

        if (executed)
        {
            await JSRuntime.InvokeVoidAsync("alert", "תלמידים נמחקו בהצלחה");
        }
        // If denied, user already saw access denied message
    }
}
```

**What happens:**
1. ✅ User clicks "מחק תלמידים נבחרים"
2. ✅ `ExecuteSecureActionAsync()` verifies permission first
3. ✅ If allowed → Executes lambda → Returns `true`
4. ✅ If denied → Shows alert → Returns `false` → Lambda never runs

### Pattern 3: Actions with Return Values

**Using generic `ExecuteSecureActionAsync<T>()`**:

```csharp
@code {
    private async Task ExportStudents()
    {
        var (success, fileBytes) = await ExecuteSecureActionAsync<byte[]>(
            actionName: "students_export",
            functionName: "ExportStudents",
            action: async () =>
            {
                // Returns byte[] if allowed
                return await ApiService.GetAsync<byte[]>("students/export");
            }
        );

        if (success && fileBytes != null)
        {
            // Download file
            await JSRuntime.InvokeVoidAsync("downloadFile", 
                Convert.ToBase64String(fileBytes), "students.xlsx");
        }
    }
}
```

---

## Secure Buttons

### Pattern 1: Basic Secure Button

**Replace standard buttons** with security-aware version:

```razor
<!-- ❌ OLD: No security -->
<button class="btn-primary" @onclick="SaveData">שמור</button>

<!-- ✅ NEW: Automatic security check -->
<SecureButton 
    ActionName="students_saveStudent"
    ScreenName="students"
    FunctionName="SaveData"
    OnClick="SaveData"
    CssClass="btn-primary">
    שמור
</SecureButton>
```

**What happens:**
1. ✅ User clicks button
2. ✅ `SecureButton` verifies permission first
3. ✅ If allowed → Calls `OnClick` handler → Executes `SaveData()`
4. ✅ If denied → Shows alert → Handler never called
5. ✅ Shows loading spinner during processing

### Pattern 2: Hide Button if No Access

**Don't show button at all** if user lacks permission:

```razor
<SecureButton 
    ActionName="students_deleteStudent"
    ScreenName="students"
    FunctionName="DeleteStudent"
    OnClick="() => DeleteStudent(student.Id)"
    HideIfNoAccess="true">
    מחק
</SecureButton>
```

**What happens:**
1. ✅ `OnInitializedAsync()` checks permission
2. ✅ If allowed → Button visible
3. ✅ If denied → Button hidden (doesn't render at all)
4. ✅ No extra API call on click (already checked)

### Pattern 3: Action with Parameters

**Pass contextual data** for audit logging:

```razor
@foreach (var student in _students)
{
    <SecureButton 
        ActionName="students_viewStudent"
        ScreenName="students"
        FunctionName="ViewStudent"
        ActionParams="@($"studentId={student.Id},name={student.FirstName} {student.LastName}")"
        OnClick="() => ViewStudent(student.Id)">
        <img src="/images/view_icon.png" alt="צפייה" />
    </SecureButton>
}
```

**Audit log entry:**
```json
{
  "actionName": "students_viewStudent",
  "screenName": "students",
  "functionName": "ViewStudent",
  "eventType": "BUTTON_CLICK",
  "actionParams": "studentId=123,name=יוסי כהן",
  "userId": 5,
  "timestamp": "2026-01-18T10:30:00Z",
  "success": true
}
```

### Pattern 4: Disabled State

**Disable button conditionally**:

```razor
<SecureButton 
    ActionName="students_saveStudent"
    ScreenName="students"
    FunctionName="SaveData"
    OnClick="SaveData"
    Disabled="@(!_isFormValid)">
    שמור
</SecureButton>
```

**What happens:**
1. ✅ If `_isFormValid = false` → Button disabled (gray, no click)
2. ✅ If `_isFormValid = true` AND permission allowed → Button enabled
3. ✅ If `_isFormValid = true` BUT permission denied → Click shows alert

---

## Secure Actions

### Direct Service Usage

**When NOT using SecurePageBase** or need manual control:

```csharp
@inject ActionSecurityService SecurityService

@code {
    private async Task CustomAction()
    {
        var allowed = await SecurityService.VerifyActionAsync(
            actionName: "reports_generate",
            screenName: "reports",
            functionName: "GenerateReport",
            eventType: "CUSTOM_ACTION",
            actionParams: "reportType=monthly"
        );

        if (!allowed)
        {
            await JSRuntime.InvokeVoidAsync("alert", 
                SecurityService.GetAccessDeniedMessage("reports_generate"));
            return;
        }

        // Execute action
        await GenerateReport();
    }
}
```

### Menu Navigation Security

**Verify menu item access** before navigation:

```csharp
private async Task NavigateToPage(string pageName)
{
    var allowed = await SecurityService.VerifyMenuNavigationAsync(pageName);

    if (!allowed)
    {
        await JSRuntime.InvokeVoidAsync("alert", "אין לך גישה לעמוד זה");
        return;
    }

    Navigation.NavigateTo($"/{pageName}");
}
```

---

## Testing Auto-Create

### Test Scenario: New Action

**Goal**: Verify missing actions are auto-created in database

**Steps**:

1. **Create button with non-existent action**:
```razor
<SecureButton 
    ActionName="students_newFeature"
    ScreenName="students"
    FunctionName="TestNewFeature"
    OnClick="TestAction">
    Test New Feature
</SecureButton>
```

2. **Click the button**

3. **Check backend logs**:
```
[Warning] Action 'students_newFeature' not found in database. Auto-creating...
[Information] Action 'students_newFeature' auto-created with ACTIVE status and no role assignments
```

4. **Verify database**:
```sql
SELECT * FROM petel_schema.system_actions 
WHERE action_name = 'students_newFeature';
```

**Expected Result**:
```
| id  | action_name         | description                 | is_active |
|-----|---------------------|-----------------------------|-----------|
| 157 | students_newFeature | Auto-created: students_...  | true      |
```

**Expected Behavior**:
- ✅ Action auto-created in `system_actions` table
- ✅ Status = `ACTIVE`
- ✅ No role assignments in `roles_actions` table
- ✅ Access **DENIED** (fail-secure: no roles = no access)
- ✅ Audit log entry created
- ✅ User sees "אין לך הרשאה לבצע פעולה זו"

### Granting Access After Auto-Create

**Administrator workflow**:

1. Navigate to **Security Management** page
2. Find action `students_newFeature` in list
3. Assign to role (e.g., "School Manager")
4. Test again → User now has access

---

## Common Patterns

### Pattern: Context Button Section

**Secure all toolbar buttons**:

```razor
<div class="context-buttons-section">
    <SecureButton 
        ActionName="students_refreshData"
        ScreenName="@PageName"
        FunctionName="RefreshData"
        OnClick="RefreshData"
        CssClass="context-btn">
        רענן נתונים
    </SecureButton>

    <SecureButton 
        ActionName="students_uploadFile"
        ScreenName="@PageName"
        FunctionName="ShowUploadDialog"
        OnClick="ShowUploadDialog"
        CssClass="context-btn">
        העלה קובץ
    </SecureButton>

    <SecureButton 
        ActionName="students_bulkPricing"
        ScreenName="@PageName"
        FunctionName="ShowBulkPricingDialog"
        OnClick="ShowBulkPricingDialog"
        CssClass="context-btn">
        תמחור מרוכז
    </SecureButton>
</div>
```

### Pattern: Table Row Actions

**Secure view/edit/delete buttons**:

```razor
<tbody>
    @foreach (var student in _students)
    {
        <tr>
            <td>
                <SecureButton 
                    ActionName="students_viewStudent"
                    ScreenName="@PageName"
                    FunctionName="ViewStudent"
                    ActionParams="@($"studentId={student.Id}")"
                    OnClick="() => ViewStudent(student.Id)"
                    CssClass="btn-icon"
                    HideIfNoAccess="true">
                    <img src="/images/view_icon.png" alt="צפייה" class="action-icon-natural">
                </SecureButton>

                <SecureButton 
                    ActionName="students_editStudent"
                    ScreenName="@PageName"
                    FunctionName="EditStudent"
                    ActionParams="@($"studentId={student.Id}")"
                    OnClick="() => EditStudent(student.Id)"
                    CssClass="btn-icon"
                    HideIfNoAccess="true">
                    <img src="/images/edit_icon.png" alt="עריכה" class="action-icon-natural">
                </SecureButton>

                <SecureButton 
                    ActionName="students_deleteStudent"
                    ScreenName="@PageName"
                    FunctionName="DeleteStudent"
                    ActionParams="@($"studentId={student.Id}")"
                    OnClick="() => DeleteStudent(student.Id)"
                    CssClass="btn-icon"
                    HideIfNoAccess="true">
                    <img src="/images/delete_icon.png" alt="מחיקה" class="action-icon-natural">
                </SecureButton>
            </td>
            <td>@student.IdNumber</td>
            <td>@student.FirstName</td>
            <td>@student.LastName</td>
        </tr>
    }
</tbody>
```

### Pattern: Conditional Visibility

**Show different buttons based on permissions**:

```razor
@if (_showAdminControls)
{
    <SecureButton 
        ActionName="students_deleteAll"
        ScreenName="@PageName"
        FunctionName="DeleteAll"
        OnClick="DeleteAllStudents"
        HideIfNoAccess="true">
        מחק הכל
    </SecureButton>
}

@code {
    private bool _showAdminControls = false;

    protected override async Task OnPageInitializedAsync()
    {
        // Check if user has admin role
        var session = await SessionStateService.GetSessionAsync();
        _showAdminControls = session?.Roles.Any(r => r.RoleName == "Administrator") ?? false;
    }
}
```

---

## Troubleshooting

### Issue: "אין לך הרשאה" for all actions

**Possible Causes**:
1. User has no roles assigned
2. Action not assigned to user's roles
3. Action is INACTIVE in database

**Solution**:
```sql
-- Check user roles
SELECT r.* FROM petel_schema.user_roles ur
JOIN petel_schema.roles r ON ur.role_id = r.id
WHERE ur.user_id = 5;

-- Check action assignments
SELECT * FROM petel_schema.roles_actions 
WHERE action_id = (SELECT id FROM petel_schema.system_actions WHERE action_name = 'students_saveStudent');

-- Check action status
SELECT * FROM petel_schema.system_actions WHERE action_name = 'students_saveStudent';
```

### Issue: Button doesn't hide with HideIfNoAccess

**Possible Causes**:
1. ActionName typo
2. Backend API error
3. Session expired

**Debug**:
```razor
<SecureButton 
    ActionName="students_viewStudent"
    ...>
    @* Add debug output *@
    <div>Debug: @_debugMessage</div>
</SecureButton>

@code {
    private string _debugMessage = "";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var allowed = await SecurityService.VerifyActionAsync(
                "students_viewStudent", PageName, "ViewStudent", "PAGE_LOAD", null);
            _debugMessage = allowed ? "Allowed" : "Denied";
        }
        catch (Exception ex)
        {
            _debugMessage = $"Error: {ex.Message}";
        }
    }
}
```

### Issue: Action not auto-created

**Possible Causes**:
1. Backend API not running
2. Database connection issue
3. Auto-create feature disabled

**Verify**:
```bash
# Check backend logs
tail -f PetelApp.Api/logs/api-log-YYYYMMDD.txt

# Expected output on first click:
# [Warning] Action 'students_newAction' not found in database. Auto-creating...
# [Information] Action 'students_newAction' auto-created successfully
```

### Issue: Performance degradation

**Possible Causes**:
1. Too many permission checks
2. No session caching
3. N+1 query problem

**Solution**:
- ✅ `SessionStateService` caches session for 1 minute
- ✅ `HideIfNoAccess` checks on init, not every click
- ✅ Backend uses indexed queries

**Optimize**:
```csharp
// ❌ WRONG: Check permission in loop
foreach (var student in _students)
{
    var allowed = await SecurityService.VerifyActionAsync(...);  // N queries!
}

// ✅ CORRECT: Check once, use HideIfNoAccess
<SecureButton ... HideIfNoAccess="true" />  // One check on init
```

---

## Next Steps

1. **Migrate existing pages**: Replace buttons with `SecureButton`, inherit from `SecurePageBase`
2. **Test auto-create**: Create test actions, verify database entries
3. **Security audit**: Review all actions, assign to roles
4. **User training**: Teach admins how to manage action permissions

---

## Reference

- **Design Document**: [BLAZOR_ACTION_SECURITY_DESIGN.md](BLAZOR_ACTION_SECURITY_DESIGN.md)
- **Implementation Log**: [BLAZOR_SECURITY_IMPLEMENTATION_LOG.md](BLAZOR_SECURITY_IMPLEMENTATION_LOG.md)
- **Backend API**: `PetelApp.Api/Controllers/SecurityController.cs`
- **Original Security**: `PetelApp.Api/js/action-security.js` (vanilla JS reference)
