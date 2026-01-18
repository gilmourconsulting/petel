# Action Type Verification Tests

## Pre-Test: Check Current State

```sql
-- See all action types in database
SELECT id, name as action_type_name 
FROM petel_schema.action_types 
ORDER BY id;

-- Expected:
-- 7 | Button
-- 8 | Screen (or Page)

-- Check existing actions before test
SELECT 
    name,
    action_type_id,
    reference,
    is_active,
    description
FROM petel_schema.actions
WHERE name IN ('students', 'securitytest', 'students_addStudent', 'securitytest_basicAction')
ORDER BY name;
```

## Test 1: Page Access (Type 8)

**Objective**: Verify that navigating to a page creates a Type 8 action.

### Steps:
1. Delete test action if exists:
   ```sql
   DELETE FROM petel_schema.actions WHERE name = 'securitytest';
   ```

2. Navigate to SecurityTest page: `https://localhost:7169/securitytest`

3. Check action was created:
   ```sql
   SELECT 
       id,
       name,
       action_type_id,
       reference,
       description
   FROM petel_schema.actions
   WHERE name = 'securitytest';
   ```

### Expected Result:
```
name           | action_type_id | reference        | description
securitytest   | 8              | /securitytest    | Auto-created from screen...
```

✅ **PASS**: `action_type_id = 8` and `reference = '/securitytest'`
❌ **FAIL**: Different action type or reference = 'unknown'

---

## Test 2: Button Click (Type 7)

**Objective**: Verify that clicking a SecureButton creates a Type 7 action.

### Steps:
1. Delete test action if exists:
   ```sql
   DELETE FROM petel_schema.actions WHERE name = 'securitytest_basicAction';
   ```

2. Navigate to SecurityTest page: `https://localhost:7169/securitytest`

3. Click the "בדיקה בסיסית" (Basic Test) button

4. Check action was created:
   ```sql
   SELECT 
       id,
       name,
       action_type_id,
       reference,
       description
   FROM petel_schema.actions
   WHERE name = 'securitytest_basicAction';
   ```

### Expected Result:
```
name                       | action_type_id | reference | description
securitytest_basicAction   | 7              | NULL      | Auto-created from screen...
```

✅ **PASS**: `action_type_id = 7` and `reference` is NULL or context value
❌ **FAIL**: Different action type or reference = 'unknown'

---

## Test 3: Students Page Access (Type 8)

**Objective**: Verify existing Students page access action.

### Steps:
1. Delete test action if exists:
   ```sql
   DELETE FROM petel_schema.actions WHERE name = 'students';
   ```

2. Navigate to Students page: `https://localhost:7169/students`

3. Check action was created:
   ```sql
   SELECT 
       id,
       name,
       action_type_id,
       reference,
       description
   FROM petel_schema.actions
   WHERE name = 'students';
   ```

### Expected Result:
```
name      | action_type_id | reference   | description
students  | 8              | /students   | Auto-created from screen...
```

✅ **PASS**: `action_type_id = 8` and `reference = '/students'`
❌ **FAIL**: Different action type or reference = 'unknown'

---

## Test 4: Students Add Button (Type 7)

**Objective**: Verify button actions on Students page.

### Steps:
1. Delete test action if exists:
   ```sql
   DELETE FROM petel_schema.actions WHERE name = 'students_addStudent';
   ```

2. Navigate to Students page: `https://localhost:7169/students`

3. Click the "הוסף תלמיד" (Add Student) button

4. Check action was created:
   ```sql
   SELECT 
       id,
       name,
       action_type_id,
       reference,
       description
   FROM petel_schema.actions
   WHERE name = 'students_addStudent';
   ```

### Expected Result:
```
name                 | action_type_id | reference | description
students_addStudent  | 7              | NULL      | Auto-created from screen...
```

✅ **PASS**: `action_type_id = 7`
❌ **FAIL**: Different action type

---

## Test 5: Action Audit Logs

**Objective**: Verify that audit logs are being created properly.

