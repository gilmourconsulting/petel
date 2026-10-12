-- Migration: Add is_active (logical-delete flag) to configuration lookup tables
-- that do not already have one. Needed by the Settings configuration-tables screen,
-- where "delete" is a logical delete (row kept so references do not fail, but the
-- value is flagged inactive so it can be excluded from drop-downs).
--
-- Tables that already have is_active (council_types, districts,
-- transaction_account_types, transaction_types, transaction_detail_types) are not
-- touched here.
--
-- Run on all environments. Idempotent (safe to re-run).

DO $$
DECLARE
    tbl TEXT;
    lookup_tables TEXT[] := ARRAY[
        'statuses',
        'entity_types',
        'alert_types',
        'alert_statuses',
        'alert_levels',
        'document_status_types',
        'special_needs_characterizations'
    ];
BEGIN
    FOREACH tbl IN ARRAY lookup_tables
    LOOP
        -- Only add the column if the table exists and the column does not
        IF EXISTS (
            SELECT FROM pg_tables
            WHERE schemaname = 'petel_schema'
              AND tablename = tbl
        ) AND NOT EXISTS (
            SELECT FROM information_schema.columns
            WHERE table_schema = 'petel_schema'
              AND table_name = tbl
              AND column_name = 'is_active'
        ) THEN
            EXECUTE format(
                'ALTER TABLE petel_schema.%I ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT true',
                tbl
            );
            RAISE NOTICE 'Column is_active added to %', tbl;
        ELSE
            RAISE NOTICE 'Column is_active already exists (or table missing) for %', tbl;
        END IF;
    END LOOP;
END
$$;
