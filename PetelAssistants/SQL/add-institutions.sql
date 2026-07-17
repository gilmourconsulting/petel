-- =============================================================================
-- PetelAssistants — Tenant-owned institutions (schools / kindergartens)
-- Moves org units out of shared_schema.entities into assist_schema.institutions.
-- Fresh table: does not migrate existing child entity rows.
-- Retargets entitlements.school_entity_id → institution_id.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. assist_schema.institutions ────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'institutions'
    ) THEN
        CREATE TABLE assist_schema.institutions (
            id                     SERIAL PRIMARY KEY,
            entity_id              INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            name                   VARCHAR(200) NOT NULL,
            institution_type       VARCHAR(20) NOT NULL,
            school_level           VARCHAR(20) NULL,
            is_special_education   BOOLEAN NOT NULL DEFAULT false,
            is_active              BOOLEAN NOT NULL DEFAULT true,
            created_at             TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at             TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user            INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT institutions_type_check CHECK (
                institution_type IN ('school', 'kindergarten')
            ),
            CONSTRAINT institutions_school_level_check CHECK (
                (institution_type = 'school'
                    AND school_level IN ('elementary', 'high_school'))
                OR
                (institution_type = 'kindergarten' AND school_level IS NULL)
            ),
            CONSTRAINT institutions_entity_name_unique UNIQUE (entity_id, name)
        );

        CREATE INDEX idx_institutions_entity_id
            ON assist_schema.institutions(entity_id);

        CREATE INDEX idx_institutions_entity_type
            ON assist_schema.institutions(entity_id, institution_type);

        RAISE NOTICE 'Table assist_schema.institutions created';
    ELSE
        RAISE NOTICE 'Table assist_schema.institutions already exists';
    END IF;
END $$;

-- ─── 2. entitlements: drop school_entity_id constraints / column ──────────────
ALTER TABLE assist_schema.entitlements
    DROP CONSTRAINT IF EXISTS entitlements_school_required;

DROP INDEX IF EXISTS assist_schema.idx_entitlements_school_entity;

ALTER TABLE assist_schema.entitlements
    DROP CONSTRAINT IF EXISTS entitlements_school_entity_id_fkey;

ALTER TABLE assist_schema.entitlements
    DROP COLUMN IF EXISTS school_entity_id;

-- ─── 3. entitlements: add institution_id ──────────────────────────────────────
ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS institution_id INTEGER NULL
        REFERENCES assist_schema.institutions(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS idx_entitlements_institution_id
    ON assist_schema.entitlements(institution_id)
    WHERE institution_id IS NOT NULL;

-- Institution required when no orphaned rows remain (fresh installs / after wipe).
-- Existing rows with NULL institution_id keep the column nullable until re-created.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assist_schema'
          AND table_name = 'entitlements'
          AND constraint_name = 'entitlements_institution_required'
    ) AND NOT EXISTS (
        SELECT 1 FROM assist_schema.entitlements WHERE institution_id IS NULL
    ) THEN
        ALTER TABLE assist_schema.entitlements
            ADD CONSTRAINT entitlements_institution_required
            CHECK (institution_id IS NOT NULL);
        RAISE NOTICE 'Constraint entitlements_institution_required added';
    ELSE
        RAISE NOTICE 'Constraint entitlements_institution_required skipped (exists or null rows)';
    END IF;
END $$;

RAISE NOTICE 'add-institutions.sql completed';
