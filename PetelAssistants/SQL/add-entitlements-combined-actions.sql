-- =============================================================================
-- PetelAssistants — Combined entitlements page security actions
-- Replaces the separate personal/institutional entitlement nav actions with a
-- single unified entitlements action. Idempotent.
-- Run after add-entitlements-actions.sql.
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
    ORDER BY CASE WHEN lower(name) = 'button' THEN 0 ELSE 1 END
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    ORDER BY CASE WHEN lower(name) = 'page_action' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    -- ── YearManagement: single entitlements nav card ─────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES (
        'yearmanagement_entitlements',
        'מעבר לזכאויות',
        'yearmanagement',
        'כפתור מעבר לדף הזכאויות המאוחד',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    -- ── Combined entitlements page actions ───────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('entitlements_page_action', 'גישה לזכאויות',      'entitlements', 'גישה לדף הזכאויות המאוחד',    v_page_action_type_id),
        ('entitlements_back',        'חזרה לניהול שנה',     'entitlements', 'חזרה לניהול שנה',              v_button_type_id),
        ('entitlements_refresh',     'רענון זכאויות',       'entitlements', 'רענון רשימת הזכאויות',         v_button_type_id),
        ('entitlements_add',         'הוספת זכאות',         'entitlements', 'הוספת זכאות חדשה',             v_button_type_id),
        ('entitlements_edit',        'עריכת זכאות',         'entitlements', 'עריכת זכאות קיימת',            v_button_type_id),
        ('entitlements_deactivate',  'השבתת זכאות',         'entitlements', 'השבתת זכאות',                  v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── Assign all new actions to every existing role ────────────────────────
    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'yearmanagement_entitlements',
                'entitlements_page_action',
                'entitlements_back',
                'entitlements_refresh',
                'entitlements_add',
                'entitlements_edit',
                'entitlements_deactivate'
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

    RAISE NOTICE 'Combined entitlements security actions seeded';
END $$;
