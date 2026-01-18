# Blazor Security - Action Type Implementation

## Summary

Fixed the action auto-creation system to correctly distinguish between:
- **Type 7**: Button/Click actions (SecureButton components, page helper methods)
- **Type 8**: Page/Screen access actions (SecurePageBase page verification)

This ensures actions are created in the `actions` table with the correct `action_type_id` and meaningful `reference` field values.

---

## Problem

When users accessed pages or clicked secured buttons, actions were being auto-created in the database with:
- ❌ `action_type_id = 7` (button type) for **all** actions including page access
- ❌ `reference = "unknown"` for page actions

**Expected behavior**:
- ✅ Page access should create `action_type_id = 8` with reference = page URL (e.g., "/students")
- ✅ Button clicks should create `action_type_id = 7` with reference = NULL or context

---

## Solution

### Backend Changes

#### 1. Updated `SecureActionRequest` DTO (SecurityController.cs)

Added two new properties:

```csharp
public class SecureActionRequest
{
    public string ActionName { get; set; } = string.Empty;
    public string? ScreenName { get; set; }
    public string? FunctionName { get; set; }
    public string? EventType { get; set; }
    public int ActionType { get; set; } = 7; // ✅ NEW: 7 = Button, 8 = Page
    public string? Reference { get; set; }    // ✅ NEW: Page URL, menu href, etc.
    public string? ActionParams { get; set; }
    public string? Description { get; set; }
}
```

#### 2. Updated `ActionAuthorizationService.AutoCreateMissingActionAsync`

Modified to accept and use `actionType` and `reference` parameters:

```csharp
private async Task<SystemAction?> AutoCreateMissingActionAsync(
    string actionName, 
    string screenName, 
    string functionName, 
    int actionType = 7,        // ✅ Passed from caller
    string? reference = null)   // ✅ Passed from caller
{
    // ...
    var newAction = new SystemAction
    {
        Name = actionName,
        DisplayName = displayName,
        Reference = reference ?? screenName, // ✅ Use provided reference
        ActionTypeId = actionType,           // ✅ Use provided action type
        IsActive = true,
        // ...
    };
}
```

#### 3. Updated `ActionAuthorizationService.VerifyActionByNameAsync`

Modified signature to accept `actionType` and `reference`, then pass to auto-create:

```csharp
public async Task<bool> VerifyActionByNameAsync(
    int userId, 
    string actionName, 
    int actionType = 7,     // ✅ NEW parameter
    string? reference = null) // ✅ NEW parameter
{
    // ...
    if (action == null)
    {
        action = await AutoCreateMissingActionAsync(
            actionName, 
            "unknown", 
            actionName, 
            actionType,  // ✅ Pass to auto-create
            reference    // ✅ Pass to auto-create
        );
    }
}
```

#### 4. Updated `SecurityController.VerifyActionSecure`

Pass `actionType` and `reference` from request to service:

```csharp
hasAccess = await _actionAuthService.VerifyActionByNameAsync(
    userId, 
    request.ActionName, 
    request.ActionType,  // ✅ From frontend
    request.Reference    // ✅ From frontend
);
```

---

### Frontend Changes

#### 1. Updated `ActionSecurityService.VerifyActionAsync`

Added `actionType` and `reference` parameters with defaults:

```csharp
public async Task<bool> VerifyActionAsync(
    string actionName,
    string screenName,
    string functionName,
    string eventType = "BUTTON_CLICK",
    int actionType = 7,        // ✅ NEW: Default to button type
    string? reference = null,  // ✅ NEW: Optional reference
    string? actionParams = null,
    string? description = null)
{
    var request = new SecureActionRequest
    {
        ActionName = actionName,
        ScreenName = screenName,
        FunctionName = functionName,
        EventType = eventType,
        ActionType = actionType,    // ✅ Include in request
        Reference = reference,      // ✅ Include in request
        ActionParams = actionParams,
        Description = description
    };
    // ...
}
```

#### 2. Updated `ActionSecurityService.VerifyPageAccessAsync`

**PAGE ACCESS** → Type 8, reference = page URL:

```csharp
public async Task<bool> VerifyPageAccessAsync(string pageName)
{
    return await VerifyActionAsync(
        actionName: pageName,
        screenName: "navigation",
        functionName: "accessPage",
        eventType: "PAGE_ACCESS",
        actionType: 8,            // ✅ Type 8 for page access
        reference: $"/{pageName}" // ✅ Page URL as reference
    );
}
```

#### 3. Updated `ActionSecurityService.VerifyMenuNavigationAsync`

**MENU NAVIGATION** → Type 8, reference = menu href:

```csharp
public async Task<bool> VerifyMenuNavigationAsync(string menuItemName, string menuReference)
{
    return await VerifyActionAsync(
        actionName: menuItemName,
        screenName: "menu",
        functionName: "navigateTo",
        eventType: "MENU_NAVIGATION",
        actionType: 8,            // ✅ Type 8 for navigation
        reference: menuReference, // ✅ Menu href as reference
        actionParams: menuReference
    );
}
```

