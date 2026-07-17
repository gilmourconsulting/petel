-- =============================================================================
-- PetelAssistants — Person Excel upload security action (assistants screen)
-- Idempotent — safe to run multiple times
-- Run after add-persons-actions.sql
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
        ('assistants_upload', 'העלאת קובץ סייעות', 'assistants', 'כפתור העלאת סייעות מקובץ Excel/CSV', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        SELECT id INTO v_action_id
        FROM shared_schema.actions
        WHERE name = 'assistants_upload';

        IF v_action_id IS NOT NULL THEN
            INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
            SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
            WHERE NOT EXISTS (
                SELECT 1 FROM assist_schema.roles_actions
                WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
            );
        END IF;
    END LOOP;

    RAISE NOTICE 'Person file upload action seeded for assistants screen';
END $$;
