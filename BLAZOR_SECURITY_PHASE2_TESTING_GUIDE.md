# Phase 2 Testing Guide - Blazor Security

**Date**: January 18, 2026  
**Status**: Phase 2 In Progress - Ready for Testing

---

## Testing Objectives

Phase 2 focuses on validating the security implementation through:
1. **Pilot Implementation**: Students page with real security
2. **Auto-Create Feature**: Verify missing actions are created automatically
3. **Audit Logging**: Confirm all actions are logged
4. **Fail-Secure Behavior**: Verify access denied without permissions

---

## Test 1: Students Page Security

### Prerequisites
- Backend API running on http://localhost:5082
- Blazor Server running
- User authenticated with valid session
- PostgreSQL database accessible

### Test Steps

**1. Page Access Control**

a. **Test Access Granted**:
```
1. Login as user with "students" page permission
2. Navigate to /students
3. Expected: Page loads successfully
4. Verify: Page title shows "תלמידים - [School Name]"
5. Verify: Student list displays
```

b. **Test Access Denied** (future test when role restrictions added):
```
1. Login as user WITHOUT "students" page permission
2. Navigate to /students
3. Expected: Alert "אין לך הרשאה לגשת לעמוד זה"
4. Expected: Navigate back to previous page (history.back)
5. Verify: Page does not load
```

**2. Context Button Actions**

Test each button on the Students page:

| Button | Action Name | Expected Behavior |
|--------|-------------|-------------------|
| רענן נתונים | `students_refreshData` | Reloads student data |
| העלה קובץ תלמידים | `students_uploadFile` | Shows upload dialog (placeholder) |
| תמחור מרוכז | `students_bulkPricing` | Shows bulk pricing confirmation |
| הפקת מסמכים | `students_generateDocuments` | Shows document generation modal |
| חזרה למסך בית הספר | `students_backToSchool` | Navigates to /schooldashboard |

**Test Procedure** (for each button):
```
1. Click button
2. Expected: No immediate alert (permission granted by default)
3. Expected: Action executes (dialog shows, navigation works, etc.)
4. Check browser console: 
   - "✅ Page access granted: students"
   - No security errors
5. Check backend logs:
   - Action audit log entry created
   - Action auto-created if first time
```

**3. Table Row Actions**

Test the view student button:

```
1. Click view icon (👁️) next to any student
2. Expected: Navigation to /student page
3. Expected: Student ID stored in session
4. Verify audit log includes:
   - ActionName: "students_viewStudent"
   - ActionParams: "studentId=123,name=יוסי כהן"
```

---

## Test 2: Auto-Create Feature

### Using Security Test Page

**1. Access Test Page**:
```
1. Navigate to /security-test
2. Expected: Test page loads (no menu item needed)
3. Page shows 5 test sections
```

**2. Test Basic Action**:
```
1. Click "Test Basic Action" button
2. Expected: Alert "Test 1: Basic Action - Success!"
3. Expected: Result message shows below button
4. Check backend logs:
   [Warning] Action 'securitytest_basicAction' not found in database. Auto-creating...
   [Information] Action 'securitytest_basicAction' auto-created successfully
```

**3. Verify Database Entry**:
```sql
SELECT * FROM petel_schema.system_actions
WHERE action_name = 'securitytest_basicAction';
```

**Expected Result**:
```
| id  | action_name               | description                      | is_active |
|-----|---------------------------|----------------------------------|-----------|
| XXX | securitytest_basicAction  | Auto-created: securitytest_...   | true      |
```

**4. Verify No Role Assignments**:
```sql
SELECT * FROM petel_schema.roles_actions ra
JOIN petel_schema.system_actions sa ON ra.action_id = sa.id
WHERE sa.action_name = 'securitytest_basicAction';
```

**Expected Result**: No rows (fail-secure: no roles assigned)

**5. Test Access Denied on Second Click**:
```
1. Click "Test Basic Action" button again
2. Expected: Alert "אין לך הרשאה לבצע פעולה זו"
3. Expected: Action does NOT execute
4. Verify: Result message does NOT update
```

**6. Grant Permission**:
```sql
-- Get your role ID
SELECT r.id, r.role_name
FROM petel_schema.roles r
JOIN petel_schema.user_roles ur ON r.id = ur.role_id
WHERE ur.user_id = YOUR_USER_ID;

-- Get action ID
SELECT id FROM petel_schema.system_actions
WHERE action_name = 'securitytest_basicAction';

-- Grant permission
INSERT INTO petel_schema.roles_actions (role_id, action_id, created_at)
VALUES (YOUR_ROLE_ID, ACTION_ID, CURRENT_TIMESTAMP);
```

