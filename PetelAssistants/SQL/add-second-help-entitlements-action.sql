-- =============================================================================
-- PetelAssistants — Security action for second-help (סייעת שנייה) entitlement pick-list
-- Assumes shared_schema.assistant_types already has name = 'second_help' (e.g. id=5).
-- Idempotent.
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
    VALUES (
        'entitlements_second_help',
        E'\u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05dc\u05e1\u05d9\u05d9\u05e2\u05ea \u05e9\u05e0\u05d9\u05d4',
        'entitlements',
        E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05e1\u05d9\u05d9\u05e2\u05ea \u05e9\u05e0\u05d9\u05d4 \u05dc\u05d2\u05e0\u05d9 \u05d9\u05dc\u05d3\u05d9\u05dd',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'entitlements_second_help';

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

    RAISE NOTICE 'entitlements_second_help security action seeded';
END $$;

DO $$ BEGIN RAISE NOTICE 'add-second-help-entitlements-action.sql completed'; END $$;
