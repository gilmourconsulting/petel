-- =============================================================================
-- PetelAssistants — Year management screen security actions
-- Idempotent — safe to run multiple times
-- =============================================================================

DO $$
DECLARE
    v_button_type_id     INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id          INTEGER;
    v_role_rec           RECORD;
BEGIN
    INSERT INTO shared_schema.action_types (name, description)
    VALUES ('page_action', 'Page-level access control')
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) = 'button'
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) = 'page_action'
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    -- ── yearmanagement screen ────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('yearmanagement_page_action', 'גישה למסך ניהול שנה',       'yearmanagement', 'גישה לדף ניהול שנה',                 v_page_action_type_id),
        ('yearmanagement_back',        'חזרה לדף הבית',             'yearmanagement', 'כפתור חזרה לדף הבית',                v_button_type_id),
        ('yearmanagement_assistants',  'מעבר למסך סייעות',          'yearmanagement', 'כפתור מעבר לניהול סייעות',           v_button_type_id),
        ('yearmanagement_entitlements','מעבר למסך זכאיות',          'yearmanagement', 'כפתור מעבר לניהול זכאיות',           v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── assistants screen ────────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('assistants_page_action', 'גישה למסך סייעות',           'assistants', 'גישה לדף ניהול סייעות',               v_page_action_type_id),
        ('assistants_back',        'חזרה לניהול שנה מ-סייעות',   'assistants', 'כפתור חזרה לניהול שנה',               v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── entitlements screen ──────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('entitlements_page_action', 'גישה למסך זכאיות',         'entitlements', 'גישה לדף ניהול זכאיות',             v_page_action_type_id),
        ('entitlements_back',        'חזרה לניהול שנה מ-זכאיות', 'entitlements', 'כפתור חזרה לניהול שנה',             v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── Assign to all existing roles (core post-login workflow) ──────────────
    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'yearmanagement_page_action', 'yearmanagement_back',
                'yearmanagement_assistants', 'yearmanagement_entitlements',
                'assistants_page_action', 'assistants_back',
                'entitlements_page_action', 'entitlements_back'
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

    RAISE NOTICE 'Year management screen actions seeded';
END $$;
