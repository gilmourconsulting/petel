-- Add Council Summary menu item to the menu_items table
-- This allows users to access the council summary page from the menu

-- Check if menu item already exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM petel_schema.menu_items 
        WHERE name = 'councilsummary'
    ) THEN
        INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
        VALUES ('councilsummary', '/councilsummary', 'סיכום רשויות', NULL, 60, true);
        
        RAISE NOTICE 'Council Summary menu item added successfully';
    ELSE
        RAISE NOTICE 'Council Summary menu item already exists';
    END IF;
END
$$;
