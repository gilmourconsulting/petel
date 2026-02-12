-- Add System Configuration menu item
-- This adds an admin menu item for managing system configuration settings

-- Check if menu item already exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM petel_schema.menu_items 
        WHERE name = 'system-configuration'
    ) THEN
        INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
        VALUES ('system-configuration', '#system-configuration', 'הגדרות מערכת', NULL, 999, true);
        
        RAISE NOTICE 'System Configuration menu item added successfully';
    ELSE
        RAISE NOTICE 'System Configuration menu item already exists';
    END IF;
END
$$;

COMMIT;