-- SQL/add-definition-json-column.sql
-- Adds the definition_json column to excel_report_definitions.
-- Idempotent (safe to re-run).

DO $$
BEGIN
    -- ── definition_json on excel_report_definitions ──────────────────────
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'excel_report_definitions'
          AND column_name  = 'definition_json'
    ) THEN
        ALTER TABLE petel_schema.excel_report_definitions
            ADD COLUMN definition_json TEXT NULL;

        RAISE NOTICE 'Column definition_json added to excel_report_definitions';
    ELSE
        RAISE NOTICE 'Column definition_json already exists on excel_report_definitions';
    END IF;
END
$$;