### Steps:
1. Perform tests 1-4 above

2. Check audit logs:
   ```sql
   SELECT 
       user_id,
       action_name,
       screen_name,
       function_name,
       event_type,
       result,
       timestamp
   FROM petel_schema.action_audit_logs
   WHERE action_name IN ('students', 'securitytest', 'students_addStudent', 'securitytest_basicAction')
   ORDER BY timestamp DESC
   LIMIT 20;
   ```

### Expected Result:
```
action_name              | screen_name | function_name | event_type   | result
securitytest             | navigation  | accessPage    | PAGE_ACCESS  | DENIED
securitytest_basicAction | securitytest| basicAction   | BUTTON_CLICK | DENIED
students                 | navigation  | accessPage    | PAGE_ACCESS  | GRANTED (if user has access)
students_addStudent      | students    | addStudent    | BUTTON_CLICK | DENIED
```

✅ **PASS**: Audit logs show correct event types and results
❌ **FAIL**: Missing audit logs or incorrect event types

---

## Test 6: Verification Query

**Objective**: Get overview of all auto-created actions.

```sql
-- All auto-created actions with type info
SELECT 
    a.id,
    a.name,
    at.name as action_type,
    a.action_type_id,
    a.reference,
    a.is_active,
    a.description,
    a.created_at
FROM petel_schema.actions a
LEFT JOIN petel_schema.action_types at ON a.action_type_id = at.id
WHERE a.description LIKE 'Auto-created%'
ORDER BY a.created_at DESC
LIMIT 50;
```

### Expected Result:
- **Type 7 (Button)**: Actions ending with function names (e.g., `students_addStudent`)
- **Type 8 (Screen/Page)**: Actions matching page names (e.g., `students`, `securitytest`)
- **Reference field**: 
  - Type 8: Should have page URL (e.g., `/students`)
  - Type 7: NULL or contextual value

---

## Test 7: Negative Test - Invalid Action Type

**Objective**: Verify system handles unexpected action types gracefully.

This is a manual backend test if needed - frontend should always send types 7 or 8.

---

## Summary Checklist

- [ ] Test 1: Page access creates Type 8 actions
- [ ] Test 2: Button clicks create Type 7 actions
- [ ] Test 3: Students page access is Type 8
- [ ] Test 4: Students buttons are Type 7
- [ ] Test 5: Audit logs capture all actions
- [ ] Test 6: Verification query shows correct types
- [ ] Reference field has meaningful values (not "unknown")
- [ ] All auto-created actions start as inactive (is_active = true but no role assignment = access denied)

---

## Troubleshooting

### Issue: Actions still created with action_type_id = 7 for pages

**Check:**
1. Backend `SecureActionRequest` DTO has `ActionType` property
2. Frontend `ActionSecurityService.VerifyPageAccessAsync` passes `actionType: 8`
3. Backend `ActionAuthorizationService.VerifyActionByNameAsync` accepts and uses actionType parameter
4. Backend `AutoCreateMissingActionAsync` uses passed actionType (not hardcoded 7)

### Issue: Reference field still shows "unknown"

**Check:**
1. Frontend passes `reference: $"/{pageName}"` in `VerifyPageAccessAsync`
2. Backend `SecureActionRequest` DTO has `Reference` property
3. Backend `AutoCreateMissingActionAsync` uses `reference ?? screenName`

### Issue: Actions not auto-creating

**Check:**
1. User is authenticated (has valid session)
2. Backend security service is running
3. Database connection is working
4. Check backend logs for errors

---

## Expected Database State After All Tests

```sql
-- Should have these actions
SELECT name, action_type_id, reference, is_active
FROM petel_schema.actions
WHERE name IN (
    'students',           -- Type 8, /students
    'securitytest',       -- Type 8, /securitytest
    'students_addStudent', -- Type 7, NULL
    'securitytest_basicAction' -- Type 7, NULL
);
```

**All 4 actions should exist with correct types and references.**
