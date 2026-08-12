-- =============================================================================
-- PetelAssistants — Personal (student_help) entitlement Excel/PDF upload
-- Adds:
--   assist_schema.personal_entitlement_field_mappings
--   entitlements_personal_upload security action
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. personal_entitlement_field_mappings ───────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'personal_entitlement_field_mappings'
    ) THEN
        CREATE TABLE assist_schema.personal_entitlement_field_mappings (
            id           SERIAL PRIMARY KEY,
            entity_id    INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            mapping_json TEXT         NOT NULL,
            created_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id      INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user  INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT personal_entitlement_field_mappings_entity_unique UNIQUE (entity_id)
        );

        CREATE INDEX idx_personal_entitlement_field_mappings_entity_id
            ON assist_schema.personal_entitlement_field_mappings(entity_id);

        RAISE NOTICE 'Table assist_schema.personal_entitlement_field_mappings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.personal_entitlement_field_mappings already exists';
    END IF;
END $$;

-- ─── 2. entitlements_personal_upload security action ──────────────────────────
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
        'entitlements_personal_upload',
        E'\u05d4\u05e2\u05dc\u05d0\u05ea \u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05d0\u05d9\u05e9\u05d9\u05d5\u05ea',
        'entitlements',
        E'\u05d4\u05e2\u05dc\u05d0\u05ea \u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05d0\u05d9\u05e9\u05d9\u05d5\u05ea (student_help) \u05de-PDF \u05d0\u05d5 Excel',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'entitlements_personal_upload';

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

    RAISE NOTICE 'Personal entitlement upload security action seeded';
END $$;

RAISE NOTICE 'add-personal-entitlement-upload.sql completed';
