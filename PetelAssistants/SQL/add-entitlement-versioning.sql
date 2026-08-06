-- =============================================================================
-- PetelAssistants — Entitlement versioning + class_classifications
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. class_classifications (shared lookup, mirrors petel_schema.special_needs_characterizations)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'class_classifications'
    ) THEN
        CREATE TABLE shared_schema.class_classifications (
            id           SERIAL PRIMARY KEY,
            name         VARCHAR(50) NOT NULL,
            foreign_id   INTEGER NULL,
            user_id      INTEGER NULL DEFAULT 0,
            sort_order   INTEGER NOT NULL DEFAULT 0,
            is_active    BOOLEAN NOT NULL DEFAULT true
        );
        RAISE NOTICE 'Table shared_schema.class_classifications created';
    ELSE
        RAISE NOTICE 'Table shared_schema.class_classifications already exists';
    END IF;
END $$;

-- Seed from ATH special_needs_characterizations when that table exists
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema' AND tablename = 'special_needs_characterizations'
    ) THEN
        INSERT INTO shared_schema.class_classifications (id, name, foreign_id, user_id)
        SELECT s.id, COALESCE(s.name, ''), s.foreign_id, COALESCE(s.user_id, 0)
        FROM petel_schema.special_needs_characterizations s
        WHERE NOT EXISTS (
            SELECT 1 FROM shared_schema.class_classifications c WHERE c.id = s.id
        );

        PERFORM setval(
            pg_get_serial_sequence('shared_schema.class_classifications', 'id'),
            GREATEST((SELECT COALESCE(MAX(id), 1) FROM shared_schema.class_classifications), 1)
        );
        RAISE NOTICE 'class_classifications seeded from special_needs_characterizations';
    ELSE
        RAISE NOTICE 'petel_schema.special_needs_characterizations not found — skip seed';
    END IF;
END $$;

-- ─── 2. entitlements versioning columns ───────────────────────────────────────
ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS master_entitlement_id INTEGER NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS is_last_version BOOLEAN NOT NULL DEFAULT true;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS is_cancelled BOOLEAN NOT NULL DEFAULT false;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS class_classification_id INTEGER NULL;

-- Backfill master_entitlement_id = id for existing rows
UPDATE assist_schema.entitlements
SET master_entitlement_id = id
WHERE master_entitlement_id IS NULL;

ALTER TABLE assist_schema.entitlements
    ALTER COLUMN master_entitlement_id SET NOT NULL;

-- Backfill cancelled from legacy is_active
UPDATE assist_schema.entitlements
SET is_cancelled = true,
    is_active = false
WHERE is_active = false
  AND is_cancelled = false;

-- FK to class_classifications
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_entitlements_class_classification'
    ) THEN
        ALTER TABLE assist_schema.entitlements
            ADD CONSTRAINT fk_entitlements_class_classification
            FOREIGN KEY (class_classification_id)
            REFERENCES shared_schema.class_classifications(id)
            ON DELETE SET NULL;
        RAISE NOTICE 'FK fk_entitlements_class_classification added';
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_entitlements_master
    ON assist_schema.entitlements(master_entitlement_id);

CREATE INDEX IF NOT EXISTS idx_entitlements_entity_year_last
    ON assist_schema.entitlements(entity_id, hebrew_year_id, is_last_version);
