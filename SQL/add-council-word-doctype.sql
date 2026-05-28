-- Migration: Add document type for council Word letter exports
-- Idempotent: safe to re-run on all environments

DO $$
BEGIN
    -- Widen the "name" column if it cannot hold the Hebrew label
    IF EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'document_types'
          AND column_name  = 'name'
          AND character_maximum_length < 50
    ) THEN
        ALTER TABLE petel_schema.document_types
            ALTER COLUMN name TYPE VARCHAR(100);
        RAISE NOTICE 'Widened document_types.name to VARCHAR(100)';
    END IF;

    -- Insert the document type if it does not already exist
    INSERT INTO petel_schema.document_types (name, level, year_id, created_at)
    VALUES ('מכתב לרשות', 'רשת', NULL, NOW())
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'document_type "מכתב לרשות" ready';
END
$$;
