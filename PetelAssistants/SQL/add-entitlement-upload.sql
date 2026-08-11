-- =============================================================================
-- PetelAssistants — Institutional entitlements Excel upload
-- Adds:
--   institutions.symbol (סמל מוסד)
--   assist_schema.entitlement_field_mappings
--   assist_schema.entitlement_upload_processes
--   yearmanagement_entitlements_upload security action
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. institutions.symbol ───────────────────────────────────────────────────
ALTER TABLE assist_schema.institutions
    ADD COLUMN IF NOT EXISTS symbol VARCHAR(20) NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_institutions_entity_symbol
    ON assist_schema.institutions(entity_id, symbol)
    WHERE symbol IS NOT NULL;

-- ─── 2. entitlement_field_mappings ───────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'entitlement_field_mappings'
    ) THEN
        CREATE TABLE assist_schema.entitlement_field_mappings (
            id           SERIAL PRIMARY KEY,
            entity_id    INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            mapping_json TEXT         NOT NULL,
            created_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id      INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user  INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT entitlement_field_mappings_entity_unique UNIQUE (entity_id)
        );

        CREATE INDEX idx_entitlement_field_mappings_entity_id
            ON assist_schema.entitlement_field_mappings(entity_id);

        RAISE NOTICE 'Table assist_schema.entitlement_field_mappings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.entitlement_field_mappings already exists';
    END IF;
END $$;

-- ─── 3. entitlement_upload_processes ─────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'entitlement_upload_processes'
    ) THEN
        CREATE TABLE assist_schema.entitlement_upload_processes (
            id               SERIAL PRIMARY KEY,
            entity_id        INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            hebrew_year_id   INTEGER      NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE RESTRICT,
            file_name        VARCHAR(255) NULL,
            created_count    INTEGER      NOT NULL DEFAULT 0,
            versioned_count  INTEGER      NOT NULL DEFAULT 0,
            skipped_count    INTEGER      NOT NULL DEFAULT 0,
            error_count      INTEGER      NOT NULL DEFAULT 0,
            created_at       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id          INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user      INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_entitlement_upload_processes_entity_id
            ON assist_schema.entitlement_upload_processes(entity_id);

        CREATE INDEX idx_entitlement_upload_processes_year
            ON assist_schema.entitlement_upload_processes(entity_id, hebrew_year_id);

        RAISE NOTICE 'Table assist_schema.entitlement_upload_processes created';
    ELSE
        RAISE NOTICE 'Table assist_schema.entitlement_upload_processes already exists';
    END IF;
END $$;

-- ─── 4. Security action ──────────────────────────────────────────────────────
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
        'yearmanagement_entitlements_upload',
        E'\u05d4\u05e2\u05dc\u05d0\u05ea \u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05de\u05d5\u05e1\u05d3\u05d9\u05d5\u05ea',
        'yearmanagement',
        E'\u05db\u05e4\u05ea\u05d5\u05e8 \u05d4\u05e2\u05dc\u05d0\u05ea \u05d6\u05db\u05d0\u05d5\u05d9\u05d5\u05ea \u05de\u05d5\u05e1\u05d3\u05d9\u05d5\u05ea \u05de\u05de\u05e1\u05da \u05e0\u05d9\u05d4\u05d5\u05dc \u05e9\u05e0\u05d4',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'yearmanagement_entitlements_upload';

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

    RAISE NOTICE 'Entitlement upload security action seeded';
END $$;

RAISE NOTICE 'add-entitlement-upload.sql completed';
