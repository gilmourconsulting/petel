-- =============================================================================
-- PetelAssistants — Main dashboard navigation to org units (schools / kindergartens)
-- Institutions are tenant-owned, not year-scoped; entry is from the home dashboard.
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
    ORDER BY CASE WHEN lower(name) = 'button' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('maindashboard_org_units', 'מעבר לבתי ספר וגנים', 'maindashboard', 'כפתור מעבר לניהול מוסדות מדף הבית', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'maindashboard_org_units';

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

    RAISE NOTICE 'Main dashboard org units navigation action seeded';
END $$;
