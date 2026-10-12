-- Settings configuration-tables screen — page + button actions.
-- The /settings menu item already exists (הגדרות מערכת). This script:
-- 1. Ensures a page_action named 'settings' so SecurePageBase can grant access
-- 2. Adds button actions used by Settings.razor
-- 3. Grants them to Admin (role_id = 1)
--
-- Run on all environments. Idempotent.

DO $$
DECLARE
    page_type_id integer;
    button_type_id integer;
BEGIN
    SELECT id INTO page_type_id FROM petel_schema.action_types WHERE name = 'page_action';
    SELECT id INTO button_type_id FROM petel_schema.action_types WHERE name = 'button';

    IF page_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row page_action not found';
    END IF;

    IF button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row button not found';
    END IF;

    -- Name is unique on actions. A menu_item row named 'settings' (type 6) may
    -- already exist; only insert a page_action if the name is unused.
    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings', 'הגדרות מערכת',
           'גישה לעמוד הגדרות טבלאות תצורה', page_type_id, 'accessPage', 'settings', 10, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings_add', 'הוסף רשומה',
           'הוספת ערך לטבלת הגדרות', button_type_id, 'ShowAddModal', 'settings', 20, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings_add');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings_edit', 'ערוך רשומה',
           'עריכת ערך בטבלת הגדרות', button_type_id, 'ShowEditModal', 'settings', 30, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings_edit');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings_delete', 'הסר רשומה',
           'הסרה לוגית (is_active = false) של ערך בטבלת הגדרות', button_type_id, 'SoftDelete', 'settings', 40, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings_delete');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings_refresh', 'רענן נתונים',
           'רענון טבלת ההגדרות הפעילה', button_type_id, 'RefreshData', 'settings', 50, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings_refresh');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'settings_back', 'חזרה לדף ראשי',
           'חזרה לדף הראשי מעמוד הגדרות', button_type_id, 'NavigateBack', 'settings', 60, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'settings_back');
END $$;

INSERT INTO petel_schema.roles_actions (role_id, action_id, action_level, updated_at, update_user)
SELECT 1, a.id, 1, NOW(), 1
FROM petel_schema.actions a
WHERE a.name IN (
    'settings',
    'settings_add',
    'settings_edit',
    'settings_delete',
    'settings_refresh',
    'settings_back'
)
  AND NOT EXISTS (
      SELECT 1 FROM petel_schema.roles_actions ra
      WHERE ra.role_id = 1 AND ra.action_id = a.id
  );
