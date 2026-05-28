-- ================================================================
-- rename-report-tables.sql
-- Renames excel_report_* tables to report_* and adds format column.
-- Idempotent: safe to run multiple times.
-- ================================================================

DO $$
BEGIN

    -- ── Rename excel_report_definitions → report_definitions ─────────────
    IF EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_definitions'
    ) AND NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'report_definitions'
    ) THEN
        ALTER TABLE petel_schema.excel_report_definitions RENAME TO report_definitions;
        RAISE NOTICE 'Renamed excel_report_definitions → report_definitions';
    ELSE
        RAISE NOTICE 'excel_report_definitions rename skipped (already done or source missing)';
    END IF;

    -- ── Rename excel_report_templates → report_templates ─────────────────
    IF EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_templates'
    ) AND NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'report_templates'
    ) THEN
        ALTER TABLE petel_schema.excel_report_templates RENAME TO report_templates;
        RAISE NOTICE 'Renamed excel_report_templates → report_templates';
    ELSE
        RAISE NOTICE 'excel_report_templates rename skipped (already done or source missing)';
    END IF;

    -- ── Rename excel_report_queries → report_queries ──────────────────────
    IF EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_queries'
    ) AND NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'report_queries'
    ) THEN
        ALTER TABLE petel_schema.excel_report_queries RENAME TO report_queries;
        RAISE NOTICE 'Renamed excel_report_queries → report_queries';
    ELSE
        RAISE NOTICE 'excel_report_queries rename skipped (already done or source missing)';
    END IF;

    -- ── Rename excel_report_parameters → report_parameters ───────────────
    IF EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_parameters'
    ) AND NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'petel_schema' AND tablename = 'report_parameters'
    ) THEN
        ALTER TABLE petel_schema.excel_report_parameters RENAME TO report_parameters;
        RAISE NOTICE 'Renamed excel_report_parameters → report_parameters';
    ELSE
        RAISE NOTICE 'excel_report_parameters rename skipped (already done or source missing)';
    END IF;

    -- ── Add format column to report_definitions (if not exists) ──────────
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'report_definitions'
          AND column_name  = 'format'
    ) THEN
        ALTER TABLE petel_schema.report_definitions
            ADD COLUMN format VARCHAR(10) NOT NULL DEFAULT 'excel';
        RAISE NOTICE 'Added format column to report_definitions';
    ELSE
        RAISE NOTICE 'format column already exists on report_definitions';
    END IF;

    -- ── Update menu_items reference from /excelreports to /reports ────────
    UPDATE petel_schema.menu_items
       SET reference = '/reports',
           name      = 'reports'
     WHERE reference = '/excelreports'
       AND name      = 'excelreports';
    RAISE NOTICE 'Updated menu_items reference excelreports → reports (% rows)', found;

END
$$;