**7. Test Access Granted After Permission Grant**:
```
1. Refresh page (clear session cache)
2. Click "Test Basic Action" button
3. Expected: Alert "Test 1: Basic Action - Success!"
4. Expected: Action executes successfully
5. Result message updates
```

---

## Test 3: Action Parameters & Audit Logging

**1. Test Action with Parameters**:
```
1. On /security-test page
2. Click "Test Action with Parameters"
3. Expected: Success alert
4. Verify audit log includes parameters
```

**2. Check Audit Log Entry**:
```sql
SELECT aal.*, sa.action_name, u.username
FROM petel_schema.action_audit_logs aal
JOIN petel_schema.system_actions sa ON aal.action_id = sa.id
JOIN petel_schema.users u ON aal.user_id = u.id
WHERE sa.action_name = 'securitytest_actionWithParams'
ORDER BY aal.created_at DESC
LIMIT 1;
```

**Expected Fields**:
- `action_name`: "securitytest_actionWithParams"
- `user_id`: Your user ID
- `screen_name`: "securitytest"
- `function_name`: "TestActionWithParams"
- `event_type`: "BUTTON_CLICK"
- `action_params`: "testId=123,testName=TestUser"
- `success`: true
- `created_at`: Recent timestamp

---

## Test 4: Hide If No Access

**1. Test Hidden Button**:
```
1. On /security-test page
2. Look for "Test 3: Hide Button If No Access" section
3. Expected: Button is HIDDEN (not rendered in DOM)
   - Because action not assigned to your role after auto-create
4. Inspect HTML: Button element should not exist
```

**2. Grant Permission and Verify Button Appears**:
```sql
-- Grant permission (use same SQL as Test 2, step 6)
INSERT INTO petel_schema.roles_actions (role_id, action_id, created_at)
VALUES (YOUR_ROLE_ID, HIDDEN_ACTION_ID, CURRENT_TIMESTAMP);
```

```
1. Refresh page
2. Expected: Button now VISIBLE
3. Click button
4. Expected: Action executes successfully
```

---

## Test 5: Disabled State

**1. Test Disabled Button**:
```
1. On /security-test page, Test 4 section
2. Button is disabled by default (checkbox unchecked)
3. Try clicking button
4. Expected: Nothing happens (button grayed out)
```

**2. Enable Button**:
```
1. Check "Enable Button" checkbox
2. Button becomes enabled (if you have permission)
3. Click button
4. Expected: Action executes
```

---

## Test 6: SecurePageBase Helper Method

**1. Test ExecuteSecureActionAsync**:
```
1. On /security-test page, Test 5 section
2. Click "Test Helper Method" (standard button, not SecureButton)
3. Expected: Result shows "🔄 Executing with helper method..."
4. After permission check:
   - If no permission: "❌ Permission denied by helper method"
   - If permission granted: "✅ Helper method executed successfully"
```

**2. Verify Audit Log**:
```sql
SELECT * FROM petel_schema.action_audit_logs aal
JOIN petel_schema.system_actions sa ON aal.action_id = sa.id
WHERE sa.action_name = 'securitytest_helperAction'
ORDER BY aal.created_at DESC;
```

**Expected**: Entry with `action_params = "method=helper,testId=999"`

---

## Test 7: Performance & Session Caching

**1. Test Session Caching**:
```
1. On /students page
2. Click multiple buttons rapidly
3. Expected: No lag or multiple API calls
4. Check browser network tab:
   - Session API calls cached for 1 minute
   - Security verification calls per action (not cached)
```

**2. Test Loading States**:
```
1. Click "תמחור מרוכז" button
2. Expected: Button shows loading spinner
3. Expected: Button disabled during processing
4. After completion: Button re-enabled
```

---

## Test 8: Error Handling

**1. Test Backend API Down**:
```
1. Stop backend API
2. Try clicking any secured button
3. Expected: Console error logged
4. Expected: Alert "שגיאה בביצוע הפעולה"
5. Expected: Action does NOT execute (fail-secure)
6. No crash or blank page
```

**2. Test Session Expired**:
```
1. Logout in another tab
2. Return to /students page
3. Click any button
4. Expected: Redirect to login or home
5. Expected: No crash
```

