-- =============================================================================
-- PetelAssistants — Meitar data integration
-- Adds symbol_code to entities (local authority beneficiary codes) and
-- meitar_data_filter_values config table for Meitar API filter values.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. entities: beneficiary/symbol code for local authorities ──────────────
ALTER TABLE shared_schema.entities
    ADD COLUMN IF NOT EXISTS symbol_code VARCHAR(20) NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_entities_symbol_code
    ON shared_schema.entities(symbol_code)
    WHERE symbol_code IS NOT NULL;

-- ─── 2. meitar_data_filter_values: filter config for Meitar data/query ───────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'meitar_data_filter_values'
    ) THEN
        CREATE TABLE shared_schema.meitar_data_filter_values (
            id            SERIAL PRIMARY KEY,
            file_name     VARCHAR(50)  NOT NULL,
            filter_field  VARCHAR(100) NOT NULL,
            filter_value  VARCHAR(500) NOT NULL,
            is_active     BOOLEAN NOT NULL DEFAULT true,
            display_order INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX idx_meitar_filter_file_field
            ON shared_schema.meitar_data_filter_values(file_name, filter_field)
            WHERE is_active;

        RAISE NOTICE 'Table meitar_data_filter_values created';
    END IF;
END $$;