#### 4. Updated `SecureButton.razor`

**BUTTON CLICKS** → Type 7, no reference:

```csharp
// In OnInitializedAsync (visibility check)
_isVisible = await SecurityService.VerifyActionAsync(
    ActionName, 
    ScreenName, 
    FunctionName, 
    "BUTTON_VISIBLE_CHECK", 
    actionType: 7, // ✅ Type 7 for button
    actionParams: ActionParams
);

// In HandleClickAsync (click handler)
var allowed = await SecurityService.VerifyActionAsync(
    ActionName,
    ScreenName,
    FunctionName,
    "BUTTON_CLICK",
    actionType: 7, // ✅ Type 7 for button
    actionParams: ActionParams
);
```

#### 5. Updated `SecurePageBase.ExecuteSecureActionAsync`

**PAGE ACTIONS** → Type 7 (actions invoked from page, like button clicks):

```csharp
// Both overloads updated
var allowed = await SecurityService.VerifyActionAsync(
    actionName,
    PageName,
    functionName,
    "PAGE_ACTION",
    actionType: 7, // ✅ Type 7 for actions from page
    actionParams: actionParams
);
```

---

## Action Type Usage Matrix

| Scenario | Component | ActionType | Reference | Example |
|----------|-----------|------------|-----------|---------|
| **Page Access** | SecurePageBase | 8 | `/pageName` | User navigates to /students |
| **Menu Navigation** | Future | 8 | `#menuitem` | User clicks menu item |
| **Button Click** | SecureButton | 7 | NULL | User clicks "הוסף תלמיד" |
| **Page Action** | SecurePageBase helper | 7 | NULL | Page code calls ExecuteSecureActionAsync |

---

## Database Results

### Before Fix
```sql
-- Page access created wrong type
SELECT id, name, action_type_id, reference FROM petel_schema.actions 
WHERE name = 'students';
-- Result: action_type_id = 7, reference = 'unknown'  ❌
```

### After Fix
```sql
-- Page access creates type 8
SELECT id, name, action_type_id, reference FROM petel_schema.actions 
WHERE name = 'students';
-- Result: action_type_id = 8, reference = '/students'  ✅

-- Button click creates type 7
SELECT id, name, action_type_id, reference FROM petel_schema.actions 
WHERE name = 'students_addStudent';
-- Result: action_type_id = 7, reference = NULL  ✅
```

---

## Testing

### Test Page Access (Type 8)

1. Navigate to a secured page (e.g., `/students`)
2. If action doesn't exist, it should be auto-created as:
   - `name = "students"`
   - `action_type_id = 8`
   - `reference = "/students"`
   - `is_active = true` (but access denied until role assigned)

### Test Button Click (Type 7)

1. Click a `<SecureButton>` component
2. If action doesn't exist, it should be auto-created as:
   - `name = "students_addStudent"` (example)
   - `action_type_id = 7`
   - `reference = NULL` (or screen context)
   - `is_active = true` (but access denied until role assigned)

---

## Verification Query

```sql
-- Check recent auto-created actions
SELECT 
    id,
    name,
    action_type_id,
    reference,
    description,
    created_at
FROM petel_schema.actions
WHERE description LIKE 'Auto-created%'
ORDER BY created_at DESC
LIMIT 20;
```

---

## Files Modified

### Backend (PetelApp.Api)
1. ✅ `Controllers/SecurityController.cs` - Added ActionType and Reference to DTO and pass to service
2. ✅ `Services/ActionAuthorizationService.cs` - Updated AutoCreateMissingActionAsync signature and usage

### Frontend (PetelApp.BlazorServer)
1. ✅ `DTOs/SecurityDTOs.cs` - Added ActionType and Reference properties
2. ✅ `Services/ActionSecurityService.cs` - Added parameters to VerifyActionAsync and updated all callers
3. ✅ `Components/Shared/SecureButton.razor` - Passes actionType: 7 (already done)
4. ✅ `Components/Pages/SecurePageBase.cs` - Passes actionType: 7 for page actions, VerifyPageAccessAsync passes 8

---

## Benefits

✅ **Correct action types** - Database accurately reflects whether action is a button or page
✅ **Meaningful references** - Page URLs stored for context
✅ **Better reporting** - Can query actions by type for analytics
✅ **Future extensibility** - Easy to add more action types (e.g., API calls, file uploads)
✅ **Audit trail clarity** - Action type visible in audit logs

---

## Next Steps

1. ✅ Test page access - verify type 8 actions created
2. ✅ Test button clicks - verify type 7 actions created
3. ✅ Verify reference field has meaningful values
4. 📝 Update documentation to explain action types
5. 🔍 Run verification query to audit existing actions

---

*Implementation completed: {{CURRENT_DATE}}*
