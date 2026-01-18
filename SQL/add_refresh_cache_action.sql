-- Add refresh cache actions for both roles and users management
-- These actions allow administrators to refresh the security cache when new actions are added

-- Add refresh cache action for roles page
INSERT INTO petel_schema.actions (
    name,
    description,
    action_type_id,
    reference,
    is_active,
    created_at
) VALUES (
    'roles_refreshcache',
    'רענן מטמון אבטחה - טוען מחדש את כל הפעולות והתפקידים',
    7, -- Button action type
    'roles',
    true,
    CURRENT_TIMESTAMP
) ON CONFLICT (name) DO UPDATE SET 
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active,
    updated_at = CURRENT_TIMESTAMP;

-- Add refresh cache action for users page
INSERT INTO petel_schema.actions (
    name,
    description,
    action_type_id,
    reference,
    is_active,
    created_at
) VALUES (
    'users_refreshcache',
    'רענן מטמון אבטחה - טוען מחדש את כל הפעולות והתפקידים',
    7, -- Button action type
    'users',
    true,
    CURRENT_TIMESTAMP
) ON CONFLICT (name) DO UPDATE SET 
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active,
    updated_at = CURRENT_TIMESTAMP;

-- Assign both actions to the administrator role (assuming role_id 1 is admin)
INSERT INTO petel_schema.roles_actions (
    role_id,
    action_id,
    updated_at
) 
SELECT 
    1 as role_id,
    a.id as action_id,
    CURRENT_TIMESTAMP
FROM petel_schema.actions a
WHERE a.name IN ('roles_refreshcache', 'users_refreshcache')
ON CONFLICT (role_id, action_id) DO NOTHING;

-- Show the results
SELECT 
    sa.name as action_name,
    sa.description,
    sa.is_active,
    r.name as role_name
FROM petel_schema.actions sa
LEFT JOIN petel_schema.roles_actions ra ON sa.id = ra.action_id
LEFT JOIN petel_schema.roles r ON ra.role_id = r.id
WHERE sa.name = 'roles_refreshcache'
ORDER BY r.name;