---

## Test 9: Hebrew Localization

**1. Verify All Error Messages in Hebrew**:

| Scenario | Expected Message |
|----------|------------------|
| No permission | אין לך הרשאה לבצע פעולה זו |
| Page access denied | אין לך הרשאה לגשת לעמוד זה |
| Generic error | שגיאה בביצוע הפעולה |

**2. Test Each Message**:
```
1. Trigger each scenario
2. Verify Hebrew text displays correctly (RTL)
3. Verify no English fallback
4. Verify alert dialogs show Hebrew
```

---

## Test Results Template

### Test Session: [Date & Time]

**Environment**:
- Backend: http://localhost:5082
- Frontend: [URL]
- User: [Username]
- Role: [Role Name]
- Browser: [Browser Name & Version]

**Test Results**:

| Test # | Test Name | Status | Notes |
|--------|-----------|--------|-------|
| 1.1 | Page Access Granted | ✅ / ❌ | |
| 1.2 | Context Buttons Work | ✅ / ❌ | |
| 1.3 | Table Actions Work | ✅ / ❌ | |
| 2.1 | Auto-Create Basic | ✅ / ❌ | |
| 2.2 | Database Verification | ✅ / ❌ | |
| 2.3 | No Role Assignment | ✅ / ❌ | |
| 2.4 | Access Denied | ✅ / ❌ | |
| 2.5 | Grant Permission | ✅ / ❌ | |
| 2.6 | Access Granted | ✅ / ❌ | |
| 3.1 | Action Parameters | ✅ / ❌ | |
| 3.2 | Audit Log Entry | ✅ / ❌ | |
| 4.1 | Button Hidden | ✅ / ❌ | |
| 4.2 | Button Shows After Grant | ✅ / ❌ | |
| 5.1 | Disabled State | ✅ / ❌ | |
| 5.2 | Enabled State | ✅ / ❌ | |
| 6.1 | Helper Method | ✅ / ❌ | |
| 6.2 | Helper Audit Log | ✅ / ❌ | |
| 7.1 | Session Caching | ✅ / ❌ | |
| 7.2 | Loading States | ✅ / ❌ | |
| 8.1 | Backend Down | ✅ / ❌ | |
| 8.2 | Session Expired | ✅ / ❌ | |
| 9.1 | Hebrew Messages | ✅ / ❌ | |

**Issues Found**: [List any issues]

**Overall Assessment**: ✅ Pass / ❌ Fail

---

## Success Criteria

Phase 2 testing is considered successful when:

- ✅ All 21 test cases pass
- ✅ Auto-create feature works for all test actions
- ✅ Audit logs contain all required fields
- ✅ Fail-secure behavior verified (access denied without permissions)
- ✅ Performance acceptable (no lag with session caching)
- ✅ No crashes or errors in normal operation
- ✅ Hebrew messages display correctly
- ✅ Loading states work properly
- ✅ Error handling graceful

**Next Phase**: Unit Tests & Integration Tests

---

## Quick Test Commands

```bash
# Start backend
cd PetelApp.Api
dotnet run

# Start Blazor Server (separate terminal)
cd PetelApp.BlazorServer
dotnet run

# Access test URLs
# Students page: http://localhost:[port]/students
# Test page: http://localhost:[port]/security-test
```

```sql
-- Quick verification queries

-- Check auto-created actions
SELECT * FROM petel_schema.system_actions
WHERE action_name LIKE 'securitytest_%'
   OR action_name LIKE 'students_%'
ORDER BY created_at DESC;

-- Check recent audit logs
SELECT aal.created_at, sa.action_name, u.username, aal.event_type, aal.success
FROM petel_schema.action_audit_logs aal
JOIN petel_schema.system_actions sa ON aal.action_id = sa.id
JOIN petel_schema.users u ON aal.user_id = u.id
ORDER BY aal.created_at DESC
LIMIT 20;

-- Check role assignments
SELECT r.role_name, sa.action_name, ra.created_at
FROM petel_schema.roles_actions ra
JOIN petel_schema.roles r ON ra.role_id = r.id
JOIN petel_schema.system_actions sa ON ra.action_id = sa.id
WHERE sa.action_name LIKE 'students_%'
   OR sa.action_name LIKE 'securitytest_%'
ORDER BY ra.created_at DESC;
```

---

**Happy Testing! 🧪**
