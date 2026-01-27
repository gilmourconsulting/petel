-- ===================================================================
-- Add Transaction Accounts Menu Item and Action
-- Created: 2026-01-27
-- Description: Adds action and menu item for transaction accounts page
-- ===================================================================

DO $$
DECLARE
    v_action_id INTEGER;
    max_sort INTEGER;
BEGIN
    -- Step 1: Create or get action for the page
    INSERT INTO petel_schema.actions (action_name, action_type, description, is_active)
    VALUES ('transactionaccounts', 'page', 'Transaction Accounts Page', true)
    ON CONFLICT (action_name) DO NOTHING
    RETURNING id INTO v_action_id;
    
    -- Get action_id if it already existed
    IF v_action_id IS NULL THEN
        SELECT id INTO v_action_id 
        FROM petel_schema.actions 
        WHERE action_name = 'transactionaccounts';
        RAISE NOTICE 'Action "transactionaccounts" already exists with id %', v_action_id;
    ELSE
        RAISE NOTICE 'Action "transactionaccounts" created with id %', v_action_id;
    END IF;
    
    -- Step 2: Create menu item if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM petel_schema.menu_items 
        WHERE name = 'transactionaccounts'
    ) THEN
        SELECT COALESCE(MAX(sort_order), 0) INTO max_sort 
        FROM petel_schema.menu_items;
        
        INSERT INTO petel_schema.menu_items 
            (name, reference, text, action_id, sort_order, is_active)
        VALUES 
            (
                'transactionaccounts',           -- name: matches page route
                '#transactionaccounts',          -- reference: navigation link
                'חשבונות תנועות',               -- text: Hebrew display text
                v_action_id,                     -- action_id: link to action
                max_sort + 10,                   -- sort_order: add at end
                true                             -- is_active: enabled
            );
        
        RAISE NOTICE 'Menu item "transactionaccounts" added successfully with sort_order % and action_id %', max_sort + 10, v_action_id;
    ELSE
        RAISE NOTICE 'Menu item "transactionaccounts" already exists';
        
        -- Update menu item with action_id if it was NULL
        UPDATE petel_schema.menu_items 
        SET action_id = v_action_id
        WHERE name = 'transactionaccounts' AND action_id IS NULL;
    END IF;
END
$$;

-- Verify action and menu item were added
SELECT a.id as action_id, a.action_name, a.action_type, a.description
FROM petel_schema.actions a
WHERE a.action_name = 'transactionaccounts';

SELECT m.id, m.name, m.reference, m.text, m.action_id, m.sort_order, m.is_active 
FROM petel_schema.menu_items m
WHERE m.name = 'transactionaccounts';
