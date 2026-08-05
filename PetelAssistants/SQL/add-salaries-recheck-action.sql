-- =============================================================================
-- PetelAssistants — Salary recheck button security action
-- Creates the salaries_recheck button action for /salaries
-- (re-run person matching + allocation status for the displayed period).
-- Idempotent — safe to run multiple times.
-- Run after add-salaries-view-actions.sql
-- =============================================================================

DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('salaries_recheck', 'בדיקת התאמות שכר מחדש', 'salaries', 'כפתור בדיקה מחדש של התאמת אנשים ושיבוצים לנתוני שכר', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'salaries_recheck';

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

    RAISE NOTICE 'Salary recheck security action seeded';
END $$;
