-- =============================================================================
-- PetelAssistants — Salary view screen security actions
-- Creates page/button actions for /salaries and navigation from dashboard / year hub.
-- Idempotent — safe to run multiple times.
-- Run after add-salary-upload.sql
-- =============================================================================

DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
    v_action_name         TEXT;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('salaries_page_action',           'גישה למסך נתוני שכר',     'salaries',       'גישה לדף צפייה בנתוני שכר',              v_page_action_type_id),
        ('salaries_back',                  'חזרה ממסך נתוני שכר',     'salaries',       'כפתור חזרה ממסך נתוני שכר',             v_button_type_id),
        ('salaries_refresh',               'רענון נתוני שכר',         'salaries',       'כפתור רענון רשימת נתוני שכר',           v_button_type_id),
        ('maindashboard_salaries_view',    'צפייה בנתוני שכר',        'maindashboard',  'כפתור מעבר לצפייה בנתוני שכר מדף הבית', v_button_type_id),
        ('yearmanagement_salaries_view',   'צפייה בנתוני שכר',        'yearmanagement', 'כפתור מעבר לצפייה בנתוני שכר מניהול שנה', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'salaries_page_action',
        'salaries_back',
        'salaries_refresh',
        'maindashboard_salaries_view',
        'yearmanagement_salaries_view'
    ]
    LOOP
        SELECT id INTO v_action_id
        FROM shared_schema.actions
        WHERE name = v_action_name;

        IF v_action_id IS NOT NULL THEN
            FOR v_role_rec IN
                SELECT id AS role_id, entity_id
                FROM assist_schema.roles
            LOOP
                INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
                SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM assist_schema.roles_actions
                    WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
                );
            END LOOP;
        END IF;
    END LOOP;

    RAISE NOTICE 'Salary view security actions seeded';
END $$;
