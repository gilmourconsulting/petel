-- =============================================================================
-- PetelAssistants — Person management security actions (assistants screen)
-- Idempotent — safe to run multiple times
-- Run after add-persons.sql and add-year-management-actions.sql
-- =============================================================================

DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
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
        ('assistants_refresh',       'רענון רשימת סייעות',        'assistants', 'כפתור רענון נתונים',              v_button_type_id),
        ('assistants_add',           'הוספת אדם',                 'assistants', 'כפתור הוספת אדם חדש',             v_button_type_id),
        ('assistants_edit',          'עריכת פרטי אדם',            'assistants', 'כפתור עריכת פרטי אדם',            v_button_type_id),
        ('assistants_view_history',  'צפייה בהיסטוריית גרסאות',   'assistants', 'כפתור צפייה בהיסטוריית גרסאות', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'assistants_refresh',
                'assistants_add',
                'assistants_edit',
                'assistants_view_history'
            )
        LOOP
            INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
            SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
            WHERE NOT EXISTS (
                SELECT 1 FROM assist_schema.roles_actions
                WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
            );
        END LOOP;
    END LOOP;

    RAISE NOTICE 'Person management actions seeded for assistants screen';
END $$;
