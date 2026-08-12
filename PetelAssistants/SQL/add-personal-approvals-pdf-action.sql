-- =============================================================================
-- PetelAssistants — Personal approvals PDF → Excel extract
-- Adds entitlements_personal_approvals_pdf security action.
-- Idempotent — safe to run multiple times.
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
        'entitlements_personal_approvals_pdf',
        E'\u05d7\u05d9\u05dc\u05d5\u05e5 \u05d0\u05d9\u05e9\u05d5\u05e8\u05d9\u05dd \u05de-PDF',
        'entitlements',
        E'\u05d7\u05d9\u05dc\u05d5\u05e5 \u05d0\u05d9\u05e9\u05d5\u05e8\u05d9 \u05ea\u05d5\u05de\u05db\u05ea \u05d7\u05d9\u05e0\u05d5\u05da \u05d0\u05d9\u05e9\u05d9\u05ea \u05de-PDF \u05dc-Excel',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'entitlements_personal_approvals_pdf';

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

    RAISE NOTICE 'Personal approvals PDF security action seeded';
END $$;

RAISE NOTICE 'add-personal-approvals-pdf-action.sql completed';
