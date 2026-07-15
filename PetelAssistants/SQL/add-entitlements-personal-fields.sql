-- =============================================================================
-- PetelAssistants — Entitlements personal-fields migration
-- Removes entitlement_kind and pupil_external_id; adds pupil identity columns;
-- tightens CHECK constraints; adds ministry_participation_options lookup table;
-- seeds validate_israeli_id_checksum system attribute.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. assistant_types: ensure level column exists ───────────────────────────
ALTER TABLE shared_schema.assistant_types
    ADD COLUMN IF NOT EXISTS level VARCHAR(30) NULL;

-- ─── 2. entitlements: drop obsolete columns ───────────────────────────────────
ALTER TABLE assist_schema.entitlements DROP COLUMN IF EXISTS entitlement_kind;
ALTER TABLE assist_schema.entitlements DROP COLUMN IF EXISTS pupil_external_id;

-- ─── 3. entitlements: drop old kind-based CHECK constraints ──────────────────
ALTER TABLE assist_schema.entitlements DROP CONSTRAINT IF EXISTS entitlements_kind_check;
ALTER TABLE assist_schema.entitlements DROP CONSTRAINT IF EXISTS entitlements_institutional_check;
ALTER TABLE assist_schema.entitlements DROP CONSTRAINT IF EXISTS entitlements_personal_check;

-- ─── 4. entitlements: add pupil identity columns ─────────────────────────────
-- pupil_id_number stores AES-encrypted ciphertext, so the column must be wide
-- enough for the encoded value (~44–88 chars); plaintext is validated in code.
ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS pupil_id_number  VARCHAR(500) NULL;

-- Widen to VARCHAR(500) if the column was previously created as VARCHAR(9).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'assist_schema'
          AND table_name   = 'entitlements'
          AND column_name  = 'pupil_id_number'
          AND character_maximum_length < 500
    ) THEN
        ALTER TABLE assist_schema.entitlements
            ALTER COLUMN pupil_id_number TYPE VARCHAR(500);
    END IF;
END $$;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS pupil_first_name VARCHAR(100) NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS pupil_last_name  VARCHAR(100) NULL;

-- ─── 5. entitlements: replace index that referenced entitlement_kind ──────────
DROP INDEX IF EXISTS assist_schema.idx_entitlements_entity_year_kind;

CREATE INDEX IF NOT EXISTS idx_entitlements_entity_year
    ON assist_schema.entitlements(entity_id, hebrew_year_id);

-- ─── 6. entitlements: new CHECK constraints ───────────────────────────────────

-- School is always mandatory (both personal and institutional)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assist_schema'
          AND table_name = 'entitlements'
          AND constraint_name = 'entitlements_school_required'
    ) THEN
        ALTER TABLE assist_schema.entitlements
            ADD CONSTRAINT entitlements_school_required
            CHECK (school_entity_id IS NOT NULL);
    END IF;
END $$;

-- Pupil fields are all-or-nothing:
--   personal  → pupil_id_number, pupil_first_name, pupil_last_name all NOT NULL
--   institutional → all three are NULL
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assist_schema'
          AND table_name = 'entitlements'
          AND constraint_name = 'entitlements_pupil_fields_consistent'
    ) THEN
        ALTER TABLE assist_schema.entitlements
            ADD CONSTRAINT entitlements_pupil_fields_consistent CHECK (
                (pupil_id_number IS NULL AND pupil_first_name IS NULL AND pupil_last_name IS NULL)
                OR
                (pupil_id_number IS NOT NULL AND pupil_first_name IS NOT NULL AND pupil_last_name IS NOT NULL)
            );
    END IF;
END $$;

-- ─── 7. system_attributes: validate_israeli_id_checksum ───────────────────────
-- NOTE: this attribute (id=20) already exists in the database and is managed
-- directly. It is a system-wide flag that controls Israeli national ID checksum
-- validation across all features (entitlements, persons, etc.). Do not re-seed
-- here; the INSERT below is a safety net for fresh environments only.
INSERT INTO shared_schema.system_attributes (name, value, value_type, description)
VALUES (
    'validate_israeli_id_checksum',
    'false',
    'bool',
    'האם לבדוק ספרת ביקורת של תעודת זהות (חל על כל הפיצ׳רים במערכת)'
)
ON CONFLICT (name) DO NOTHING;

-- ─── 8. ministry_participation_options lookup table ───────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema'
          AND tablename  = 'ministry_participation_options'
    ) THEN
        CREATE TABLE shared_schema.ministry_participation_options (
            id            SERIAL PRIMARY KEY,
            percentage    NUMERIC(5,2) NOT NULL UNIQUE,
            display_order INTEGER      NOT NULL DEFAULT 0,
            is_active     BOOLEAN      NOT NULL DEFAULT true
        );

        INSERT INTO shared_schema.ministry_participation_options (percentage, display_order)
        VALUES (100, 1), (70, 2);

        RAISE NOTICE 'Table ministry_participation_options created and seeded';
    ELSE
        RAISE NOTICE 'Table ministry_participation_options already exists';
    END IF;
END $$;

RAISE NOTICE 'add-entitlements-personal-fields.sql completed';
