-- =============================================================================
-- PetelAssistants — Fix shared_schema.assistant_types.level column length
-- add-entitlements-personal-fields.sql intended `level VARCHAR(30)`, but the
-- column already existed (created elsewhere as VARCHAR(10)) so the
-- `ADD COLUMN IF NOT EXISTS` was a silent no-op. Codes like 'kindergarten'
-- (12 chars) then overflow VARCHAR(10) with a Postgres 22001 error on insert.
-- Widen the column to match the C# model (`[MaxLength(30)]`) and
-- shared_schema.assistant_levels.code (VARCHAR(30)).
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'shared_schema'
          AND table_name   = 'assistant_types'
          AND column_name  = 'level'
          AND character_maximum_length < 30
    ) THEN
        ALTER TABLE shared_schema.assistant_types
            ALTER COLUMN level TYPE VARCHAR(30);
        RAISE NOTICE 'Widened shared_schema.assistant_types.level to VARCHAR(30)';
    ELSE
        RAISE NOTICE 'shared_schema.assistant_types.level already VARCHAR(30) or wider';
    END IF;
END $$;
