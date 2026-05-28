-- ============================================================
-- Migration: add entity_id column to report_definitions
-- Purpose  : Supports per-entity Word (and Excel) templates.
--            When entity_id IS NULL the row is the shared default.
--            When entity_id = X the row is specific to that entity
--            (e.g. different logo/letterhead in the .docx template).
-- Safe to re-run (idempotent).
-- ============================================================

DO $$
BEGIN
    -- Add column if it does not yet exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'report_definitions'
          AND column_name  = 'entity_id'
    ) THEN
        ALTER TABLE petel_schema.report_definitions
            ADD COLUMN entity_id INTEGER NULL
            REFERENCES petel_schema.entities(id) ON DELETE SET NULL;

        CREATE INDEX IF NOT EXISTS idx_report_defs_entity_id
            ON petel_schema.report_definitions(entity_id);

        RAISE NOTICE 'Column entity_id added to report_definitions';
    ELSE
        RAISE NOTICE 'Column entity_id already exists in report_definitions – skipped';
    END IF;
END
$$;
