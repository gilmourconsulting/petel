-- ============================================================================
-- Menu Security Integration - Add Menu Items to Actions Table
-- ============================================================================
-- This script:
-- 1. Inserts all active menu items from menu_items table into actions table (type 6)
-- 2. Assigns all menu actions to Admin role (role_id = 1)
-- ============================================================================

-- Step 1: Insert menu items as actions (action_type_id = 6 for menu_item)
INSERT INTO petel_schema.actions (
    name, 
    display_name, 
    description, 
    action_type_id, 
    reference, 
    sort_order, 
    is_active,
    created_at,
    updated_at
)
SELECT 
    mi.name,                          -- Use menu item name as action name
    mi.text,                          -- Hebrew display text
    'Menu item: ' || mi.text,         -- Description
    6,                                -- action_type_id = 6 (menu_item)
    mi.reference,                     -- Menu reference (href)
    mi.sort_order,                    -- Preserve sort order
    mi.is_active,                     -- Preserve active status
    NOW(),                            -- created_at
    NOW()                             -- updated_at
FROM petel_schema.menu_items mi
WHERE mi.is_active = true
AND NOT EXISTS (
    -- Avoid duplicates: only insert if action doesn't already exist
    SELECT 1 FROM petel_schema.actions a 
    WHERE a.name = mi.name AND a.action_type_id = 6
);

-- Step 2: Assign all menu actions to Admin role (role_id = 1)
INSERT INTO petel_schema.roles_actions (
    role_id, 
    action_id, 
    action_level, 
    updated_at,
    update_user
)
SELECT 
    1,              -- Admin role
    a.id,           -- Action ID
    1,              -- Action level (full access)
    NOW(),          -- updated_at
    1               -- update_user (system/admin)
FROM petel_schema.actions a
WHERE a.action_type_id = 6  -- Menu items
AND a.is_active = true
AND NOT EXISTS (
    -- Avoid duplicates
    SELECT 1 FROM petel_schema.roles_actions ra 
    WHERE ra.role_id = 1 AND ra.action_id = a.id
);

-- ============================================================================
-- Verification Queries
-- ============================================================================

-- Count menu actions created
SELECT 
    COUNT(*) as total_menu_actions,
    COUNT(CASE WHEN is_active THEN 1 END) as active_menu_actions
FROM petel_schema.actions 
WHERE action_type_id = 6;

-- List all menu actions
SELECT 
    a.id,
    a.name,
    a.display_name,
    a.reference,
    a.sort_order,
    a.is_active
FROM petel_schema.actions a
WHERE a.action_type_id = 6
ORDER BY a.sort_order, a.name;

-- Verify admin has all menu permissions
SELECT 
    COUNT(*) as admin_menu_permissions
FROM petel_schema.roles_actions ra
JOIN petel_schema.actions a ON ra.action_id = a.id
WHERE ra.role_id = 1 AND a.action_type_id = 6;

-- Show menu items with their action mappings
SELECT 
    mi.id as menu_item_id,
    mi.name as menu_name,
    mi.text as menu_text,
    mi.reference,
    a.id as action_id,
    a.name as action_name,
    CASE WHEN ra.id IS NOT NULL THEN 'Yes' ELSE 'No' END as admin_has_access
FROM petel_schema.menu_items mi
LEFT JOIN petel_schema.actions a ON a.name = mi.name AND a.action_type_id = 6
LEFT JOIN petel_schema.roles_actions ra ON ra.action_id = a.id AND ra.role_id = 1
WHERE mi.is_active = true
ORDER BY mi.sort_order;

ALTER TABLE petel_schema.action_audit_logs
ADD COLUMN IF NOT EXISTS action_params VARCHAR(500),
ADD COLUMN IF NOT EXISTS description VARCHAR(1000);

-- Drop user_agent column (not needed)
ALTER TABLE petel_schema.action_audit_logs
DROP COLUMN IF EXISTS user_agent;

-- Add index on event_type for filtering by authorization type
CREATE INDEX IF NOT EXISTS idx_action_audit_logs_event_type 
ON petel_schema.action_audit_logs(event_type);

-- Add composite index for common queries
CREATE INDEX IF NOT EXISTS idx_action_audit_logs_user_result 
ON petel_schema.action_audit_logs(user_id, result, timestamp DESC);

COMMENT ON COLUMN petel_schema.action_audit_logs.event_type IS 
'Authorization type: ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL, FILE_UPLOAD, etc.';

COMMENT ON COLUMN petel_schema.action_audit_logs.action_params IS 
'Parameters passed to action (e.g., yearId, schoolId, file name)';

COMMENT ON COLUMN petel_schema.action_audit_logs.description IS 
'Optional human-readable description of the action';