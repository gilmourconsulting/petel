-- ============================================================================
-- Add About Page - Menu Item and Security Action
-- ============================================================================
-- This script:
-- 1. Adds the 'about' menu item to menu_items table
-- 2. Creates corresponding action for security permissions (action_type_id = 6)
-- 3. Assigns action to Admin role (role_id = 1)
-- ============================================================================

-- Step 1: Insert menu item
INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
VALUES ('about', '#about', 'אודות', NULL, 120, true)
ON CONFLICT DO NOTHING;

-- Step 2: Insert action for security
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
VALUES (
    'about',                        -- Action name (matches menu item name)
    'אודות',                        -- Hebrew display text
    'Menu item: אודות',             -- Description
    6,                              -- action_type_id = 6 (menu_item)
    '#about',                       -- Menu reference
    120,                            -- Sort order (matches menu item)
    true,                           -- Active
    NOW(),                          -- created_at
    NOW()                           -- updated_at
)
ON CONFLICT (name) DO NOTHING;

-- Step 3: Assign action to Admin role
INSERT INTO petel_schema.roles_actions (
    role_id, 
    action_id, 
    action_level, 
    updated_at,
    update_user
)
SELECT 
    1,              -- Admin role
    a.id,           -- Action ID (from actions table)
    1,              -- Action level (full access)
    NOW(),          -- updated_at
    1               -- update_user (system/admin)
FROM petel_schema.actions a
WHERE a.name = 'about' AND a.action_type_id = 6
AND NOT EXISTS (
    -- Avoid duplicates
    SELECT 1 FROM petel_schema.roles_actions ra 
    WHERE ra.role_id = 1 AND ra.action_id = a.id
);

-- ============================================================================
-- Verification Queries
-- ============================================================================

-- Verify menu item was created
SELECT * FROM petel_schema.menu_items WHERE name = 'about';

-- Verify action was created
SELECT 
    a.id,
    a.name,
    a.display_name,
    a.description,
    at.name AS action_type,
    a.reference,
    a.sort_order,
    a.is_active
FROM petel_schema.actions a
JOIN petel_schema.action_types at ON a.action_type_id = at.id
WHERE a.name = 'about';

-- Verify Admin has permission
SELECT 
    r.name AS role_name,
    a.name AS action_name,
    a.display_name,
    ra.action_level
FROM petel_schema.roles_actions ra
JOIN petel_schema.roles r ON ra.role_id = r.id
JOIN petel_schema.actions a ON ra.action_id = a.id
WHERE a.name = 'about' AND ra.role_id = 1;
